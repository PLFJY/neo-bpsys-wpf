using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.AttachedBehaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
            SharedDataService = sharedDataService,
            ResourceResolver = resourceResolver,
            WindowId = context.WindowId,
            CanvasName = context.CanvasName,
            Logger = logger
        };

        foreach (var (name, controlConfig) in config.Controls.OrderBy(x => x.Value.ZIndex))
        {
            var factory = controlRegistry.GetControl(controlConfig.ControlType);
            if (factory is null)
            {
                throw new FrontedLayoutConfigException(
                    $"Control '{name}' has no registered factory for ControlType '{controlConfig.ControlType}'.");
            }

            var element = factory.Create(name, controlConfig, buildContext);
            FrontedRendererProperties.SetIsGeneratedControl(element, true);
            BindingOperations.SetBinding(element, DesignBehavior.IsDesignerModeProperty, new Binding("IsDesignerMode")
            {
                Source = canvas.DataContext
            });
            RegisterGeneratedName(canvas, name, element);
            canvas.Children.Add(element);
        }
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
        var imageSource = resourceResolver.ResolveImage(backgroundImage);
        return imageSource is null
            ? null
            : new ImageBrush(imageSource) { Stretch = Stretch.Fill };
    }
}
