namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

public class RectangleFrontedControlConfig : ShapeFrontedControlConfigBase
{
    public RectangleFrontedControlConfig()
    {
        ControlType = "Rectangle";
    }

    public double RadiusX { get; set; }

    public double RadiusY { get; set; }
}
