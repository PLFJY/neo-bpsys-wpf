using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public class RectangleFrontedControl : IFrontedControl
{
    public string ControlType => "Rectangle";

    public Type ConfigType => typeof(RectangleFrontedControlConfig);

    public FrameworkElement Create(
        string name,
        FrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        if (config is not RectangleFrontedControlConfig rectangleConfig)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a Rectangle config.");
        }

        var rectangle = new Rectangle
        {
            Name = name,
            RadiusX = Math.Max(0, rectangleConfig.RadiusX),
            RadiusY = Math.Max(0, rectangleConfig.RadiusY)
        };
        FrontedControlFactoryHelper.ApplyCanvasLayout(rectangle, rectangleConfig);
        ShapeFillBrushFactory.Apply(rectangle, rectangleConfig, context);
        return rectangle;
    }
}
