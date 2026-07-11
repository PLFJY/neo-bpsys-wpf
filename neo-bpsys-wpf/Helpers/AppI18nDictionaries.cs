namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 主程序本地化字典名称的集中目录。
/// 仅包含实际存在的非空资源族常量，避免在 C# 代码中散布原始字符串字面量。
/// </summary>
/// <remarks>
/// 所有常量值均为代码侧使用的短字典名 <c>Locales.&lt;Family&gt;</c>。
/// XAML 的 <c>ResxLocalizationProvider.DefaultDictionary</c> 则必须使用程序集实际嵌入的
/// 完整资源基名，例如 <c>neo_bpsys_wpf.Locales.Shell</c>。
/// 调用 <see cref="I18nHelper.GetLocalizedString(string, string)"/> 时应传入此处的常量，
/// 而非硬编码字典名。新增资源族时必须在此处补充常量。
/// </remarks>
public static class AppI18nDictionaries
{
    /// <summary>
    /// 主程序程序集名称，用于 <see cref="WPFLocalizeExtension.Providers.ResxLocalizationProvider"/> 解析。
    /// </summary>
    public const string Assembly = "neo-bpsys-wpf";

    /// <summary>通用跨域资源字典（仅收录真正跨多域共用的键）。</summary>
    public const string Common = "Locales.Common";

    /// <summary>外壳/导航层资源字典。</summary>
    public const string Shell = "Locales.Shell";

    /// <summary>队伍信息相关资源字典。</summary>
    public const string Team = "Locales.Team";

    /// <summary>对局相关资源字典。</summary>
    public const string Game = "Locales.Game";

    /// <summary>BP 流程相关资源字典。</summary>
    public const string Bp = "Locales.Bp";

    /// <summary>比分相关资源字典。</summary>
    public const string Score = "Locales.Score";

    /// <summary>前台管理相关资源字典。</summary>
    public const string FrontManage = "Locales.FrontManage";

    /// <summary>前台设计器相关资源字典。</summary>
    public const string Designer = "Locales.Designer";

    /// <summary>动画编辑器相关资源字典。</summary>
    public const string AnimationEditor = "Locales.AnimationEditor";

    /// <summary>设置页相关资源字典。</summary>
    public const string Settings = "Locales.Settings";

    /// <summary>插件市场相关资源字典。</summary>
    public const string PluginMarket = "Locales.PluginMarket";

    /// <summary>Tutorial 步骤内容资源字典。</summary>
    public const string TourContent = "Locales.TourContent";

    /// <summary>
    /// 所有宿主资源族字典名的有序集合，用于在无法预先确定归属字典时进行全量查找
    /// （例如前台布局控件按配置中的 LocalizationKey 解析任意域的文本）。
    /// </summary>
    public static readonly string[] AllDictionaries =
    {
        Common, Shell, Team, Game, Bp, Score,
        FrontManage, Designer, AnimationEditor, Settings, PluginMarket,
        TourContent
    };
}
