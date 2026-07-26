using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.PluginSdk;
using System.Windows;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 矩形控件。
/// </summary>
[FrontedV3Control("Rectangle", IsBuiltIn = true)]
public class RectangleFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not RectangleFrontedControlConfig rectangleConfig)
        {
            throw new FrontedLayoutConfigException("Control config is not a Rectangle config.");
        }

        var buildContext = context.ToBuildContext();
        var rectangle = new Rectangle
        {
            Name = context.ControlName,
            RadiusX = Math.Max(0, rectangleConfig.RadiusX),
            RadiusY = Math.Max(0, rectangleConfig.RadiusY)
        };
        ShapeFillBrushFactory.Apply(rectangle, rectangleConfig, buildContext);
        Content = rectangle;
    }
}
