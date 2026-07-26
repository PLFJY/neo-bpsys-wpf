using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 前台布局包管理器接口，负责布局包的列表、激活、复制、删除等操作。
/// </summary>
public interface IFrontedLayoutPackageManager
{
    /// <summary>
    /// 列出所有可用的布局包。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>布局包信息列表。</returns>
    Task<IReadOnlyList<FrontedLayoutPackageInfo>> ListPackagesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前激活的布局包状态。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前激活包状态。</returns>
    Task<FrontedLayoutActivePackageState> GetActivePackageStateAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 激活指定的布局包。
    /// </summary>
    /// <param name="packageId">要激活的包 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ActivatePackageAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 确保当前激活的包可写，必要时创建副本。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可写的布局包信息。</returns>
    Task<FrontedLayoutPackageInfo> EnsureWritableActivePackageAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 复制指定的布局包。
    /// </summary>
    /// <param name="sourcePackageId">源包 ID。</param>
    /// <param name="requestedName">请求的新包名称（可选）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>复制后的布局包信息。</returns>
    Task<FrontedLayoutPackageInfo> DuplicatePackageAsync(
        string sourcePackageId,
        string? requestedName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定的布局包。
    /// </summary>
    /// <param name="packageId">要删除的包 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeletePackageAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定包的布局存储根目录。
    /// </summary>
    /// <param name="packageId">包 ID。</param>
    /// <returns>布局存储根目录路径。</returns>
    string GetPackageLayoutsRootFolder(string packageId);

    /// <summary>
    /// 获取指定包中窗口的布局文件路径。
    /// </summary>
    /// <param name="packageId">包 ID。</param>
    /// <param name="fullWindowType">完整窗口类型名。</param>
    /// <returns>布局文件路径。</returns>
    string GetPackageLayoutPath(string packageId, string fullWindowType);

    /// <summary>
    /// 获取包管理器的根目录。
    /// </summary>
    /// <returns>包管理器根目录路径。</returns>
    string GetPackageRootFolder();
}
