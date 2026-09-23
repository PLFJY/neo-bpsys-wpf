#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台布局包导入器，负责将 .bpui 格式的压缩包或目录导入为可用的布局包。
/// 导入时会验证包格式、布局版本、资源完整性和安全性。
/// </summary>
public sealed partial class FrontedLayoutPackageImporter : IFrontedLayoutPackageImporter
{
    private const string ManifestFileName = "manifest.json";
    private readonly string _packageRoot;
    private readonly string _tempRoot;
    private readonly IFrontedLayoutPackageManager? _packageManager;
    private readonly ILogger<FrontedLayoutPackageImporter> _logger;
    private readonly FrontedLayoutValidator _validator;
    private readonly IFrontedImageSafetyService _imageSafetyService;
    private readonly FrontedImageCompressionService _imageCompressionService;
    private readonly IFrontedV3ControlRegistry? _controlRegistry;
    private readonly IFrontedPluginMetadataProvider? _pluginMetadataProvider;
    private readonly FrontedBehaviorEventCatalog? _behaviorEventCatalog;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    /// <summary>
    /// 使用默认路径初始化导入器。
    /// </summary>
    /// <param name="packageManager">包管理器。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="controlRegistry">控件注册表（可选）。</param>
    /// <param name="pluginMetadataProvider">插件元数据提供者（可选）。</param>
    public FrontedLayoutPackageImporter(
        IFrontedLayoutPackageManager packageManager,
        ILogger<FrontedLayoutPackageImporter> logger,
        IFrontedV3ControlRegistry? controlRegistry = null,
        IFrontedPluginMetadataProvider? pluginMetadataProvider = null)
        : this(
            packageManager,
            logger,
            controlRegistry,
            pluginMetadataProvider,
            behaviorEventCatalog: null)
    {
    }

    /// <summary>
    /// 使用默认路径和行为事件目录初始化导入器。
    /// </summary>
    /// <param name="packageManager">包管理器。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="controlRegistry">控件注册表（可选）。</param>
    /// <param name="pluginMetadataProvider">插件元数据提供者（可选）。</param>
    /// <param name="behaviorEventCatalog">当前启动已注册的行为事件目录。</param>
    public FrontedLayoutPackageImporter(
        IFrontedLayoutPackageManager packageManager,
        ILogger<FrontedLayoutPackageImporter> logger,
        IFrontedV3ControlRegistry? controlRegistry,
        IFrontedPluginMetadataProvider? pluginMetadataProvider,
        FrontedBehaviorEventCatalog? behaviorEventCatalog)
        : this(
            AppConstants.FrontedLayoutPackagesPath,
            Path.Combine(AppConstants.AppTempPath, "bpui-import"),
            packageManager,
            logger,
            controlRegistry,
            pluginMetadataProvider,
            behaviorEventCatalog)
    {
    }

    /// <summary>
    /// 使用自定义根路径初始化导入器。
    /// </summary>
    /// <param name="packageRoot">包存储根目录。</param>
    /// <param name="tempRoot">临时文件根目录。</param>
    /// <param name="packageManager">包管理器（可选）。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="controlRegistry">控件注册表（可选）。</param>
    /// <param name="pluginMetadataProvider">插件元数据提供者（可选）。</param>
    public FrontedLayoutPackageImporter(
        string packageRoot,
        string tempRoot,
        IFrontedLayoutPackageManager? packageManager = null,
        ILogger<FrontedLayoutPackageImporter>? logger = null,
        IFrontedV3ControlRegistry? controlRegistry = null,
        IFrontedPluginMetadataProvider? pluginMetadataProvider = null)
        : this(
            packageRoot,
            tempRoot,
            packageManager,
            logger,
            controlRegistry,
            pluginMetadataProvider,
            behaviorEventCatalog: null)
    {
    }

    /// <summary>
    /// 使用自定义根路径和行为事件目录初始化导入器。
    /// </summary>
    /// <param name="packageRoot">包存储根目录。</param>
    /// <param name="tempRoot">临时文件根目录。</param>
    /// <param name="packageManager">包管理器（可选）。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="controlRegistry">控件注册表（可选）。</param>
    /// <param name="pluginMetadataProvider">插件元数据提供者（可选）。</param>
    /// <param name="behaviorEventCatalog">当前启动已注册的行为事件目录。</param>
    public FrontedLayoutPackageImporter(
        string packageRoot,
        string tempRoot,
        IFrontedLayoutPackageManager? packageManager,
        ILogger<FrontedLayoutPackageImporter>? logger,
        IFrontedV3ControlRegistry? controlRegistry,
        IFrontedPluginMetadataProvider? pluginMetadataProvider,
        FrontedBehaviorEventCatalog? behaviorEventCatalog)
    {
        _packageRoot = packageRoot;
        _tempRoot = tempRoot;
        _packageManager = packageManager;
        _logger = logger ?? NullLogger<FrontedLayoutPackageImporter>.Instance;
        _controlRegistry = controlRegistry;
        _pluginMetadataProvider = pluginMetadataProvider;
        _behaviorEventCatalog = behaviorEventCatalog;
        _validator = new FrontedLayoutValidator(controlRegistry);
        _imageSafetyService = new FrontedImageSafetyService();
        _imageCompressionService = new FrontedImageCompressionService();
    }
}

#pragma warning restore CS1591
