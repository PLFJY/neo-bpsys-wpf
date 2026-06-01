using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.ExampleFrontedControls;

public sealed class TeamCardFrontedControlConfig : FrontedControlConfigBase
{
    public TeamCardFrontedControlConfig()
    {
        ControlType = TeamCardFrontedControlContributor.FullControlType;
        Width = 260;
        Height = 96;
    }

    public string? TeamNameBindingPath { get; set; } = "CurrentGame.SurTeam.Name";

    public string? LogoBindingPath { get; set; } = "CurrentGame.SurTeam.Logo";

    public string BackgroundColor { get; set; } = "#AA000000";

    public string ForegroundColor { get; set; } = "#FFFFFFFF";

    public double CornerRadius { get; set; } = 12;

    public double LogoSize { get; set; } = 64;

    public double FontSize { get; set; } = 24;

    public string FontWeight { get; set; } = "Bold";
}
