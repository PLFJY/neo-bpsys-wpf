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
        { FrontedWindowType.WidgetsWindow, "712D2E21-B8DF-4220-8E3D-8AD0003DD079" }
    };

    /// <summary>
    /// 获取内置前台窗口 GUID。FrontedWindowType 只表示内置窗口。
    /// </summary>
    public static string GetFrontedWindowGuid(FrontedWindowType windowType)
    {
        return FrontedWindowGuidDict.TryGetValue(windowType, out var guid)
            ? guid
            : throw new ArgumentException($"{windowType} is not a valid built-in FrontedWindowType");
    }
}
