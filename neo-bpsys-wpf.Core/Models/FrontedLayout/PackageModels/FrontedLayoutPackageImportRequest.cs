using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// 导入设计器 v3 <c>.bpui</c> 布局包的请求。
/// </summary>
public sealed class FrontedLayoutPackageImportRequest
{
    /// <summary>
    /// 用户选择的包文件路径。
    /// </summary>
    public string PackagePath { get; set; } = string.Empty;

    /// <summary>
    /// 指示是否允许替换已安装的、具有相同包标识的包。
    /// </summary>
    public bool ReplaceExisting { get; set; }

    /// <summary>
    /// 指示是否在安装后立即激活该包。
    /// </summary>
    public bool ActivateAfterImport { get; set; }

    /// <summary>
    /// 指示是否保留缺失插件控件和插件窗口布局，而不是移除。
    /// 保留的插件控件在设计器中显示为占位符，并在运行时渲染时跳过。
    /// </summary>
    public bool PreserveMissingPlugins { get; set; } = true;

    /// <summary>
    /// 指示是否允许在导入暂存副本中压缩所有超出图片安全限制的资源后重试导入。
    /// 源 <c>.bpui</c> 文件不会被修改。
    /// </summary>
    public bool CompressOversizedImages { get; set; }
}

/// <summary>
/// 导入设计器 v3 <c>.bpui</c> 包的结果。
/// </summary>
public sealed class FrontedLayoutPackageImportResult
{
    /// <summary>
    /// 指示包是否导入成功。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 可用时为导入的包标识。
    /// </summary>
    public string? PackageId { get; set; }

    /// <summary>
    /// 已安装包的文件夹路径。
    /// </summary>
    public string? InstalledPath { get; set; }

    /// <summary>
    /// 从包中安装的布局数量。
    /// </summary>
    public int LayoutCount { get; set; }

    /// <summary>
    /// 从包中安装的资源数量。
    /// </summary>
    public int ResourceCount { get; set; }

    /// <summary>
    /// 面向用户或可记录的导入错误消息。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 指示所选包是否被识别为旧版包。
    /// </summary>
    public bool IsLegacyPackage { get; set; }

    /// <summary>
    /// 指示包清单是否需要更新版本的宿主应用。
    /// </summary>
    public bool RequiresNewerApp { get; set; }

    /// <summary>
    /// 指示是否已存在具有相同标识的已安装包。
    /// </summary>
    public bool PackageAlreadyExists { get; set; }

    /// <summary>
    /// 因文件体积或像素尺寸超出导入限制而未能直接导入的图片资源。
    /// 调用方可征得用户确认后，以 <see cref="FrontedLayoutPackageImportRequest.CompressOversizedImages"/> 重试。
    /// </summary>
    public List<FrontedLayoutPackageImageIssue> OversizedImages { get; set; } = [];

    /// <summary>
    /// 指示导入是否因超限图片而提供了压缩重试选项。
    /// </summary>
    public bool HasOversizedImages => OversizedImages.Count > 0;

    /// <summary>
    /// 此次导入中已在暂存副本压缩的图片资源。
    /// </summary>
    public List<FrontedLayoutPackageImageCompression> CompressedImages { get; set; } = [];

    /// <summary>
    /// 指示包是否包含其插件缺失的控件。
    /// </summary>
    public bool HasMissingPluginControls => MissingPluginControls.Count > 0;

    /// <summary>
    /// 缺失的插件控件，用于设计器占位符或强制导入时移除。
    /// </summary>
    public List<FrontedLayoutPackagePluginControlIssue> MissingPluginControls { get; set; } = [];

    /// <summary>
    /// 指示是否存在插件依赖缺失或版本不满足要求的情况。
    /// </summary>
    public bool HasUnsatisfiedPluginDependencies => UnsatisfiedPluginDependencies.Count > 0;

    /// <summary>
    /// 从清单、画布声明和扫描的控件中收集的插件依赖问题。
    /// </summary>
    public List<FrontedLayoutPackagePluginDependencyIssue> UnsatisfiedPluginDependencies { get; set; } = [];

}

/// <summary>
/// 描述一个因安全限制而无法直接导入的包内图片资源。
/// </summary>
public sealed class FrontedLayoutPackageImageIssue
{
    /// <summary>
    /// 包内资源的相对路径。
    /// </summary>
    public string ResourcePath { get; set; } = string.Empty;

    /// <summary>
    /// 图片校验失败代码。
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// 图片文件的字节数。
    /// </summary>
    public long FileBytes { get; set; }

    /// <summary>
    /// 图片像素宽度；无法读取时为零。
    /// </summary>
    public int PixelWidth { get; set; }

    /// <summary>
    /// 图片像素高度；无法读取时为零。
    /// </summary>
    public int PixelHeight { get; set; }
}

/// <summary>
/// 描述一次包内图片压缩的结果。
/// </summary>
public sealed class FrontedLayoutPackageImageCompression
{
    /// <summary>
    /// 包内资源的相对路径。
    /// </summary>
    public string ResourcePath { get; set; } = string.Empty;

    /// <summary>
    /// 压缩前的文件字节数。
    /// </summary>
    public long OriginalBytes { get; set; }

    /// <summary>
    /// 压缩后的文件字节数。
    /// </summary>
    public long CompressedBytes { get; set; }
}

/// <summary>
/// 标识当前无法具现化的一个插件控件。
/// </summary>
public class FrontedLayoutPackagePluginControlIssue
{
    /// <summary>
    /// 包含该控件的完整窗口类型。
    /// </summary>
    public string Window { get; set; } = string.Empty;

    /// <summary>
    /// v3 布局 JSON 中的控件名称。
    /// </summary>
    public string ControlName { get; set; } = string.Empty;

    /// <summary>
    /// 完整的插件控件类型，例如 <c>plugin:top.plfjy.example/TeamCard</c>。
    /// </summary>
    public string ControlType { get; set; } = string.Empty;

    /// <summary>
    /// 缺失的插件包标识。
    /// </summary>
    public string PackageId { get; set; } = string.Empty;
}

/// <summary>
/// 描述一个缺失或版本不满足要求的插件依赖。
/// </summary>
public sealed class FrontedLayoutPackagePluginDependencyIssue
{
    /// <summary>
    /// 所需的插件包标识。
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// 可选的插件显示名称，来自清单、已安装的元数据或市场数据。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 所需的最低插件版本。
    /// </summary>
    public string? MinVersion { get; set; }

    /// <summary>
    /// 插件存在时已安装的插件版本。
    /// </summary>
    public string? InstalledVersion { get; set; }

    /// <summary>
    /// 用于安装/更新引导的市场标识。
    /// </summary>
    public string? MarketplaceId { get; set; }

    /// <summary>
    /// 指示插件是否已安装。
    /// </summary>
    public bool IsInstalled { get; set; }

    /// <summary>
    /// 指示已安装的插件版本是否满足 <see cref="MinVersion"/>。
    /// </summary>
    public bool IsVersionSatisfied { get; set; }

    /// <summary>
    /// 指示是否能在已配置的插件市场中找到该插件。
    /// </summary>
    public bool IsAvailableInMarket { get; set; }

    /// <summary>
    /// 指示市场查询是否无法完成。
    /// </summary>
    public bool IsMarketUnavailable { get; set; }

    /// <summary>
    /// 需要此插件的完整插件控件类型列表。
    /// </summary>
    public List<string> Controls { get; set; } = [];

    /// <summary>
    /// 包需要此插件的原因。
    /// </summary>
    public FrontedPluginDependencyReason Reason { get; set; } = FrontedPluginDependencyReason.Unknown;

    /// <summary>
    /// 需要此插件的布局窗口列表，格式为 <c>{FullWindowType}</c>。
    /// </summary>
    public List<string> RequiredBy { get; set; } = [];

    /// <summary>
    /// 受此依赖问题影响的具体控件。
    /// </summary>
    public List<FrontedLayoutPackagePluginControlIssue> AffectedControls { get; set; } = [];
}
