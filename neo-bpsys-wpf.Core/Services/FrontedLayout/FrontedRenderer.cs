using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Options;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
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
    IFrontedV3ControlRegistry v3ControlRegistry,
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
            // V3 是唯一的控件创建路径：所有控件经 FrontedV3ControlHost 包装，
            // Host 唯一负责根布局（Left/Top/ZIndex/Width/Height/Visibility/GaussianBlur）。
            if (v3ControlRegistry.TryGetRegistration(controlConfig.ControlType, out var v3Registration))
            {
                var host = new FrontedV3ControlHost(v3Registration, controlConfig, context.IsDesignerPreview);
                var v3Context = new FrontedV3ControlContext
                {
                    Services = buildContext.Services,
                    SharedDataService = buildContext.SharedDataService,
                    ResourceResolver = buildContext.ResourceResolver,
                    WindowId = buildContext.WindowId,
                    CanvasName = buildContext.CanvasName,
                    CanvasBackgroundImage = buildContext.CanvasBackgroundImage,
                    CanvasWidth = buildContext.CanvasWidth,
                    CanvasHeight = buildContext.CanvasHeight,
                    Config = controlConfig,
                    // ControlName 用于动画部件注册等运行时身份场景（如 MapV2 的 PickingBorder RegisteredName）。
                    // 必须传入与 Canvas 注册名一致的值，否则多个同类型控件会产生空名称冲突与动画目标错绑。
                    ControlName = name,
                    Options = FrontedV3OptionsView.Create(controlConfig, v3Registration.Properties),
                    IsDesignerPreview = buildContext.IsDesignerPreview,
                    Logger = buildContext.Logger
                };
                host.TryInitialize(v3Context, logger);
                RegisterGeneratedName(canvas, name, host);
                canvas.Children.Add(host);
                renderedElements[name] = host;
                continue;
            }

            // 未注册的插件控件：Designer 保留为可选占位，前台窗口跳过。
            if (FrontedPluginControlType.IsPluginControlType(controlConfig.ControlType))
            {
                if (context.RenderMissingPluginPlaceholders)
                {
                    var placeholder = CreateMissingPluginPlaceholder(name, controlConfig);
                    placeholder.Visibility = MapVisibility(controlConfig.Visibility);
                    FrontedRendererProperties.SetIsGeneratedControl(placeholder, true);
                    FrontedRendererProperties.SetBehaviorGuid(placeholder, controlConfig.BehaviorGuid);
                    RegisterGeneratedName(canvas, name, placeholder);
                    var placeholderHost = FrontedEffectHostFactory.Wrap(placeholder);
                    ApplyStaticEffects(placeholderHost, controlConfig);
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

    }

    private static Visibility MapVisibility(FrontedControlVisibility visibility) =>
        visibility switch
        {
            FrontedControlVisibility.Hidden => Visibility.Hidden,
            FrontedControlVisibility.Collapsed => Visibility.Collapsed,
            _ => Visibility.Visible
        };

    private static void ApplyStaticEffects(FrontedEffectHost host, FrontedControlConfigBase config)
    {
        var hasAnyEffect = config.IsGaussianBlurEnabled
                           || config.IsShadowEnabled
                           || config.IsGlowEnabled;

        if (!hasAnyEffect)
        {
            return;
        }

        // 取出占位内容，用效果链包装后放回宿主。
        var content = host.HostedElement;
        host.Child = null;
        var outermost = FrontedEffectHostFactory.BuildEffectChain(content, config);
        host.Child = outermost;
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
