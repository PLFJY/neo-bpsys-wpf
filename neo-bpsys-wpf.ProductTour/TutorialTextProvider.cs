namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Provides fixed UI text used by Product Tour controls.
/// </summary>
public interface ITutorialTextProvider
{
    /// <summary>Gets the previous step button text.</summary>
    string Previous { get; }

    /// <summary>Gets the next step button text.</summary>
    string Next { get; }

    /// <summary>Gets the finish button text.</summary>
    string Finish { get; }

    /// <summary>Gets the skip button text.</summary>
    string Skip { get; }

    /// <summary>Gets the waiting text shown while an expected action is pending.</summary>
    string WaitingForAction { get; }

    /// <summary>Gets the continue button text.</summary>
    string Continue { get; }

    /// <summary>Gets the dialogue continue hint text.</summary>
    string ClickToContinue { get; }

    /// <summary>Gets the first-run welcome title.</summary>
    string WelcomeTitle { get; }

    /// <summary>Gets the first-run welcome description.</summary>
    string WelcomeDescription { get; }

    /// <summary>Gets the language selector label.</summary>
    string LanguageLabel { get; }

    /// <summary>Gets the start tour button text.</summary>
    string StartTour { get; }

    /// <summary>Gets the hint explaining where the tour can be restarted.</summary>
    string RestartAvailableHint { get; }

    /// <summary>Gets the skip confirmation title.</summary>
    string SkipConfirmTitle { get; }

    /// <summary>Gets the skip confirmation description.</summary>
    string SkipConfirmDescription { get; }

    /// <summary>Gets the skip confirmation continue button text.</summary>
    string SkipConfirmContinue { get; }

    /// <summary>Gets the skip confirmation confirm button text.</summary>
    string SkipConfirmConfirm { get; }
}

/// <summary>
/// 基于 WPFLocalizeExtension 的默认本地化文本提供器，解析 ProductTour 自身程序集的资源。
/// </summary>
public sealed class DefaultTutorialTextProvider : ITutorialTextProvider
{
    private const string AssemblyName = "neo-bpsys-wpf.ProductTour";
    private const string Dictionary = "Locales.Tour";

    private static string Loc(string key)
    {
        var value = WPFLocalizeExtension.Engine.LocalizeDictionary.Instance.GetLocalizedObject(
            AssemblyName, Dictionary, key, WPFLocalizeExtension.Engine.LocalizeDictionary.CurrentCulture);
        return value?.ToString() ?? key;
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
