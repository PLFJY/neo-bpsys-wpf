namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Temporary built-in tutorial package text catalog.
/// </summary>
public static class NeoBpsysTutorialTexts
{
    /// <summary>Gets the first-run opening dialogue lines.</summary>
    /// <returns>Opening dialogue lines.</returns>
    public static string[] FirstRunOpeningDialogue() =>
    [
        "欢迎来到 neo-bpsys-wpf。",
        "现在我们来进行一下简单的导播教学。"
    ];

    /// <summary>Gets the first-run ending dialogue lines.</summary>
    /// <returns>Ending dialogue lines.</returns>
    public static string[] FirstRunEndingDialogue() =>
    [
        "前台界面编辑会在首次打开 v3 编辑器时单独教学。",
        "智慧 BP 会在首次进入 SmartBP 页面时单独教学。",
        "开始你的导播之旅吧。"
    ];

    /// <summary>Gets fallback description text for a package.</summary>
    /// <param name="packageId">Package id.</param>
    /// <returns>Fallback description.</returns>
    public static string GetFallbackDescription(string packageId)
    {
        return packageId switch
        {
            TutorialPackageIds.ScoreFrontedSync => "比分会同步到比赛状态和前台显示。",
            TutorialPackageIds.FrontManageWindowsBasic => "这里可以管理前台窗口的打开、关闭和输出状态。",
            TutorialPackageIds.FrontManageLayoutPackagesBasic => "这里可以导入、导出和切换前台布局包。",
            TutorialPackageIds.DesignerV3LayoutEditBasic => "这里可以编辑 v3 前台窗口布局。",
            TutorialPackageIds.DesignerV3BehaviorEditBasic => "这里可以编辑前台窗口动画行为。",
            TutorialPackageIds.SmartBpModuleShell => "智慧 BP 是独立模块，首次进入后会提供捕获、识别区域和自动识别教程。",
            _ => "这个功能的详细教学将在你首次进入对应页面时提供。"
        };
    }
}
