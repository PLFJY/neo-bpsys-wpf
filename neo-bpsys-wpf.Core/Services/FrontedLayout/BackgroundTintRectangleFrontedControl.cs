using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public class BackgroundTintRectangleFrontedControl(BackgroundImageTintProcessor processor) : IFrontedControl
{
    public BackgroundTintRectangleFrontedControl()
        : this(new BackgroundImageTintProcessor())
    {
    }

    public string ControlType => "BackgroundTintRectangle";

    public Type ConfigType => typeof(BackgroundTintRectangleFrontedControlConfig);

    public FrameworkElement Create(string name, FrontedControlConfigBase config, FrontedControlBuildContext context)
    {
        if (config is not BackgroundTintRectangleFrontedControlConfig rectangle)
        {
            throw new FrontedLayoutConfigException($"Control '{name}' config is not a BackgroundTintRectangle config.");
        }

        return BackgroundTintFrontedControlFactoryHelper.Create(
            name,
            rectangle,
            context,
            processor,
            root => new RectangleGeometry(
                new Rect(
                    0,
                    0,
                    BackgroundTintFrontedControlFactoryHelper.GetWidth(root, rectangle),
                    BackgroundTintFrontedControlFactoryHelper.GetHeight(root, rectangle)),
                Math.Max(0, rectangle.RadiusX),
                Math.Max(0, rectangle.RadiusY)),
            BackgroundTintNormalizationMode.VisibleRectangle);
    }
}
