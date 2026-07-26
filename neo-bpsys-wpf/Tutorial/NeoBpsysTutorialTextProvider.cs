using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// 提供 neo-bpsys-wpf 固定的教程 UI 文本。
/// </summary>
public sealed class NeoBpsysTutorialTextProvider : ITutorialTextProvider
{
    private readonly DefaultTutorialTextProvider _fallback = new();

    /// <inheritdoc />
    public string Previous => _fallback.Previous;

    /// <inheritdoc />
    public string Next => _fallback.Next;

    /// <inheritdoc />
    public string Finish => _fallback.Finish;

    /// <inheritdoc />
    public string Skip => _fallback.Skip;

    /// <inheritdoc />
    public string WaitingForAction => _fallback.WaitingForAction;

    /// <inheritdoc />
    public string Continue => _fallback.Continue;

    /// <inheritdoc />
    public string ClickToContinue => _fallback.ClickToContinue;

    /// <inheritdoc />
    public string WelcomeTitle => _fallback.WelcomeTitle;

    /// <inheritdoc />
    public string WelcomeDescription => _fallback.WelcomeDescription;

    /// <inheritdoc />
    public string LanguageLabel => _fallback.LanguageLabel;

    /// <inheritdoc />
    public string StartTour => _fallback.StartTour;

    /// <inheritdoc />
    public string RestartAvailableHint => _fallback.RestartAvailableHint;

    /// <inheritdoc />
    public string SkipConfirmTitle => _fallback.SkipConfirmTitle;

    /// <inheritdoc />
    public string SkipConfirmDescription => _fallback.SkipConfirmDescription;

    /// <inheritdoc />
    public string SkipConfirmContinue => _fallback.SkipConfirmContinue;

    /// <inheritdoc />
    public string SkipConfirmConfirm => _fallback.SkipConfirmConfirm;

    /// <inheritdoc />
    public string SkipForCurrentSession => _fallback.SkipForCurrentSession;

    /// <inheritdoc />
    public string SkipPermanently => _fallback.SkipPermanently;

    /// <inheritdoc />
    public string SuppressUntilNextStartup => _fallback.SuppressUntilNextStartup;

    /// <inheritdoc />
    public string FirstRunSkipConfirmDescription => _fallback.FirstRunSkipConfirmDescription;

    /// <inheritdoc />
    public string SequenceSkipConfirmDescription => _fallback.SequenceSkipConfirmDescription;
}
