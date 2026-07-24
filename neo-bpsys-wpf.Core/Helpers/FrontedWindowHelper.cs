using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 前台窗口辅助类。
/// </summary>
public static class FrontedWindowHelper
{
    private static readonly Dictionary<FrontedWindowType, string> FrontedWindowGuidDict = new()
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
    /// 获取内置前台窗口 GUID。FrontedWindowType 只表示内置窗口。
    /// </summary>
    /// <remarks>
    /// 该方法仅用于仍需 GUID 身份的 XAML 内置窗口。
    /// v3 内置窗口应使用 <see cref="GetFrontedWindowCanonicalId"/> 获取 Canonical ID（窗口名）。
    /// </remarks>
    public static string GetFrontedWindowGuid(FrontedWindowType windowType)
    {
        return FrontedWindowGuidDict.TryGetValue(windowType, out var guid)
            ? guid
            : throw new ArgumentException($"{windowType} is not a valid built-in FrontedWindowType");
    }

    /// <summary>
    /// 获取内置前台窗口的 Canonical ID。
    /// </summary>
    /// <param name="windowType">内置窗口类型枚举。</param>
    /// <returns>v3 内置窗口返回枚举名（例如 <c>BpWindow</c>）；
    /// <see cref="FrontedWindowType.ScoreWindow"/> 返回 <see cref="Guid.Empty"/> 的字符串形式（复合操作，非真实窗口）。</returns>
    /// <remarks>
    /// v3 内置窗口的 Canonical ID 直接使用枚举名。
    /// </remarks>
    public static string GetFrontedWindowCanonicalId(FrontedWindowType windowType)
    {
        return windowType == FrontedWindowType.ScoreWindow
            ? Guid.Empty.ToString()
            : windowType.ToString();
    }
}
