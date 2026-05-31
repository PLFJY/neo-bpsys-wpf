using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
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

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public FrontedLayoutService()
        : this(
            new FrontedUserLayoutStore(),
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            NullLogger<FrontedLayoutService>.Instance)
    {
    }

    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        ILogger<FrontedLayoutService> logger)
        : this(
            userLayoutStore,
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            logger)
    {
    }

    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        string builtInLayoutRoot,
        ILogger<FrontedLayoutService>? logger)
    {
        _userLayoutStore = userLayoutStore;
        _builtInLayoutRoot = builtInLayoutRoot;
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

    /// <inheritdoc />
    public async Task SaveCanvasConfigAsync(
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config,
        CancellationToken cancellationToken = default)
    {
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
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<FrontedCanvasConfig>(json, _jsonSerializerOptions);
    }

    private static string? CombineErrors(string? first, string second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : $"{first}; {second}";
    }
}
