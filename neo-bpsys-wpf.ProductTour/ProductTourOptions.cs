using System.Windows;

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

    /// <summary>Gets or sets the default mask opacity.</summary>
    public double MaskOpacity { get; set; } = 0.86;

    /// <summary>Gets or sets the welcome overlay mask opacity.</summary>
    public double WelcomeMaskOpacity { get; set; } = 0.90;

    /// <summary>Gets or sets the dialogue overlay mask opacity.</summary>
    public double DialogueMaskOpacity { get; set; } = 0.82;

    /// <summary>Gets or sets the product tour overlay mask opacity.</summary>
    public double ProductTourMaskOpacity { get; set; } = 0.84;

    /// <summary>Gets or sets the dialogue box maximum width.</summary>
    public double DialogueBoxMaxWidth { get; set; } = 760;

    /// <summary>Gets or sets the minimum opacity expected for the dialogue box surface.</summary>
    public double DialogueBoxMinOpacity { get; set; } = 0.94;

    /// <summary>Gets or sets the dialogue box margin.</summary>
    public Thickness DialogueBoxMargin { get; set; } = new(48);

    /// <summary>Gets or sets whether guide avatars are shown when available.</summary>
    public bool ShowAvatar { get; set; } = true;

    /// <summary>Gets or sets the welcome guide avatar width.</summary>
    public double WelcomeAvatarWidth { get; set; } = 220;

    /// <summary>Gets or sets the dialogue guide avatar width.</summary>
    public double DialogueAvatarWidth { get; set; } = 260;

    /// <summary>Gets or sets the product tour guide avatar width.</summary>
    public double ProductTourAvatarWidth { get; set; } = 96;

    /// <summary>Gets or sets the guide avatar margin.</summary>
    public Thickness AvatarMargin { get; set; } = new(16);
}
