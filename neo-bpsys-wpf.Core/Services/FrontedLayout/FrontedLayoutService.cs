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

    public FrontedLayoutService()
        : this(
            new FrontedUserLayoutStore(),
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            null,
            null,
            NullLogger<FrontedLayoutService>.Instance)
    {
    }

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

    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        string builtInLayoutRoot,
        ILogger<FrontedLayoutService>? logger)
        : this(userLayoutStore, builtInLayoutRoot, null, null, logger)
    {
    }

    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        string builtInLayoutRoot,
        IFrontedWindowRegistry? windowRegistry,
        ILogger<FrontedLayoutService>? logger)
        : this(userLayoutStore, builtInLayoutRoot, null, windowRegistry, logger)
    {
    }

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
    public async Task<FrontedCanvasConfig?> LoadCanvasConfigAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default)
    {
        return (await LoadCanvasConfigWithMetadataAsync(windowTypeName, canvasName, cancellationToken)).Config;
    }

    /// <inheritdoc />
    public async Task<FrontedLayoutLoadResult> LoadCanvasConfigWithMetadataAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default)
    {
        var userPath = _userLayoutStore.GetLayoutPath(windowTypeName, canvasName);
        var builtInPath = GetBuiltInDefaultLayoutPath(windowTypeName, canvasName);
        string? userLoadError = null;

        if (_packageManager is not null)
        {
            var activeState = await _packageManager.GetActivePackageStateAsync(cancellationToken);
            if (!string.Equals(activeState.PackageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
            {
                var packagePath = _packageManager.GetPackageLayoutPath(activeState.PackageId, windowTypeName, canvasName);
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
                            canvasName,
                            packagePath);
                    }
                }
            }
        }

        if (_userLayoutStore.Exists(windowTypeName, canvasName))
        {
            try
            {
                var config = await _userLayoutStore.LoadAsync(windowTypeName, canvasName, cancellationToken);
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
                    "User fronted layout loaded as null. Window: {WindowTypeName}, Canvas: {CanvasName}, Path: {Path}",
                    windowTypeName,
                    canvasName,
                    userPath);
            }
            catch (Exception ex)
            {
                userLoadError = ex.Message;
                _logger.LogWarning(
                    ex,
                    "Failed to load user fronted layout. Falling back to built-in layout. Window: {WindowTypeName}, Canvas: {CanvasName}, Path: {Path}",
                    windowTypeName,
                    canvasName,
                    userPath);
            }
        }

        if (TryGetPluginDefaultLayout(windowTypeName, canvasName, out var pluginDefaultPath))
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
                    canvasName,
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
                    canvasName,
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

    private bool TryGetPluginDefaultLayout(
        string windowTypeName,
        string canvasName,
        out string pluginDefaultPath)
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
            pluginDescriptor.WindowTypeName,
            $"{canvasName}.json");

        return File.Exists(pluginDefaultPath);
    }

    /// <inheritdoc />
    public async Task SaveCanvasConfigAsync(
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config,
        CancellationToken cancellationToken = default)
    {
        if (_packageManager is not null)
        {
            var package = await _packageManager.EnsureWritableActivePackageAsync(cancellationToken);
            var path = _packageManager.GetPackageLayoutPath(package.PackageId, windowTypeName, canvasName);
            await WriteConfigAsync(path, config, cancellationToken);
            return;
        }

        await _userLayoutStore.SaveAsync(windowTypeName, canvasName, config, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteUserLayoutAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default)
    {
        return _userLayoutStore.DeleteAsync(windowTypeName, canvasName, cancellationToken);
    }

    /// <inheritdoc />
    public bool UserLayoutExists(string windowTypeName, string canvasName)
    {
        return _userLayoutStore.Exists(windowTypeName, canvasName);
    }

    /// <inheritdoc />
    public string GetUserLayoutPath(string windowTypeName, string canvasName)
    {
        return _userLayoutStore.GetLayoutPath(windowTypeName, canvasName);
    }

    /// <inheritdoc />
    public string GetUserLayoutFolder(string windowTypeName, string canvasName)
    {
        return _userLayoutStore.GetLayoutFolder(windowTypeName, canvasName);
    }

    /// <inheritdoc />
    public string GetUserLayoutRootFolder()
    {
        return _userLayoutStore.GetRootFolder();
    }

    /// <inheritdoc />
    public string GetBuiltInDefaultLayoutPath(string windowTypeName, string canvasName)
    {
        return Path.Combine(
            _builtInLayoutRoot,
            windowTypeName,
            $"{canvasName}.json");
    }

    /// <inheritdoc />
    public string GetPluginDefaultLayoutPath(string pluginFolder, string windowTypeName, string canvasName)
    {
        return Path.Combine(pluginFolder, "FrontedLayouts", windowTypeName, $"{canvasName}.json");
    }

    private async Task<FrontedCanvasConfig?> ReadConfigAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length > FrontedLayoutLimits.MaxLayoutJsonBytes)
        {
            throw new InvalidDataException("LayoutJsonTooLarge");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<FrontedCanvasConfig>(json, _jsonSerializerOptions);
    }

    private async Task WriteConfigAsync(
        string path,
        FrontedCanvasConfig config,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        config.Version = 3;
        var json = JsonSerializer.Serialize(config, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private static string? CombineErrors(string? first, string second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : $"{first}; {second}";
    }
}
