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

    /// <summary>
    /// 发布时生成的模块文件完整性清单；旧包未声明时仅执行关键文件探针，不补写清单。
    /// </summary>
    public SmartBpModuleIntegrity? Integrity { get; set; }
}

/// <summary>SmartBP 模块不可变发布文件的完整性契约。</summary>
public sealed class SmartBpModuleIntegrity
{
    /// <summary>契约版本；当前支持版本 1。</summary>
    public int Version { get; set; }

    /// <summary>相对模块根目录的文件列表，不包含清单自身和用户下载的模型、CUDA 或缓存。</summary>
    public List<SmartBpModuleFile> Files { get; set; } = [];
}

/// <summary>SmartBP 发布文件的大小和 SHA-256 摘要。</summary>
public sealed class SmartBpModuleFile
{
    /// <summary>使用正斜线分隔的模块内相对路径。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>文件大小，单位为字节。</summary>
    public long Size { get; set; }

    /// <summary>文件的 SHA-256 十六进制摘要。</summary>
    public string Sha256 { get; set; } = string.Empty;
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
