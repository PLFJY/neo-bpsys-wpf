using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal static class BackgroundTintFrontedControlFactoryHelper
{
    public static FrameworkElement Create(
        string name,
        BackgroundTintFrontedControlConfigBase config,
        FrontedControlBuildContext context,
        BackgroundImageTintProcessor processor,
        Func<BackgroundTintControlHost, Geometry> createClip)
    {
        var source = context.ResourceResolver.ResolveImage(
            context.CanvasBackgroundImage,
            FrontedImagePurpose.Background);
        if (source is null)
        {
            return CreateMissingBackgroundRoot(name, config, context);
        }

        var root = new BackgroundTintControlHost(
            processor,
            source,
            context.CanvasBackgroundImage,
            config.TintMode,
            config.TintStrength,
            context.CanvasWidth,
            context.CanvasHeight,
            config.Left,
            config.Top,
            context.Logger)
        {
            Name = name
        };
        FrontedControlFactoryHelper.ApplyCanvasLayout(root, config);

        void RefreshClip()
        {
            var geometry = createClip(root);
            if (geometry.CanFreeze)
            {
                geometry.Freeze();
            }

            root.Clip = geometry;
        }

        root.SizeChanged += (_, _) => RefreshClip();
        RefreshClip();

        if (!string.IsNullOrWhiteSpace(config.TintBindingPath))
        {
            BindingOperations.SetBinding(
                root,
                BackgroundTintControlHost.TintColorValueProperty,
                new Binding(config.TintBindingPath)
                {
                    Source = context.SharedDataService,
                    Mode = BindingMode.OneWay,
                    FallbackValue = ColorHelper.DefaultColorHex,
                    TargetNullValue = ColorHelper.DefaultColorHex
                });
        }
        else
        {
            root.TintColorValue = config.TintColor;
        }

        root.RefreshTint();
        return root;
    }

    private static FrameworkElement CreateMissingBackgroundRoot(
        string name,
        BackgroundTintFrontedControlConfigBase config,
        FrontedControlBuildContext context)
    {
        var root = new Grid { Name = name };
        FrontedControlFactoryHelper.ApplyCanvasLayout(root, config);
        if (!context.IsDesignerPreview || !config.ShowMissingBackgroundPlaceholder)
        {
            return root;
        }

        var localization = context.Services.GetService<IFrontedDesignerLocalizationService>();
        var text = localization?.GetDesignerText(
            "Designer.Validation.MissingCanvasBackgroundImage",
            "Missing Canvas background image")
            ?? "Missing Canvas background image";
        root.Children.Add(new Border
        {
            BorderBrush = Brushes.OrangeRed,
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(72, 255, 69, 0)),
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(4)
            }
        });
        return root;
    }

    public static double GetWidth(FrameworkElement element, FrontedControlConfigBase config) =>
        element.ActualWidth > 0
            ? element.ActualWidth
            : config.Width is > 0 && double.IsFinite(config.Width.Value) ? config.Width.Value : 1D;

    public static double GetHeight(FrameworkElement element, FrontedControlConfigBase config) =>
        element.ActualHeight > 0
            ? element.ActualHeight
            : config.Height is > 0 && double.IsFinite(config.Height.Value) ? config.Height.Value : 1D;
}
