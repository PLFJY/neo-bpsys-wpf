namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

public abstract class BackgroundTintFrontedControlConfigBase : FrontedControlConfigBase
{
    public string? TintColor { get; set; } = "#FFFFFFFF";

    public string? TintBindingPath { get; set; }

    public BackgroundTintMode TintMode { get; set; } = BackgroundTintMode.LuminanceColorize;

    public double TintStrength { get; set; } = 1D;

    public bool ShowMissingBackgroundPlaceholder { get; set; } = true;
}
