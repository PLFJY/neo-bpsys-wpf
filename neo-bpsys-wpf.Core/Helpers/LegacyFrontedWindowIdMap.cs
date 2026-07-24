using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 旧版前台窗口 GUID 标识映射表，仅用于仍需 GUID 身份的遗留路径（如 legacy converter、设计器回退条目）。
/// </summary>
/// <remarks>
/// <para>
/// 新代码应使用 <see cref="FrontedWindowHelper.GetFrontedWindowCanonicalId"/> 获取 v3 内置窗口的 Canonical ID（枚举名）。
/// 该类仅为保留旧版 GUID 字符串身份而存在，不应在前台窗口注册/查询路径中使用。
/// </para>
/// <para>
/// <see cref="FrontedWindowType.ScoreWindow"/> 映射到 <see cref="Guid.Empty"/> 的字符串形式，
/// 表示它是一个复合操作标识，而非真实窗口。
/// </para>
/// </remarks>
public static class LegacyFrontedWindowIdMap
{
    private static readonly Dictionary<FrontedWindowType, string> LegacyGuidDict = new()
    {
        { FrontedWindowType.BpWindow, "ACFC0F23-83F4-4607-B473-24D7DB292D23" },
        { FrontedWindowType.CutSceneWindow, "8716A6DB-3DEC-4D45-966B-ECD202DCFB0C" },
        { FrontedWindowType.ScoreWindow, Guid.Empty.ToString() },
        { FrontedWindowType.ScoreGlobalWindow, "3A4F66F7-BAC7-47AF-AC45-11657C50F7DD" },
        { FrontedWindowType.ScoreHunWindow, "EA69B342-DDA6-4394-BDFD-13368D76A6BA" },
        { FrontedWindowType.ScoreSurWindow, "4ED64F79-E47C-490D-B86A-AE396F279889" },
        { FrontedWindowType.GameDataWindow, "25378080-2085-4121-BE9A-94E987455CEC" },
        { FrontedWindowType.BpOverviewWindow, "3F6AD6CC-9271-4FFB-A98A-91771F86C27F" },
        { FrontedWindowType.MapV2Window, "9898D1EF-6E45-4968-8B18-2016389E4C3E" }
    };

    /// <summary>
    /// 获取内置前台窗口的旧版 GUID 字符串。<see cref="FrontedWindowType"/> 只表示内置窗口。
    /// </summary>
    /// <param name="windowType">内置窗口类型枚举。</param>
    /// <returns>该内置窗口对应的旧版 GUID 字符串。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="windowType"/> 不是有效的内置 <see cref="FrontedWindowType"/> 时抛出。</exception>
    /// <remarks>
    /// 该方法仅用于仍需 GUID 身份的遗留路径（如 legacy converter、设计器回退条目）。
    /// v3 内置窗口应使用 <see cref="FrontedWindowHelper.GetFrontedWindowCanonicalId"/> 获取 Canonical ID（窗口名）。
    /// </remarks>
    public static string GetLegacyGuid(FrontedWindowType windowType)
    {
        return LegacyGuidDict.TryGetValue(windowType, out var guid)
            ? guid
            : throw new ArgumentException($"{windowType} is not a valid built-in FrontedWindowType");
    }
}
