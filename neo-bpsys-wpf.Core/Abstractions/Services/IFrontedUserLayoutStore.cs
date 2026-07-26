using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 在应用数据目录下存储设计器 v3 用户布局文件。
/// </summary>
public interface IFrontedUserLayoutStore
{
    /// <summary>
    /// 返回用户窗口布局是否存在。
    /// </summary>
    /// <param name="windowTypeName">完整的窗口类型名。</param>
    /// <returns>当用户布局存在时返回 <see langword="true"/>。</returns>
    bool Exists(string windowTypeName);

    /// <summary>
    /// 加载用户窗口布局。
    /// </summary>
    /// <param name="windowTypeName">完整的窗口类型名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>加载的配置，文件不存在时返回 <see langword="null"/>。</returns>
    Task<FrontedWindowConfig?> LoadAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存用户窗口布局。
    /// </summary>
    /// <param name="windowTypeName">完整的窗口类型名。</param>
    /// <param name="config">要保存的配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SaveAsync(
        string windowTypeName,
        FrontedWindowConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除用户窗口布局。
    /// </summary>
    /// <param name="windowTypeName">完整的窗口类型名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteAsync(
        string windowTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户窗口布局路径。
    /// </summary>
    /// <param name="windowTypeName">完整的窗口类型名。</param>
    /// <returns>布局 JSON 路径。</returns>
    string GetLayoutPath(string windowTypeName);

    /// <summary>
    /// 获取用户布局根目录。
    /// </summary>
    /// <returns>用户布局根目录。</returns>
    string GetRootFolder();
}
