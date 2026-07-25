using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.PluginSdk;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 内置 v3 背景色调矩形控件。
/// </summary>
[FrontedV3Control("BackgroundTintRectangle", IsBuiltIn = true)]
public class BackgroundTintRectangleFrontedControl : FrontedV3ControlBase
{
    /// <inheritdoc />
    protected override void OnInitializeFrontedV3(FrontedV3ControlContext context)
    {
        if (context.Config is not BackgroundTintRectangleFrontedControlConfig rectangle)
        {
            throw new FrontedLayoutConfigException("Control config is not a BackgroundTintRectangle config.");
        }

        var buildContext = context.ToBuildContext();
        var processor = context.Services.GetRequiredService<BackgroundImageTintProcessor>();
        var root = BackgroundTintFrontedControlFactoryHelper.Create(
            context.ControlName ?? string.Empty,
            rectangle,
            buildContext,
            processor,
            element => new RectangleGeometry(
                new Rect(
                    0,
                    0,
                    BackgroundTintFrontedControlFactoryHelper.GetWidth(element, rectangle),
                    BackgroundTintFrontedControlFactoryHelper.GetHeight(element, rectangle)),
                Math.Max(0, rectangle.RadiusX),
                Math.Max(0, rectangle.RadiusY)),
            BackgroundTintNormalizationMode.VisibleRectangle);
        Content = root;
    }
}
