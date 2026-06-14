namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// 前台布局包的元数据信息。
/// </summary>
public sealed class FrontedLayoutPackageInfo
{
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
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// 最低支持版本。
    /// </summary>
    public string MinVersion { get; set; } = string.Empty;

    /// <summary>
    /// 安装路径。
    /// </summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>
    /// 包来源。
    /// </summary>
    public FrontedLayoutPackageSource Source { get; set; }

    /// <summary>
    /// 是否为内置包。
    /// </summary>
    public bool IsBuiltin { get; set; }

    /// <summary>
    /// 是否为本地包。
    /// </summary>
    public bool IsLocal { get; set; }

    /// <summary>
    /// 是否为当前激活的包。
    /// </summary>
    public bool IsActivePackage { get; set; }

    /// <summary>
    /// 布局数量。
    /// </summary>
    public int LayoutCount { get; set; }

    /// <summary>
    /// 资源数量。
    /// </summary>
    public int ResourceCount { get; set; }

    /// <summary>
    /// 验证状态。
    /// </summary>
    public FrontedLayoutPackageValidationStatus ValidationStatus { get; set; }

    /// <summary>
    /// 验证消息。
    /// </summary>
    public string ValidationMessage { get; set; } = string.Empty;
}
