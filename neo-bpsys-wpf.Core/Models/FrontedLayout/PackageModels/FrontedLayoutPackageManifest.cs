using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// 前台布局包清单文件，描述包的元数据、内容和导入策略。
/// </summary>
public sealed class FrontedLayoutPackageManifest
{
    /// <summary>
    /// 布局格式标识，默认为 "neo-bpsys-bpui"。
    /// </summary>
    public string Format { get; set; } = "neo-bpsys-bpui";

    /// <summary>
    /// 格式版本号，默认为 3。
    /// </summary>
    public int FormatVersion { get; set; } = 3;

    /// <summary>
    /// 布局模型，默认为 WindowCentric。
    /// </summary>
    public string LayoutModel { get; set; } = FrontedLayoutConstants.WindowCentricLayoutModel;

    /// <summary>
    /// 包标识符。
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// 包名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 包描述。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 作者。
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 最低支持版本。
    /// </summary>
    public string MinVersion { get; set; } = string.Empty;

    /// <summary>
    /// 布局 Schema 版本号，默认为 3。
    /// </summary>
    public int LayoutSchemaVersion { get; set; } = 3;

    /// <summary>
    /// 插件依赖列表。
    /// </summary>
    public List<FrontedPluginDependency> PluginDependencies { get; set; } = [];

    /// <summary>
    /// 包内容定义。
    /// </summary>
    public FrontedLayoutPackageManifestContent Content { get; set; } = new();

    /// <summary>
    /// 导入策略配置。
    /// </summary>
    public FrontedLayoutPackageImportPolicy ImportPolicy { get; set; } = new();
}

/// <summary>
/// 前台布局包清单的内容定义，包含布局、资源和预览信息。
/// </summary>
public sealed class FrontedLayoutPackageManifestContent
{
    /// <summary>
    /// 布局条目列表。
    /// </summary>
    public List<FrontedLayoutPackageLayoutEntry> Layouts { get; set; } = [];

    /// <summary>
    /// 资源条目列表。
    /// </summary>
    public List<FrontedLayoutPackageResourceEntry> Resources { get; set; } = [];

    /// <summary>
    /// 可选的预览信息。
    /// </summary>
    public FrontedLayoutPackagePreviewEntry? Preview { get; set; }
}

/// <summary>
/// 前台布局包中的布局条目，描述窗口与布局文件的映射关系。
/// </summary>
public sealed class FrontedLayoutPackageLayoutEntry
{
    /// <summary>
    /// 窗口类型名称。
    /// </summary>
    public string Window { get; set; } = string.Empty;

    /// <summary>
    /// 布局文件路径。
    /// </summary>
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// 前台布局包中的资源条目。
/// </summary>
public sealed class FrontedLayoutPackageResourceEntry
{
    /// <summary>
    /// 资源标识符。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 资源种类。
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 资源文件路径。
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 资源 URI。
    /// </summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// 资源文件的 SHA256 哈希值。
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;
}

/// <summary>
/// 前台布局包的预览信息。
/// </summary>
public sealed class FrontedLayoutPackagePreviewEntry
{
    /// <summary>
    /// 封面图片路径。
    /// </summary>
    public string Cover { get; set; } = string.Empty;
}

/// <summary>
/// 前台布局包的导入策略配置。
/// </summary>
public sealed class FrontedLayoutPackageImportPolicy
{
    /// <summary>
    /// 覆盖现有用户布局的策略，默认为 "Ask"。
    /// </summary>
    public string OverwriteExistingUserLayouts { get; set; } = "Ask";

    /// <summary>
    /// 导入后是否需要重启。
    /// </summary>
    public bool RequireRestart { get; set; }
}
