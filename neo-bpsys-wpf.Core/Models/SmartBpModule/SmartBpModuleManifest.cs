namespace neo_bpsys_wpf.Core.Models.SmartBpModule;

/// <summary>
/// SmartBP 模块包清单。
/// </summary>
public sealed class SmartBpModuleManifest
{
    /// <summary>
    /// 组件标识符。
    /// </summary>
    public string ComponentId { get; set; } = SmartBpModuleConstants.ComponentId;

    /// <summary>
    /// 模块包版本。
    /// </summary>
    public string ModuleVersion { get; set; } = string.Empty;

    /// <summary>
    /// 运行时 ABI 版本。
    /// </summary>
    public int RuntimeAbiVersion { get; set; } = SmartBpModuleConstants.RuntimeAbiVersion;

    /// <summary>
    /// 运行时标识符。
    /// </summary>
    public string Rid { get; set; } = SmartBpModuleConstants.Rid;

    /// <summary>
    /// 要求的应用版本范围。
    /// </summary>
    public string RequiredAppVersion { get; set; } = ">=3.0.0";

    /// <summary>
    /// 模块中包含的包版本。
    /// </summary>
    public Dictionary<string, string> PackageVersions { get; set; } = [];

    /// <summary>
    /// 打包素材元数据。
    /// </summary>
    public SmartBpModuleAsset Asset { get; set; } = new();
}

/// <summary>
/// SmartBP 模块素材元数据。
/// </summary>
public sealed class SmartBpModuleAsset
{
    /// <summary>
    /// 素材文件名。
    /// </summary>
    public string Name { get; set; } = "SmartBpModule.7z";

    /// <summary>
    /// 素材下载 URL。
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 素材大小（字节）。
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 素材 SHA-256 哈希值。
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;
}
