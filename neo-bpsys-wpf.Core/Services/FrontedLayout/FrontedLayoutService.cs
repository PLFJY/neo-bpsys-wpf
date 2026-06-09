using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 默认 v3 前台布局配置读写服务。
/// </summary>
public class FrontedLayoutService : IFrontedLayoutService
{
    private readonly IFrontedUserLayoutStore _userLayoutStore;
    private readonly ILogger<FrontedLayoutService> _logger;
    private readonly string _builtInLayoutRoot;
    private readonly IFrontedWindowRegistry? _windowRegistry;
    private readonly IFrontedLayoutPackageManager? _packageManager;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    /// <summary>
    /// Initializes the layout service with the default user store and built-in layout root.
    /// </summary>
    public FrontedLayoutService()
        : this(
            new FrontedUserLayoutStore(),
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            null,
            null,
            NullLogger<FrontedLayoutService>.Instance)
    {
    }

    /// <summary>
    /// Initializes the layout service with a user store and logger.
    /// </summary>
    /// <param name="userLayoutStore">User layout store used for editable layouts.</param>
    /// <param name="logger">Logger for layout load and save diagnostics.</param>
    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        ILogger<FrontedLayoutService> logger)
        : this(
            userLayoutStore,
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            null,
            null,
            logger)
    {
    }

    /// <summary>
    /// Initializes the layout service with a user store, window registry, and logger.
    /// </summary>
    /// <param name="userLayoutStore">User layout store used for editable layouts.</param>
    /// <param name="windowRegistry">Registry used to resolve plugin default layout descriptors.</param>
    /// <param name="logger">Logger for layout load and save diagnostics.</param>
    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        IFrontedWindowRegistry windowRegistry,
        ILogger<FrontedLayoutService> logger)
        : this(
            userLayoutStore,
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            null,
            windowRegistry,
            logger)
    {
    }

    /// <summary>
    /// Initializes the layout service with a custom built-in layout root.
    /// </summary>
    /// <param name="userLayoutStore">User layout store used for editable layouts.</param>
    /// <param name="builtInLayoutRoot">Root folder for built-in window-centric layouts.</param>
    /// <param name="logger">Logger for layout load and save diagnostics.</param>
    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        string builtInLayoutRoot,
        ILogger<FrontedLayoutService>? logger)
        : this(userLayoutStore, builtInLayoutRoot, null, null, logger)
    {
    }

    /// <summary>
    /// Initializes the layout service with a custom built-in layout root and registry.
    /// </summary>
    /// <param name="userLayoutStore">User layout store used for editable layouts.</param>
    /// <param name="builtInLayoutRoot">Root folder for built-in window-centric layouts.</param>
    /// <param name="windowRegistry">Registry used to resolve plugin default layout descriptors.</param>
    /// <param name="logger">Logger for layout load and save diagnostics.</param>
    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        string builtInLayoutRoot,
        IFrontedWindowRegistry? windowRegistry,
        ILogger<FrontedLayoutService>? logger)
        : this(userLayoutStore, builtInLayoutRoot, null, windowRegistry, logger)
    {
    }

    /// <summary>
    /// Initializes the layout service with a package manager and the default built-in layout root.
    /// </summary>
    /// <param name="userLayoutStore">User layout store used when no editable package is active.</param>
    /// <param name="packageManager">Package manager used for active layout package reads and writes.</param>
    /// <param name="windowRegistry">Registry used to resolve plugin default layout descriptors.</param>
    /// <param name="logger">Logger for layout load and save diagnostics.</param>
    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        IFrontedLayoutPackageManager packageManager,
        IFrontedWindowRegistry? windowRegistry,
        ILogger<FrontedLayoutService>? logger)
        : this(
            userLayoutStore,
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            packageManager,
            windowRegistry,
            logger)
    {
    }

    /// <summary>
    /// Initializes the layout service with all explicit dependencies.
    /// </summary>
    /// <param name="userLayoutStore">User layout store used when no editable package is active.</param>
    /// <param name="builtInLayoutRoot">Root folder for built-in window-centric layouts.</param>
    /// <param name="packageManager">Optional package manager used for active package reads and writes.</param>
    /// <param name="windowRegistry">Optional registry used to resolve plugin default layout descriptors.</param>
    /// <param name="logger">Logger for layout load and save diagnostics.</param>
    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        string builtInLayoutRoot,
        IFrontedLayoutPackageManager? packageManager,
        IFrontedWindowRegistry? windowRegistry,
        ILogger<FrontedLayoutService>? logger)
    {
        _userLayoutStore = userLayoutStore;
        _builtInLayoutRoot = builtInLayoutRoot;
        _packageManager = packageManager;
        _windowRegistry = windowRegistry;
        _logger = logger ?? NullLogger<FrontedLayoutService>.Instance;
    }

    /// <inheritdoc />
    public async Task<FrontedWindowConfig?> LoadWindowConfigAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default)
    {
        return (await LoadWindowConfigWithMetadataAsync(windowTypeName, cancellationToken)).Config;
    }

    /// <inheritdoc />
    public async Task<FrontedLayoutLoadResult> LoadWindowConfigWithMetadataAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default)
    {
        var userPath = _userLayoutStore.GetLayoutPath(windowTypeName);
        var builtInPath = GetBuiltInDefaultWindowLayoutPath(windowTypeName);
        string? userLoadError = null;

        if (_packageManager is not null)
        {
            var activeState = await _packageManager.GetActivePackageStateAsync(cancellationToken);
            if (!string.Equals(activeState.PackageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
            {
                var packagePath = _packageManager.GetPackageLayoutPath(activeState.PackageId, windowTypeName);
                if (File.Exists(packagePath))
                {
                    try
                    {
                        return new FrontedLayoutLoadResult
                        {
                            Config = await ReadConfigAsync(packagePath, cancellationToken),
                            Source = FrontedLayoutSource.User,
                            Path = packagePath
                        };
                    }
                    catch (Exception ex)
                    {
                        userLoadError = ex.Message;
                        _logger.LogWarning(
                            ex,
                            "Failed to load active package fronted layout. Falling back to defaults. PackageId: {PackageId}, Window: {WindowTypeName}, Canvas: {CanvasName}, Path: {Path}",
                            activeState.PackageId,
                            windowTypeName,
                            FrontedLayoutConstants.BaseCanvasName,
                            packagePath);
                    }
                }
            }
        }

        if (_userLayoutStore.Exists(windowTypeName))
        {
            try
            {
                var config = await _userLayoutStore.LoadAsync(windowTypeName, cancellationToken);
                if (config is not null)
                {
                    return new FrontedLayoutLoadResult
                    {
                        Config = config,
                        Source = FrontedLayoutSource.User,
                        Path = userPath
                    };
                }

                userLoadError = "User layout file exists but produced no config.";
                _logger.LogWarning(
                    "User fronted layout loaded as null. Window: {WindowTypeName}, Path: {Path}",
                    windowTypeName,
                    userPath);
            }
            catch (Exception ex)
            {
                userLoadError = ex.Message;
                _logger.LogWarning(
                    ex,
                    "Failed to load user fronted layout. Falling back to built-in layout. Window: {WindowTypeName}, Canvas: {CanvasName}, Path: {Path}",
                    windowTypeName,
                    FrontedLayoutConstants.BaseCanvasName,
                    userPath);
            }
        }

        if (TryGetPluginDefaultLayout(windowTypeName, out var pluginDefaultPath))
        {
            try
            {
                return new FrontedLayoutLoadResult
                {
                    Config = await ReadConfigAsync(pluginDefaultPath, cancellationToken),
                    Source = FrontedLayoutSource.PluginDefault,
                    Path = pluginDefaultPath,
                    Error = userLoadError
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load plugin default fronted layout. Window: {WindowTypeName}, Canvas: {CanvasName}, Path: {Path}",
                    windowTypeName,
                    FrontedLayoutConstants.BaseCanvasName,
                    pluginDefaultPath);
                return new FrontedLayoutLoadResult
                {
                    Source = FrontedLayoutSource.MissingOrError,
                    Path = pluginDefaultPath,
                    Error = CombineErrors(userLoadError, ex.Message)
                };
            }
        }

        if (File.Exists(builtInPath))
        {
            try
            {
                return new FrontedLayoutLoadResult
                {
                    Config = await ReadConfigAsync(builtInPath, cancellationToken),
                    Source = FrontedLayoutSource.BuiltIn,
                    Path = builtInPath,
                    Error = userLoadError
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load built-in fronted layout. Window: {WindowTypeName}, Canvas: {CanvasName}, Path: {Path}",
                    windowTypeName,
                    FrontedLayoutConstants.BaseCanvasName,
                    builtInPath);
                return new FrontedLayoutLoadResult
                {
                    Source = FrontedLayoutSource.MissingOrError,
                    Path = builtInPath,
                    Error = CombineErrors(userLoadError, ex.Message)
                };
            }
        }

        return new FrontedLayoutLoadResult
        {
            Source = FrontedLayoutSource.MissingOrError,
            Path = builtInPath,
            Error = userLoadError
        };
    }

    private bool TryGetPluginDefaultLayout(string windowTypeName, out string pluginDefaultPath)
    {
        pluginDefaultPath = string.Empty;
        if (_windowRegistry is null)
        {
            return false;
        }

        if (!_windowRegistry.TryGetByFullWindowType(windowTypeName, out var descriptor))
        {
            return false;
        }

        if (descriptor is not FrontedPluginWindowDescriptor pluginDescriptor
            || pluginDescriptor.Kind != FrontedWindowKind.PluginLayout)
        {
            return false;
        }

        var folder = pluginDescriptor.PluginFolder;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        pluginDefaultPath = Path.Combine(
            folder,
            pluginDescriptor.DefaultLayoutRoot,
            $"{pluginDescriptor.WindowTypeName}.json");

        return File.Exists(pluginDefaultPath);
    }

    /// <inheritdoc />
    public async Task SaveWindowConfigAsync(
        string windowTypeName,
        FrontedWindowConfig config,
        CancellationToken cancellationToken = default)
    {
        if (_packageManager is not null)
        {
            var package = await _packageManager.EnsureWritableActivePackageAsync(cancellationToken);
            var path = _packageManager.GetPackageLayoutPath(package.PackageId, windowTypeName);
            await WriteConfigAsync(path, config, cancellationToken);
            return;
        }

        await _userLayoutStore.SaveAsync(windowTypeName, config, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteUserWindowLayoutAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default)
    {
        return _userLayoutStore.DeleteAsync(windowTypeName, cancellationToken);
    }

    /// <inheritdoc />
    public bool UserWindowLayoutExists(string windowTypeName)
    {
        return _userLayoutStore.Exists(windowTypeName);
    }

    /// <inheritdoc />
    public string GetUserWindowLayoutPath(string windowTypeName)
    {
        return _userLayoutStore.GetLayoutPath(windowTypeName);
    }

    /// <inheritdoc />
    public string GetUserLayoutRootFolder()
    {
        return _userLayoutStore.GetRootFolder();
    }

    /// <inheritdoc />
    public async Task<FrontedWindowConfig?> LoadBuiltInDefaultWindowLayoutAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default)
    {
        var builtInPath = GetBuiltInDefaultWindowLayoutPath(windowTypeName);
        if (!File.Exists(builtInPath))
        {
            return null;
        }

        try
        {
            return await ReadConfigAsync(builtInPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load built-in default fronted layout. Window: {WindowTypeName}, Canvas: {CanvasName}, Path: {Path}",
                windowTypeName,
                FrontedLayoutConstants.BaseCanvasName,
                builtInPath);
            return null;
        }
    }

    /// <inheritdoc />
    public string GetBuiltInDefaultWindowLayoutPath(string windowTypeName)
    {
        return Path.Combine(
            _builtInLayoutRoot,
            FrontedLayoutWindowPathHelper.GetLayoutRelativePath(windowTypeName));
    }

    /// <inheritdoc />
    public string GetPluginDefaultWindowLayoutPath(string pluginFolder, string windowTypeName)
    {
        return Path.Combine(pluginFolder, "FrontedLayouts", $"{windowTypeName}.json");
    }

    private async Task<FrontedWindowConfig?> ReadConfigAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length > FrontedLayoutLimits.MaxLayoutJsonBytes)
        {
            throw new InvalidDataException("LayoutJsonTooLarge");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var config = JsonSerializer.Deserialize<FrontedWindowConfig>(json, _jsonSerializerOptions);
        config?.SyncWindowSizeToCanvas();
        return config;
    }

    private async Task WriteConfigAsync(
        string path,
        FrontedWindowConfig config,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        config.Version = 3;
        config.SyncWindowSizeToCanvas();
        var json = JsonSerializer.Serialize(config, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private static string? CombineErrors(string? first, string second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : $"{first}; {second}";
    }
}
