using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 导入设计器 v3 前台布局 .bpui 包。
/// </summary>
public interface IFrontedLayoutPackageImporter
{
    /// <summary>
    /// 导入设计器 v3 包归档。
    /// </summary>
    /// <param name="request">导入请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导入结果。</returns>
    Task<FrontedLayoutPackageImportResult> ImportAsync(
        FrontedLayoutPackageImportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 导入已准备好的设计器 v3 包目录。
    /// </summary>
    /// <param name="packageDirectory">包含普通 v3 包的目录。</param>
    /// <param name="replaceExisting">是否允许替换同 ID 的已安装包。</param>
    /// <param name="activateAfterImport">是否在安装后激活该包。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导入结果。</returns>
    Task<FrontedLayoutPackageImportResult> ImportDirectoryAsync(
        string packageDirectory,
        bool replaceExisting,
        bool activateAfterImport,
        CancellationToken cancellationToken = default);
}
