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
/// Default Chinese fixed UI text provider for Product Tour controls.
/// </summary>
public sealed class DefaultTutorialTextProvider : ITutorialTextProvider
{
    /// <inheritdoc />
    public string Previous => "上一步";

    /// <inheritdoc />
    public string Next => "下一步";

    /// <inheritdoc />
    public string Finish => "完成";

    /// <inheritdoc />
    public string Skip => "跳过";

    /// <inheritdoc />
    public string WaitingForAction => "等待操作...";

    /// <inheritdoc />
    public string Continue => "继续";

    /// <inheritdoc />
    public string ClickToContinue => "点击继续";

    /// <inheritdoc />
    public string WelcomeTitle => "欢迎使用 neo-bpsys-wpf！";

    /// <inheritdoc />
    public string WelcomeDescription => "在开始之前，请先完成一次简短的功能导览。";

    /// <inheritdoc />
    public string LanguageLabel => "界面语言";

    /// <inheritdoc />
    public string StartTour => "开始导览";

    /// <inheritdoc />
    public string RestartAvailableHint => "之后可以在设置中重新启动导览。";

    /// <inheritdoc />
    public string SkipConfirmTitle => "跳过首次导览？";

    /// <inheritdoc />
    public string SkipConfirmDescription => "确定要跳过首次导览吗？之后可以在设置中重新启动。";

    /// <inheritdoc />
    public string SkipConfirmContinue => "继续导览";

    /// <inheritdoc />
    public string SkipConfirmConfirm => "确认跳过";
}
