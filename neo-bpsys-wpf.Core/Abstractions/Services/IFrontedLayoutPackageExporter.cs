using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 将设计器 v3 前台布局导出为 .bpui 包。
/// </summary>
public interface IFrontedLayoutPackageExporter
{
    /// <summary>
    /// 将前台布局导出为 .bpui 包。
    /// </summary>
    /// <param name="request">导出请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导出结果。</returns>
    Task<FrontedLayoutPackageExportResult> ExportAsync(
        FrontedLayoutPackageExportRequest request,
        CancellationToken cancellationToken = default);
}
