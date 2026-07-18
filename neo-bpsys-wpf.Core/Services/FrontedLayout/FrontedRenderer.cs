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
    public void RenderToCanvas(Canvas canvas, FrontedWindowConfig config, FrontedRenderContext context)
    {
        RenderToCanvas(canvas, config.CanvasSettings, config.ControlLayout, context);
    }

    /// <inheritdoc />
    public void RenderToCanvas(
        Canvas canvas,
        FrontedCanvasSettings canvasSettings,
        FrontedControlLayout controlLayout,
        FrontedRenderContext context)
    {
        RenderToCanvas(canvas, new FrontedCanvasConfig
        {
            Version = 3,
            CanvasWidth = canvasSettings.CanvasWidth,
            CanvasHeight = canvasSettings.CanvasHeight,
            BackgroundImage = canvasSettings.BackgroundImage,
            EnableBoModeStates = canvasSettings.EnableBoModeStates,
            BoModeStates = canvasSettings.BoModeStates,
            RequiredPlugins = controlLayout.RequiredPlugins,
            Controls = controlLayout.Controls
        }, context);
    }

    /// <inheritdoc />
    public void RenderToCanvas(Canvas canvas, FrontedCanvasConfig config, FrontedRenderContext context)
    {
        ClearGeneratedControls(canvas);

        var runtimeState = FrontedCanvasRuntimeStateResolver.Resolve(
            config,
            context.SharedDataServiceOverride ?? sharedDataService,
            logger);

        canvas.Width = runtimeState.CanvasWidth;
        canvas.Height = runtimeState.CanvasHeight;
        canvas.Background = CreateBackground(runtimeState.BackgroundImage);

        var buildContext = new FrontedControlBuildContext
        {
            Services = services,
            SharedDataService = context.SharedDataServiceOverride ?? sharedDataService,
            ResourceResolver = resourceResolver,
            WindowId = context.WindowId,
            CanvasName = context.CanvasName,
            CanvasBackgroundImage = runtimeState.BackgroundImage,
            CanvasWidth = runtimeState.CanvasWidth,
            CanvasHeight = runtimeState.CanvasHeight,
            IsDesignerPreview = context.IsDesignerPreview,
            Logger = logger
        };

        var renderedElements = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        foreach (var (name, controlConfig) in runtimeState.Controls.OrderBy(x => x.Value.ZIndex))
        {
            var factory = controlRegistry.GetControl(controlConfig.ControlType);
            if (factory is null)
            {
                if (FrontedPluginControlType.IsPluginControlType(controlConfig.ControlType))
                {
                    if (context.RenderMissingPluginPlaceholders)
                    {
                        // Designer preview preserves missing plugin controls as selectable placeholders; live fronted windows skip them.
                        var placeholder = CreateMissingPluginPlaceholder(name, controlConfig);
                        placeholder.Visibility = MapVisibility(controlConfig.Visibility);
                        FrontedRendererProperties.SetIsGeneratedControl(placeholder, true);
                        FrontedRendererProperties.SetBehaviorGuid(placeholder, controlConfig.BehaviorGuid);
                        RegisterGeneratedName(canvas, name, placeholder);
                        var placeholderHost = FrontedEffectHostFactory.Wrap(placeholder);
                        ApplyStaticGaussianBlur(placeholderHost, controlConfig.IsGaussianBlurEnabled, controlConfig.GaussianBlurRadius);
                        canvas.Children.Add(placeholderHost);
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
            element.Visibility = MapVisibility(controlConfig.Visibility);
            FrontedRendererProperties.SetIsGeneratedControl(element, true);
            FrontedRendererProperties.SetBehaviorGuid(element, controlConfig.BehaviorGuid);
            RegisterGeneratedName(canvas, name, element);
            var effectHost = FrontedEffectHostFactory.Wrap(element);
            ApplyStaticGaussianBlur(effectHost, controlConfig.IsGaussianBlurEnabled, controlConfig.GaussianBlurRadius);
            canvas.Children.Add(effectHost);
            renderedElements[name] = element;
        }

    }

    private static Visibility MapVisibility(FrontedControlVisibility visibility) =>
        visibility switch
        {
            FrontedControlVisibility.Hidden => Visibility.Hidden,
            FrontedControlVisibility.Collapsed => Visibility.Collapsed,
            _ => Visibility.Visible
        };

    private static void ApplyStaticGaussianBlur(FrontedEffectHost host, bool isEnabled, double radius)
    {
        if (!isEnabled || !double.IsFinite(radius) || radius <= 0D)
        {
            return;
        }

        host.Effect = new System.Windows.Media.Effects.BlurEffect
        {
            Radius = radius,
            RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance
        };
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

    private static void ClearGeneratedControls(Canvas canvas)
    {
        for (var i = canvas.Children.Count - 1; i >= 0; i--)
        {
            if (canvas.Children[i] is FrameworkElement child
                && FindGeneratedSemanticRoot(child) is { } semanticRoot)
            {
                UnregisterGeneratedName(canvas, semanticRoot);
                canvas.Children.RemoveAt(i);
            }
        }
    }

    private static FrameworkElement? FindGeneratedSemanticRoot(DependencyObject root)
    {
        if (root is FrameworkElement element
            && FrontedRendererProperties.GetIsGeneratedControl(element)
            && !FrontedRendererProperties.GetIsAnimationAuxiliaryElement(element))
        {
            return element;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            if (FindGeneratedSemanticRoot(VisualTreeHelper.GetChild(root, i)) is { } result)
            {
                return result;
            }
        }

        return null;
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
        var nameScope = NameScope.GetNameScope(element);
        if (nameScope is null)
        {
            return;
        }

        try
        {
            nameScope.UnregisterName(name);
        }
        catch (ArgumentException)
        {
            // Name was not registered in this namescope.
        }
        catch (InvalidOperationException)
        {
            // The WPF owner may already have lost its namescope during unload.
        }
    }

    private ImageBrush? CreateBackground(string? backgroundImage)
    {
        var imageSource = resourceResolver.ResolveImage(backgroundImage, FrontedImagePurpose.Background);
        if (imageSource is null)
        {
            return null;
        }

        var brush = new ImageBrush(imageSource) { Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
        return brush;
    }
}
