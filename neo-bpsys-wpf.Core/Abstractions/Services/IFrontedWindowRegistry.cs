using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 为服务和设计器 v3 提供前台窗口注册查询。
/// </summary>
/// <remarks>
/// 该接口只消费强类型 <see cref="FrontedWindowRegistration"/> 集合，
/// 维护唯一的 Canonical ID 索引。
/// </remarks>
public interface IFrontedWindowRegistry
{
    /// <summary>
    /// 获取所有已注册的前台窗口。
    /// </summary>
    /// <returns>前台窗口注册列表。</returns>
    IReadOnlyList<FrontedWindowRegistration> GetWindows();

    /// <summary>
    /// 获取在前台管理页可见的窗口，使用稳定的回退分组和排序。
    /// </summary>
    /// <returns>可管理的窗口注册列表。</returns>
    IReadOnlyList<FrontedWindowRegistration> GetManageableWindows();

    /// <summary>
    /// 获取所有 v3 Layout host 前台窗口注册。
    /// </summary>
    /// <returns>v3 Layout 窗口注册列表。</returns>
    IReadOnlyList<FrontedV3LayoutWindowRegistration> GetV3LayoutWindows();

    /// <summary>
    /// 按 Canonical ID 查找窗口注册。
    /// </summary>
    /// <param name="canonicalId">窗口的 Canonical ID。</param>
    /// <param name="registration">匹配的注册（若找到）。</param>
    /// <returns>是否找到匹配的注册。</returns>
    bool TryGet(string canonicalId, out FrontedWindowRegistration registration);
}
