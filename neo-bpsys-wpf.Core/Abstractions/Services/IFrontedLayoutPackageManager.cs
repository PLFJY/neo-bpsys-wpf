using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

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
    /// 读取当前活动包中的用户自定义 v3 窗口注册。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前活动包的自定义窗口集合。</returns>
    Task<IReadOnlyList<FrontedCustomV3LayoutWindowRegistration>> GetActiveCustomWindowsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在当前活动包中创建用户自定义 v3 窗口。
    /// </summary>
    /// <param name="request">创建请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新创建的窗口注册。</returns>
    Task<FrontedCustomV3LayoutWindowRegistration> CreateCustomWindowAsync(
        FrontedCustomWindowCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除当前活动包中的用户自定义 v3 窗口及其布局、行为文件。
    /// </summary>
    /// <param name="canonicalWindowId">要删除的 custom Canonical ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteCustomWindowAsync(
        string canonicalWindowId,
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
    /// 更新指定布局包的显示名称。
    /// </summary>
    /// <param name="packageId">要改名的包 ID。</param>
    /// <param name="name">新的显示名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>改名后的布局包信息。</returns>
    Task<FrontedLayoutPackageInfo> RenamePackageAsync(
        string packageId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新指定布局包的描述。
    /// </summary>
    /// <param name="packageId">要更新描述的包 ID。</param>
    /// <param name="description">新的包描述，可以为空。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>更新后的布局包信息。</returns>
    Task<FrontedLayoutPackageInfo> UpdatePackageDescriptionAsync(
        string packageId,
        string description,
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
