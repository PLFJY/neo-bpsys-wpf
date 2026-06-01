using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 默认 v3 前台 Canvas 渲染器。
/// </summary>
public class FrontedRenderer(
    IServiceProvider services,
    ISharedDataService sharedDataService,
    IFrontedResourceResolver resourceResolver,
    IFrontedControlRegistry controlRegistry,
    ILogger<FrontedRenderer> logger) : IFrontedRenderer
{
    internal const string MissingPluginPlaceholderTitle = "Missing Plugin";

    /// <inheritdoc />
    public void RenderToCanvas(Canvas canvas, FrontedCanvasConfig config, FrontedRenderContext context)
    {
        ClearGeneratedControls(canvas);

        canvas.Width = config.CanvasWidth;
        canvas.Height = config.CanvasHeight;
        canvas.Background = CreateBackground(config.BackgroundImage);

        var buildContext = new FrontedControlBuildContext
        {
            Services = services,
            SharedDataService = context.SharedDataServiceOverride ?? sharedDataService,
            ResourceResolver = resourceResolver,
            WindowId = context.WindowId,
            CanvasName = context.CanvasName,
            Logger = logger
        };

        var renderedElements = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        foreach (var (name, controlConfig) in config.Controls.OrderBy(x => x.Value.ZIndex))
        {
            var factory = controlRegistry.GetControl(controlConfig.ControlType);
            if (factory is null)
            {
                if (FrontedPluginControlType.IsPluginControlType(controlConfig.ControlType))
                {
                    if (context.RenderMissingPluginPlaceholders)
                    {
                        var placeholder = CreateMissingPluginPlaceholder(name, controlConfig);
                        FrontedRendererProperties.SetIsGeneratedControl(placeholder, true);
                        RegisterGeneratedName(canvas, name, placeholder);
                        canvas.Children.Add(placeholder);
                        renderedElements[name] = placeholder;
                        continue;
                    }

                    logger.LogWarning(
                        "Skipping fronted plugin control {ControlName} because ControlType {ControlType} is not registered.",
                        name,
                        controlConfig.ControlType);
                    continue;
                }

                throw new FrontedLayoutConfigException(
                    $"Control '{name}' has no registered factory for ControlType '{controlConfig.ControlType}'.");
            }

            var element = factory.Create(name, controlConfig, buildContext);
            FrontedRendererProperties.SetIsGeneratedControl(element, true);
            RegisterGeneratedName(canvas, name, element);
            canvas.Children.Add(element);
            renderedElements[name] = element;
        }

        SyncLinkedPickingBorderOverlays(config, renderedElements);
    }

    private static FrameworkElement CreateMissingPluginPlaceholder(string name, FrontedControlConfigBase config)
    {
        FrontedPluginControlType.TryParse(config.ControlType, out var parsed);
        var border = new Border
        {
            Name = name,
            Width = config.Width ?? FrontedDesignerGeometryHelper.MinHitWidth,
            Height = config.Height ?? FrontedDesignerGeometryHelper.MinHitHeight,
            BorderBrush = Brushes.OrangeRed,
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(96, 60, 20, 20)),
            Child = new TextBlock
            {
                Text = $"{MissingPluginPlaceholderTitle}\n{parsed.PackageId}\n{parsed.ControlTypeName}\n{config.ControlType}",
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };

        Canvas.SetLeft(border, config.Left);
        Canvas.SetTop(border, config.Top);
        Panel.SetZIndex(border, config.ZIndex);
        return border;
    }

    private static void SyncLinkedPickingBorderOverlays(
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, FrameworkElement> renderedElements)
    {
        foreach (var (overlayName, controlConfig) in config.Controls)
        {
            if (controlConfig is not PickingBorderOverlayControlConfig overlayConfig
                || string.IsNullOrWhiteSpace(overlayConfig.TargetControlName)
                || !config.Controls.TryGetValue(overlayConfig.TargetControlName, out var targetConfig)
                || !renderedElements.TryGetValue(overlayConfig.TargetControlName, out var target)
                || !renderedElements.TryGetValue(overlayName, out var overlay))
            {
                continue;
            }

            Canvas.SetLeft(overlay, Canvas.GetLeft(target));
            Canvas.SetTop(overlay, Canvas.GetTop(target));

            var width = ResolveRenderedSize(target.Width, target.ActualWidth, targetConfig.Width);
            if (width.HasValue)
            {
                overlay.Width = width.Value;
            }

            var height = ResolveRenderedSize(target.Height, target.ActualHeight, targetConfig.Height);
            if (height.HasValue)
            {
                overlay.Height = height.Value;
            }
        }
    }

    private static double? ResolveRenderedSize(double explicitSize, double actualSize, double? fallbackSize)
    {
        if (!double.IsNaN(explicitSize) && !double.IsInfinity(explicitSize) && explicitSize > 0D)
        {
            return explicitSize;
        }

        if (!double.IsNaN(actualSize) && !double.IsInfinity(actualSize) && actualSize > 0D)
        {
            return actualSize;
        }

        return fallbackSize;
    }

    private static void ClearGeneratedControls(Canvas canvas)
    {
        for (var i = canvas.Children.Count - 1; i >= 0; i--)
        {
            if (canvas.Children[i] is DependencyObject dependencyObject
                && FrontedRendererProperties.GetIsGeneratedControl(dependencyObject))
            {
                UnregisterGeneratedName(canvas, dependencyObject);
                canvas.Children.RemoveAt(i);
            }
        }
    }

    private static void RegisterGeneratedName(Canvas canvas, string name, FrameworkElement element)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var nameScopeOwner = GetNameScopeOwner(canvas);
        EnsureNameScope(nameScopeOwner);
        TryUnregisterName(nameScopeOwner, name);
        nameScopeOwner.RegisterName(name, element);
        FrontedRendererProperties.SetRegisteredName(element, name);
    }

    private static void UnregisterGeneratedName(Canvas canvas, DependencyObject element)
    {
        var name = FrontedRendererProperties.GetRegisteredName(element);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        TryUnregisterName(GetNameScopeOwner(canvas), name);
        FrontedRendererProperties.SetRegisteredName(element, string.Empty);
    }

    private static FrameworkElement GetNameScopeOwner(Canvas canvas)
    {
        return (FrameworkElement?)Window.GetWindow(canvas) ?? canvas;
    }

    private static void EnsureNameScope(FrameworkElement element)
    {
        if (NameScope.GetNameScope(element) is null)
        {
            NameScope.SetNameScope(element, new NameScope());
        }
    }

    private static void TryUnregisterName(FrameworkElement element, string name)
    {
        try
        {
            element.UnregisterName(name);
        }
        catch (ArgumentException)
        {
            // Name was not registered in this namescope.
        }
    }

    private ImageBrush? CreateBackground(string? backgroundImage)
    {
        var imageSource = resourceResolver.ResolveImage(backgroundImage, FrontedImagePurpose.Background);
        return imageSource is null
            ? null
            : new ImageBrush(imageSource) { Stretch = Stretch.Fill };
    }
}
