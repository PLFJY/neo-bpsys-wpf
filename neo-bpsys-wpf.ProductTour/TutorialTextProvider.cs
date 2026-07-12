namespace neo_bpsys_wpf.ProductTour;

using System.Resources;

/// <summary>
/// 提供 Product Tour 控件使用的固定 UI 文本。
/// </summary>
public interface ITutorialTextProvider
{
    /// <summary>获取上一步按钮文本。</summary>
    string Previous { get; }

    /// <summary>获取下一步按钮文本。</summary>
    string Next { get; }

    /// <summary>获取完成按钮文本。</summary>
    string Finish { get; }

    /// <summary>获取跳过按钮文本。</summary>
    string Skip { get; }

    /// <summary>获取在等待预期操作完成时显示的等待文本。</summary>
    string WaitingForAction { get; }

    /// <summary>获取继续按钮文本。</summary>
    string Continue { get; }

    /// <summary>获取对话继续提示文本。</summary>
    string ClickToContinue { get; }

    /// <summary>获取首次运行欢迎标题。</summary>
    string WelcomeTitle { get; }

    /// <summary>获取首次运行欢迎描述。</summary>
    string WelcomeDescription { get; }

    /// <summary>获取语言选择器标签。</summary>
    string LanguageLabel { get; }

    /// <summary>获取开始教程按钮文本。</summary>
    string StartTour { get; }

    /// <summary>获取说明可在何处重新开始教程的提示文本。</summary>
    string RestartAvailableHint { get; }

    /// <summary>获取跳过确认标题。</summary>
    string SkipConfirmTitle { get; }

    /// <summary>获取跳过确认描述。</summary>
    string SkipConfirmDescription { get; }

    /// <summary>获取跳过确认继续按钮文本。</summary>
    string SkipConfirmContinue { get; }

    /// <summary>获取跳过确认按钮文本。</summary>
    string SkipConfirmConfirm { get; }
}

/// <summary>
/// 基于 WPFLocalizeExtension 的默认本地化文本提供器，解析 ProductTour 自身程序集的资源。
/// </summary>
public sealed class DefaultTutorialTextProvider : ITutorialTextProvider
{
    private const string AssemblyName = "neo-bpsys-wpf.ProductTour";
    private const string Dictionary = "Locales.Tour";
    private static readonly ResourceManager FallbackResourceManager = new(
        "neo_bpsys_wpf.ProductTour.Locales.Tour",
        typeof(DefaultTutorialTextProvider).Assembly);

    private static string Loc(string key)
    {
        var value = WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.GetLocalizedObject(
            AssemblyName, Dictionary, key, WPFLocalizeExtension.Engine.LocalizeDictionary.CurrentCulture);
        return value?.ToString()
            ?? FallbackResourceManager.GetString(
                key,
                WPFLocalizeExtension.Engine.LocalizeDictionary.CurrentCulture)
            ?? key;
    }

    /// <inheritdoc />
    public string Previous => Loc(nameof(Previous));

    /// <inheritdoc />
    public string Next => Loc(nameof(Next));

    /// <inheritdoc />
    public string Finish => Loc(nameof(Finish));

    /// <inheritdoc />
    public string Skip => Loc(nameof(Skip));

    /// <inheritdoc />
    public string WaitingForAction => Loc(nameof(WaitingForAction));

    /// <inheritdoc />
    public string Continue => Loc(nameof(Continue));

    /// <inheritdoc />
    public string ClickToContinue => Loc(nameof(ClickToContinue));

    /// <inheritdoc />
    public string WelcomeTitle => Loc(nameof(WelcomeTitle));

    /// <inheritdoc />
    public string WelcomeDescription => Loc(nameof(WelcomeDescription));

    /// <inheritdoc />
    public string LanguageLabel => Loc(nameof(LanguageLabel));

    /// <inheritdoc />
    public string StartTour => Loc(nameof(StartTour));

    /// <inheritdoc />
    public string RestartAvailableHint => Loc(nameof(RestartAvailableHint));

    /// <inheritdoc />
    public string SkipConfirmTitle => Loc(nameof(SkipConfirmTitle));

    /// <inheritdoc />
    public string SkipConfirmDescription => Loc(nameof(SkipConfirmDescription));

    /// <inheritdoc />
    public string SkipConfirmContinue => Loc(nameof(SkipConfirmContinue));

    /// <inheritdoc />
    public string SkipConfirmConfirm => Loc(nameof(SkipConfirmConfirm));
}
