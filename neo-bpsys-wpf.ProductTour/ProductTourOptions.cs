namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Configures Product Tour layout, animation, and display behavior.
/// </summary>
public sealed class ProductTourOptions
{
    /// <summary>Gets or sets the product tour card width.</summary>
    public double CardWidth { get; set; } = 380;

    /// <summary>Gets or sets the product tour card maximum height.</summary>
    public double CardMaxHeight { get; set; } = 280;

    /// <summary>Gets or sets the minimum card margin from owner bounds.</summary>
    public double CardMargin { get; set; } = 12;

    /// <summary>Gets or sets the gap between target and card.</summary>
    public double Gap { get; set; } = 16;

    /// <summary>Gets or sets the spotlight padding around the target.</summary>
    public double SpotlightPadding { get; set; } = 8;

    /// <summary>Gets or sets the spotlight corner radius.</summary>
    public double SpotlightCornerRadius { get; set; } = 8;

    /// <summary>Gets or sets the overlay fade-in duration.</summary>
    public TimeSpan OverlayFadeInDuration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>Gets or sets the overlay fade-out duration.</summary>
    public TimeSpan OverlayFadeOutDuration { get; set; } = TimeSpan.FromMilliseconds(220);

    /// <summary>Gets or sets the welcome overlay fade-in duration.</summary>
    public TimeSpan WelcomeFadeInDuration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>Gets or sets the welcome overlay fade-out duration.</summary>
    public TimeSpan WelcomeFadeOutDuration { get; set; } = TimeSpan.FromMilliseconds(280);

    /// <summary>Gets or sets the welcome card enter animation duration.</summary>
    public TimeSpan WelcomeCardEnterDuration { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Gets or sets the welcome card initial vertical translation.</summary>
    public double WelcomeCardInitialTranslateY { get; set; } = 16;

    /// <summary>Gets or sets the dialogue overlay fade-in duration.</summary>
    public TimeSpan DialogueFadeInDuration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>Gets or sets the dialogue overlay fade-out duration.</summary>
    public TimeSpan DialogueFadeOutDuration { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Gets or sets the dialogue box enter animation duration.</summary>
    public TimeSpan DialogueBoxEnterDuration { get; set; } = TimeSpan.FromMilliseconds(280);

    /// <summary>Gets or sets the dialogue box initial vertical translation.</summary>
    public double DialogueInitialTranslateY { get; set; } = 24;

    /// <summary>Gets or sets the dialogue typewriter interval.</summary>
    public TimeSpan TypewriterInterval { get; set; } = TimeSpan.FromMilliseconds(28);

    /// <summary>Gets or sets whether step progress text is shown.</summary>
    public bool ShowStepProgress { get; set; } = true;

    /// <summary>Gets or sets whether skip button is shown.</summary>
    public bool ShowSkipButton { get; set; } = true;

    /// <summary>Gets or sets whether arrows are shown.</summary>
    public bool ShowArrow { get; set; } = true;

    /// <summary>Gets or sets the default arrow kind.</summary>
    public ProductTourArrowKind DefaultArrowKind { get; set; } = ProductTourArrowKind.Triangle;
}
