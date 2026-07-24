using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 前台窗口辅助类。
/// </summary>
/// <remarks>
/// 旧版 GUID 标识映射已迁移到 <see cref="LegacyFrontedWindowIdMap"/>，
/// 仅用于遗留路径。新代码应使用 <see cref="GetFrontedWindowCanonicalId"/> 获取 v3 内置窗口的 Canonical ID。
/// </remarks>
public static class FrontedWindowHelper
{
    /// <summary>
    /// 获取内置前台窗口的 Canonical ID。
    /// </summary>
    /// <param name="windowType">内置窗口类型枚举。</param>
    /// <returns>v3 内置窗口返回枚举名（例如 <c>BpWindow</c>）；
    /// <see cref="FrontedWindowType.ScoreWindow"/> 返回 <see cref="Guid.Empty"/> 的字符串形式（复合操作，非真实窗口）。</returns>
    /// <remarks>
    /// v3 内置窗口的 Canonical ID 直接使用枚举名。
    /// <see cref="FrontedWindowType.ScoreWindow"/> 是复合操作标识，前台窗口服务
    /// 在 Show/Hide 入口对 <see cref="FrontedWindowType.ScoreWindow"/> 做组合分派，不会用此返回值查找注册。
    /// </remarks>
    public static string GetFrontedWindowCanonicalId(FrontedWindowType windowType)
    {
        return windowType == FrontedWindowType.ScoreWindow
            ? Guid.Empty.ToString()
            : windowType.ToString();
    }
}
