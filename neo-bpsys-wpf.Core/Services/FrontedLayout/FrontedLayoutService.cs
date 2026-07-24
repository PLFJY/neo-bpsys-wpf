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
    private readonly ILogger<FrontedLayoutService> _logger;
    private readonly IFrontedLayoutPackageManager _packageManager;
    private readonly FrontedV3LayoutWindowConfigFactory _configFactory;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    /// <summary>
    /// 使用包管理器和记录器初始化布局服务。
    /// </summary>
    /// <param name="packageManager">用于活动布局包读取和写入的包管理器。</param>
    /// <param name="logger">用于布局加载和保存诊断的记录器。</param>
    public FrontedLayoutService(
        IFrontedLayoutPackageManager packageManager,
        ILogger<FrontedLayoutService>? logger)
    {
        _packageManager = packageManager;
        _logger = logger ?? NullLogger<FrontedLayoutService>.Instance;
        _configFactory = new FrontedV3LayoutWindowConfigFactory();
    }

    /// <summary>
    /// 使用包管理器、记录器和空模板工厂初始化布局服务。仅供测试使用。
    /// </summary>
    /// <param name="packageManager">用于活动布局包读取和写入的包管理器。</param>
    /// <param name="logger">用于布局加载和保存诊断的记录器。</param>
    /// <param name="configFactory">用于生成内存空模板的工厂。</param>
    internal FrontedLayoutService(
        IFrontedLayoutPackageManager packageManager,
        ILogger<FrontedLayoutService>? logger,
        FrontedV3LayoutWindowConfigFactory? configFactory)
        : this(packageManager, logger)
    {
        _configFactory = configFactory ?? new FrontedV3LayoutWindowConfigFactory();
    }

    /// <inheritdoc />
    public async Task<FrontedWindowConfig> LoadWindowConfigAsync(
        string canonicalWindowId,
        CancellationToken cancellationToken = default)
    {
        return (await LoadWindowConfigWithMetadataAsync(canonicalWindowId, cancellationToken)).Config;
    }

    /// <inheritdoc />
    public async Task<FrontedLayoutLoadResult> LoadWindowConfigWithMetadataAsync(
        string canonicalWindowId,
        CancellationToken cancellationToken = default)
    {
        var activeState = await _packageManager.GetActivePackageStateAsync(cancellationToken);
        var isActiveBuiltin = string.Equals(
            activeState.PackageId,
            FrontedLayoutPackageManager.BuiltInPackageId,
            StringComparison.OrdinalIgnoreCase);

        // Step 1: 尝试从激活包加载。
        var packagePath = _packageManager.GetPackageLayoutPath(activeState.PackageId, canonicalWindowId);
        if (File.Exists(packagePath))
        {
            try
            {
                return new FrontedLayoutLoadResult
                {
                    Config = await ReadConfigAsync(packagePath, cancellationToken),
                    Source = isActiveBuiltin ? FrontedLayoutSource.BuiltIn : FrontedLayoutSource.User,
                    Path = packagePath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load active package fronted layout. PackageId: {PackageId}, Window: {CanonicalWindowId}, Path: {Path}",
                    activeState.PackageId,
                    canonicalWindowId,
                    packagePath);
            }
        }

        // Step 2: 仅对内置窗口（非插件窗口），且激活包不是内置包时，回退到内置资源。
        if (!isActiveBuiltin
            && !FrontedV3LayoutWindowPathHelper.TryParsePluginCanonicalWindowId(canonicalWindowId, out _, out _))
        {
            var builtInPath = _packageManager.GetPackageLayoutPath(
                FrontedLayoutPackageManager.BuiltInPackageId,
                canonicalWindowId);
            if (File.Exists(builtInPath))
            {
                try
                {
                    return new FrontedLayoutLoadResult
                    {
                        Config = await ReadConfigAsync(builtInPath, cancellationToken),
                        Source = FrontedLayoutSource.BuiltIn,
                        Path = builtInPath
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to load built-in fronted layout. Window: {CanonicalWindowId}, Path: {Path}",
                        canonicalWindowId,
                        builtInPath);
                }
            }
        }

        // Step 3: 返回内存空模板。
        return new FrontedLayoutLoadResult
        {
            Config = _configFactory.CreateEmptyConfig(canonicalWindowId),
            Source = FrontedLayoutSource.EmptyTemplate,
            Path = null
        };
    }

    /// <inheritdoc />
    public async Task SaveWindowConfigAsync(
        string canonicalWindowId,
        FrontedWindowConfig config,
        CancellationToken cancellationToken = default)
    {
        var package = await _packageManager.EnsureWritableActivePackageAsync(cancellationToken);
        var path = _packageManager.GetPackageLayoutPath(package.PackageId, canonicalWindowId);
        await WriteConfigAsync(path, config, cancellationToken);
    }

    private async Task<FrontedWindowConfig> ReadConfigAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length > FrontedLayoutLimits.MaxLayoutJsonBytes)
        {
            throw new InvalidDataException("LayoutJsonTooLarge");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        // JSON null 视为损坏，按 spec 不允许返回 Source=User/BuiltIn 且 Config=null。
        return JsonSerializer.Deserialize<FrontedWindowConfig>(json, _jsonSerializerOptions)
            ?? throw new InvalidDataException(
                $"Fronted layout JSON deserialized to null. Path: {path}");
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
