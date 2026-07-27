namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

/// <summary>
/// 前台布局包导出请求。
/// </summary>
public sealed class FrontedLayoutPackageExportRequest
{
    /// <summary>
    /// 包标识符。
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// 要导出的已安装布局包 ID；为空时导出当前活动包。
    /// </summary>
    public string? SourcePackageId { get; set; }

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
    /// 最低支持版本。
    /// </summary>
    public string MinVersion { get; set; } = string.Empty;

    /// <summary>
    /// 导出范围。
    /// </summary>
    public FrontedLayoutPackageExportScope ExportScope { get; set; } = FrontedLayoutPackageExportScope.AllFrontendLayouts;

    /// <summary>
    /// 输出路径。
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// 可选的窗口类型名称，仅在 <see cref="ExportScope"/> 为 <see cref="FrontedLayoutPackageExportScope.CurrentWindow"/> 时使用。
    /// </summary>
    public string? WindowTypeName { get; set; }

}

/// <summary>
/// 前台布局包导出范围。
/// </summary>
public enum FrontedLayoutPackageExportScope
{
    /// <summary>
    /// 仅导出当前窗口。
    /// </summary>
    CurrentWindow,

    /// <summary>
    /// 导出所有前台布局。
    /// </summary>
    AllFrontendLayouts
}

/// <summary>
/// 前台布局包导出结果。
/// </summary>
public sealed class FrontedLayoutPackageExportResult
{
    /// <summary>
    /// 是否导出成功。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 输出路径。
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// 导出的布局数量。
    /// </summary>
    public int LayoutCount { get; set; }

    /// <summary>
    /// 导出的资源数量。
    /// </summary>
    public int ResourceCount { get; set; }

    /// <summary>
    /// 错误消息。
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
