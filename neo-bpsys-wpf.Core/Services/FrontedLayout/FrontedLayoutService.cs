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
    /// 使用默认用户存储和内置布局根初始化布局服务。
    /// </summary>
    public FrontedLayoutService()
        : this(
            new FrontedUserLayoutStore(),
            new FrontedLayoutPackageManager(),
            NullLogger<FrontedLayoutService>.Instance)
    {
    }

    /// <summary>
    /// 使用用户存储和记录器初始化布局服务。
    /// </summary>
    /// <param name="userLayoutStore">用于可编辑布局的用户布局存储。</param>
    /// <param name="logger">用于布局加载和保存诊断的记录器。</param>
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
    /// 使用用户存储、窗口注册表和记录器初始化布局服务。
    /// </summary>
    /// <param name="userLayoutStore">用于可编辑布局的用户布局存储。</param>
    /// <param name="windowRegistry">用于解析插件默认布局描述符的注册表。</param>
    /// <param name="logger">用于布局加载和保存诊断的记录器。</param>
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
    /// 使用自定义内置布局根初始化布局服务。
    /// </summary>
    /// <param name="userLayoutStore">用于可编辑布局的用户布局存储。</param>
    /// <param name="builtInLayoutRoot">内置以窗口为中心的布局的根文件夹。</param>
    /// <param name="logger">用于布局加载和保存诊断的记录器。</param>
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
    /// 使用自定义内置布局根和注册表初始化布局服务。
    /// </summary>
    /// <param name="userLayoutStore">用于可编辑布局的用户布局存储。</param>
    /// <param name="builtInLayoutRoot">内置以窗口为中心的布局的根文件夹。</param>
    /// <param name="windowRegistry">用于解析插件默认布局描述符的注册表。</param>
    /// <param name="logger">用于布局加载和保存诊断的记录器。</param>
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
    /// 使用包管理器和默认内置布局根初始化布局服务。
    /// </summary>
    /// <param name="userLayoutStore">在没有活动可编辑包时使用的用户布局存储。</param>
    /// <param name="packageManager">用于活动布局包读取和写入的包管理器。</param>
    /// <param name="windowRegistry">用于解析插件默认布局描述符的注册表。</param>
    /// <param name="logger">用于布局加载和保存诊断的记录器。</param>
    public FrontedLayoutService(
        IFrontedUserLayoutStore userLayoutStore,
        IFrontedLayoutPackageManager packageManager,
        IFrontedWindowRegistry? windowRegistry,
        ILogger<FrontedLayoutService>? logger)
        : this(userLayoutStore, packageManager, logger)
    {
    }

    /// <summary>
    /// 使用所有显式依赖项初始化布局服务。
    /// </summary>
    /// <param name="userLayoutStore">在没有活动可编辑包时使用的用户布局存储。</param>
    /// <param name="builtInLayoutRoot">内置以窗口为中心的布局的根文件夹。</param>
    /// <param name="packageManager">用于活动包读取和写入的可选包管理器。</param>
    /// <param name="windowRegistry">用于解析插件默认布局描述符的可选注册表。</param>
    /// <param name="logger">用于布局加载和保存诊断的记录器。</param>
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
    /// 使用包管理器初始化布局服务。
    /// </summary>
    /// <param name="userLayoutStore">为可编辑包创建兼容性而保留的用户布局存储。</param>
    /// <param name="packageManager">用于活动布局包读取和写入的包管理器。</param>
    /// <param name="logger">用于布局加载和保存诊断的记录器。</param>
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
