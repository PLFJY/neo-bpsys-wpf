namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

public class BackgroundTintRectangleFrontedControlConfig : BackgroundTintFrontedControlConfigBase
{
    public BackgroundTintRectangleFrontedControlConfig()
    {
        ControlType = "BackgroundTintRectangle";
    }

    public double RadiusX { get; set; }

    public double RadiusY { get; set; }
}
