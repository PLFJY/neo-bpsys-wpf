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
    private readonly IFrontedLayoutPackageManager _packageManager;

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
            new FrontedLayoutPackageManager(),
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
            new FrontedLayoutPackageManager(),
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
            new FrontedLayoutPackageManager(),
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
        : this(
            userLayoutStore,
            new FrontedLayoutPackageManager(
                GetIsolatedPackageRoot(builtInLayoutRoot),
                builtInLayoutRoot),
            logger)
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
        : this(
            userLayoutStore,
            new FrontedLayoutPackageManager(
                GetIsolatedPackageRoot(builtInLayoutRoot),
                builtInLayoutRoot),
            logger)
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
        : this(userLayoutStore, packageManager, logger)
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
        _packageManager = packageManager
            ?? new FrontedLayoutPackageManager(
                GetIsolatedPackageRoot(builtInLayoutRoot),
                builtInLayoutRoot);
        _logger = logger ?? NullLogger<FrontedLayoutService>.Instance;
    }

    /// <summary>
    /// Initializes the layout service with a package manager.
    /// </summary>
    /// <param name="userLayoutStore">User layout store retained for editable package creation compatibility.</param>
    /// <param name="packageManager">Package manager used for active layout package reads and writes.</param>
    /// <param name="logger">Logger for layout load and save diagnostics.</param>
    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        IFrontedLayoutPackageManager packageManager,
        ILogger<FrontedLayoutService>? logger)
    {
        _userLayoutStore = userLayoutStore;
        _packageManager = packageManager;
        _logger = logger ?? NullLogger<FrontedLayoutService>.Instance;
    }

    private static string GetIsolatedPackageRoot(string builtInLayoutRoot)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(builtInLayoutRoot));
        return Path.Combine(parent ?? AppConstants.FrontedLayoutPackagesPath, "FrontedLayoutPackages");
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
        var activeState = await _packageManager.GetActivePackageStateAsync(cancellationToken);
        var packagePath = _packageManager.GetPackageLayoutPath(activeState.PackageId, windowTypeName);
        if (File.Exists(packagePath))
        {
            try
            {
                return new FrontedLayoutLoadResult
                {
                    Config = await ReadConfigAsync(packagePath, cancellationToken),
                    Source = string.Equals(activeState.PackageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
                        ? FrontedLayoutSource.BuiltIn
                        : FrontedLayoutSource.User,
                    Path = packagePath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load active package fronted layout. PackageId: {PackageId}, Window: {WindowTypeName}, Path: {Path}",
                    activeState.PackageId,
                    windowTypeName,
                    packagePath);
                return new FrontedLayoutLoadResult
                {
                    Source = FrontedLayoutSource.MissingOrError,
                    Path = packagePath,
                    Error = ex.Message
                };
            }
        }

        return new FrontedLayoutLoadResult
        {
            Source = FrontedLayoutSource.MissingOrError,
            Path = packagePath,
            Error = $"Active package layout is missing: {activeState.PackageId}"
        };
    }

    /// <inheritdoc />
    public async Task SaveWindowConfigAsync(
        string windowTypeName,
        FrontedWindowConfig config,
        CancellationToken cancellationToken = default)
    {
        var package = await _packageManager.EnsureWritableActivePackageAsync(cancellationToken);
        var path = _packageManager.GetPackageLayoutPath(package.PackageId, windowTypeName);
        await WriteConfigAsync(path, config, cancellationToken);
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
        return JsonSerializer.Deserialize<FrontedWindowConfig>(json, _jsonSerializerOptions);
    }

    private async Task WriteConfigAsync(
        string path,
        FrontedWindowConfig config,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        config.Version = 3;
        var json = JsonSerializer.Serialize(config, _jsonSerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

}
