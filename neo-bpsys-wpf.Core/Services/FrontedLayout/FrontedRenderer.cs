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
    ILogger<FrontedRenderer> logger) : IFrontedRenderer, IFrontedIncrementalRenderer
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
        canvas.Background = CreateBackground(runtimeState.BackgroundImage, runtimeState.CanvasWidth, runtimeState.CanvasHeight);

        var buildContext = CreateBuildContext(runtimeState, context);
        foreach (var (name, controlConfig) in runtimeState.Controls.OrderBy(x => x.Value.ZIndex))
        {
            if (TryCreateRenderedControl(canvas, name, controlConfig, buildContext, context) is { } visual)
            {
                canvas.Children.Add(visual);
            }
        }
    }

    /// <summary>
    /// 只刷新指定控件的预览视觉，用于属性编辑等局部变更。
    /// </summary>
    /// <param name="canvas">目标 Canvas。</param>
    /// <param name="config">当前画布配置。</param>
    /// <param name="context">渲染上下文。</param>
    /// <param name="controlName">要刷新的控件名。</param>
    /// <returns>成功替换控件视觉时为 <see langword="true"/>；控件不存在或类型不支持时为 <see langword="false"/>。</returns>
    /// <remarks>
    /// 背景、Canvas 尺寸和其他控件保持不变；调用方应在局部刷新失败时回退到完整渲染。
    /// </remarks>
    public bool RefreshControl(
        Canvas canvas,
        FrontedCanvasConfig config,
        FrontedRenderContext context,
        string controlName)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(controlName))
        {
            return false;
        }

        var runtimeState = FrontedCanvasRuntimeStateResolver.Resolve(
            config,
            context.SharedDataServiceOverride ?? sharedDataService,
            logger);
        var buildContext = CreateBuildContext(runtimeState, context);
        return RefreshControlCore(canvas, runtimeState, buildContext, context, controlName);
    }

    /// <summary>
    /// 批量刷新指定控件的预览视觉，只解析一次画布运行时状态。
    /// </summary>
    /// <param name="canvas">目标 Canvas。</param>
    /// <param name="config">当前画布配置。</param>
    /// <param name="context">渲染上下文。</param>
    /// <param name="controlNames">要刷新的控件名集合。</param>
    /// <returns>成功刷新的控件数量。</returns>
    public int RefreshControls(
        Canvas canvas,
        FrontedCanvasConfig config,
        FrontedRenderContext context,
        IEnumerable<string> controlNames)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(controlNames);

        var runtimeState = FrontedCanvasRuntimeStateResolver.Resolve(
            config,
            context.SharedDataServiceOverride ?? sharedDataService,
            logger);
        var buildContext = CreateBuildContext(runtimeState, context);
        var refreshedCount = 0;
        foreach (var controlName in controlNames.Distinct(StringComparer.Ordinal))
        {
            if (RefreshControlCore(canvas, runtimeState, buildContext, context, controlName))
            {
                refreshedCount++;
            }
        }

        return refreshedCount;
    }

    private bool RefreshControlCore(
        Canvas canvas,
        FrontedCanvasRuntimeState runtimeState,
        FrontedControlBuildContext buildContext,
        FrontedRenderContext context,
        string controlName)
    {
        if (string.IsNullOrWhiteSpace(controlName)
            || !runtimeState.Controls.TryGetValue(controlName, out var controlConfig))
        {
            return false;
        }

        var existing = FindGeneratedElement(canvas, controlName);
        var insertionIndex = canvas.Children.Count;
        if (existing is not null)
        {
            var existingDirectChild = FindDirectChild(canvas, existing);
            if (existingDirectChild is not null)
            {
                insertionIndex = canvas.Children.IndexOf(existingDirectChild);
            }

            RemoveGeneratedElement(canvas, existing);
        }

        var visual = TryCreateRenderedControl(canvas, controlName, controlConfig, buildContext, context);
        if (visual is null)
        {
            return false;
        }

        Panel.SetZIndex(visual, controlConfig.ZIndex);
        canvas.Children.Insert(Math.Clamp(insertionIndex, 0, canvas.Children.Count), visual);
        return true;
    }

    /// <summary>
    /// 向现有预览 Canvas 增量添加一个控件，用于粘贴/新增控件。
    /// </summary>
    /// <param name="canvas">目标 Canvas。</param>
    /// <param name="config">当前画布配置。</param>
    /// <param name="context">渲染上下文。</param>
    /// <param name="controlName">控件名。</param>
    /// <returns>成功添加时为 <see langword="true"/>。</returns>
    public bool AddControl(
        Canvas canvas,
        FrontedCanvasConfig config,
        FrontedRenderContext context,
        string controlName)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (string.IsNullOrWhiteSpace(controlName))
        {
            return false;
        }

        if (FindGeneratedElement(canvas, controlName) is not null)
        {
            return RefreshControl(canvas, config, context, controlName);
        }

        var runtimeState = FrontedCanvasRuntimeStateResolver.Resolve(
            config,
            context.SharedDataServiceOverride ?? sharedDataService,
            logger);
        var buildContext = CreateBuildContext(runtimeState, context);
        return AddControlCore(canvas, runtimeState, buildContext, context, controlName);
    }

    /// <summary>
    /// 批量向现有预览 Canvas 增量添加控件，只解析一次画布运行时状态。
    /// </summary>
    /// <param name="canvas">目标 Canvas。</param>
    /// <param name="config">当前画布配置。</param>
    /// <param name="context">渲染上下文。</param>
    /// <param name="controlNames">要添加的控件名集合。</param>
    /// <returns>成功添加/刷新的控件数量。</returns>
    public int AddControls(
        Canvas canvas,
        FrontedCanvasConfig config,
        FrontedRenderContext context,
        IEnumerable<string> controlNames)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(controlNames);

        var runtimeState = FrontedCanvasRuntimeStateResolver.Resolve(
            config,
            context.SharedDataServiceOverride ?? sharedDataService,
            logger);
        var buildContext = CreateBuildContext(runtimeState, context);
        var knownOrderedNames = runtimeState.Controls
            .OrderBy(pair => pair.Value.ZIndex)
            .Select(pair => pair.Key)
            .ToList();
        var addedCount = 0;
        foreach (var controlName in controlNames.Distinct(StringComparer.Ordinal))
        {
            var added = FindGeneratedElement(canvas, controlName) is not null
                ? RefreshControlCore(canvas, runtimeState, buildContext, context, controlName)
                : AddControlCore(canvas, runtimeState, buildContext, context, controlName, knownOrderedNames);
            if (added)
            {
                addedCount++;
            }
        }

        return addedCount;
    }

    private bool AddControlCore(
        Canvas canvas,
        FrontedCanvasRuntimeState runtimeState,
        FrontedControlBuildContext buildContext,
        FrontedRenderContext context,
        string controlName,
        IReadOnlyList<string>? knownOrderedNames = null)
    {
        if (string.IsNullOrWhiteSpace(controlName)
            || !runtimeState.Controls.TryGetValue(controlName, out var controlConfig))
        {
            return false;
        }

        var visual = TryCreateRenderedControl(canvas, controlName, controlConfig, buildContext, context);
        if (visual is null)
        {
            return false;
        }

        Panel.SetZIndex(visual, controlConfig.ZIndex);
        var orderedNames = knownOrderedNames ?? runtimeState.Controls
            .OrderBy(pair => pair.Value.ZIndex)
            .Select(pair => pair.Key)
            .ToList();
        var controlOrder = -1;
        for (var index = 0; index < orderedNames.Count; index++)
        {
            if (string.Equals(orderedNames[index], controlName, StringComparison.Ordinal))
            {
                controlOrder = index;
                break;
            }
        }
        var insertIndex = canvas.Children.Count;
        if (controlOrder >= 0)
        {
            var beforeNames = orderedNames.Take(controlOrder).ToHashSet(StringComparer.Ordinal);
            insertIndex = 0;
            foreach (var child in canvas.Children.OfType<FrameworkElement>())
            {
                var childName = FrontedRendererProperties.GetRegisteredName(child);
                if (beforeNames.Contains(childName))
                {
                    insertIndex++;
                }
            }
        }

        canvas.Children.Insert(Math.Clamp(insertIndex, 0, canvas.Children.Count), visual);
        return true;
    }

    /// <summary>
    /// 从现有预览 Canvas 增量移除一个控件，用于删除控件。
    /// </summary>
    /// <param name="canvas">目标 Canvas。</param>
    /// <param name="controlName">控件名。</param>
    /// <returns>找到并移除控件时为 <see langword="true"/>。</returns>
    public bool RemoveControl(Canvas canvas, string controlName)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (string.IsNullOrWhiteSpace(controlName))
        {
            return false;
        }

        var existing = FindGeneratedElement(canvas, controlName);
        if (existing is null)
        {
            return false;
        }

        RemoveGeneratedElement(canvas, existing);
        return true;
    }

    /// <summary>
    /// 批量从现有预览 Canvas 增量移除控件。
    /// </summary>
    /// <param name="canvas">目标 Canvas。</param>
    /// <param name="controlNames">要移除的控件名集合。</param>
    /// <returns>实际移除的控件数量。</returns>
    public int RemoveControls(Canvas canvas, IEnumerable<string> controlNames)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(controlNames);

        var removedCount = 0;
        foreach (var controlName in controlNames.Distinct(StringComparer.Ordinal))
        {
            if (RemoveControl(canvas, controlName))
            {
                removedCount++;
            }
        }

        return removedCount;
    }

    private FrontedControlBuildContext CreateBuildContext(
        FrontedCanvasRuntimeState runtimeState,
        FrontedRenderContext context) =>
        new()
        {
            Services = services,
            SharedDataService = context.SharedDataServiceOverride ?? sharedDataService,
            CharacterPresentation = !context.IsDesignerPreview && context.SharedDataServiceOverride is null &&
                                    string.Equals(context.WindowTypeName, "BpWindow", StringComparison.Ordinal)
                ? services.GetService(typeof(IFrontedCharacterPresentationService)) as IFrontedCharacterPresentationService
                : null,
            ResourceResolver = resourceResolver,
            WindowId = context.WindowId,
            CanvasName = context.CanvasName,
            CanvasBackgroundImage = runtimeState.BackgroundImage,
            CanvasWidth = runtimeState.CanvasWidth,
            CanvasHeight = runtimeState.CanvasHeight,
            IsDesignerPreview = context.IsDesignerPreview,
            Logger = logger
        };

    private FrameworkElement? TryCreateRenderedControl(
        Canvas canvas,
        string name,
        FrontedControlConfigBase controlConfig,
        FrontedControlBuildContext buildContext,
        FrontedRenderContext context)
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
                CharacterPresentation = buildContext.CharacterPresentation,
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
            return host;
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
                return placeholderHost;
            }

            logger.LogWarning(
                "Skipping fronted plugin control {ControlName} because ControlType {ControlType} is not registered.",
                name,
                controlConfig.ControlType);
            return null;
        }

        throw new FrontedLayoutConfigException(
            $"Control '{name}' has no registered factory for ControlType '{controlConfig.ControlType}'.");
    }

    private static FrameworkElement? FindGeneratedElement(DependencyObject root, string name)
    {
        if (root is FrameworkElement element)
        {
            var registeredName = FrontedRendererProperties.GetRegisteredName(element);
            if (string.Equals(registeredName, name, StringComparison.Ordinal)
                || string.Equals(element.Name, name, StringComparison.Ordinal))
            {
                return element;
            }
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            if (FindGeneratedElement(VisualTreeHelper.GetChild(root, index), name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static FrameworkElement? FindDirectChild(Canvas canvas, FrameworkElement element)
    {
        var current = element;
        while (current is not null)
        {
            if (ReferenceEquals(VisualTreeHelper.GetParent(current), canvas))
            {
                return current;
            }

            current = VisualTreeHelper.GetParent(current) as FrameworkElement;
        }

        return null;
    }

    private static void RemoveGeneratedElement(Canvas canvas, FrameworkElement? element)
    {
        if (element is null)
        {
            return;
        }

        var directChild = FindDirectChild(canvas, element);
        if (directChild is null)
        {
            return;
        }

        UnregisterGeneratedNameInSubtree(canvas, directChild);
        canvas.Children.Remove(directChild);
    }

    private static void UnregisterGeneratedNameInSubtree(Canvas canvas, DependencyObject root)
    {
        if (root is FrameworkElement element)
        {
            UnregisterGeneratedName(canvas, element);
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            UnregisterGeneratedNameInSubtree(canvas, VisualTreeHelper.GetChild(root, index));
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

    /// <summary>移除生成的控件并注销其完整名称树，供重绘和设计器清空预览共用。</summary>
    /// <param name="canvas">承载生成控件的画布。</param>
    internal static void ClearGeneratedControls(Canvas canvas)
    {
        for (var i = canvas.Children.Count - 1; i >= 0; i--)
        {
            if (canvas.Children[i] is FrameworkElement child
                && FindGeneratedSemanticRoot(child) is not null)
            {
                UnregisterGeneratedTreeNames(canvas, child);
                canvas.Children.RemoveAt(i);
            }
        }
    }

    private static void UnregisterGeneratedTreeNames(Canvas canvas, DependencyObject root)
    {
        // 动画部件也注册在窗口 NameScope 中。仅注销根控件会让已删除部件继续持有旧预览树。
        if (FrontedRendererProperties.GetIsGeneratedControl(root))
        {
            UnregisterGeneratedName(canvas, root);
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            UnregisterGeneratedTreeNames(canvas, VisualTreeHelper.GetChild(root, i));
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

    private ImageBrush? CreateBackground(string? backgroundImage, double canvasWidth, double canvasHeight)
    {
        var imageSource = resourceResolver.ResolveImage(backgroundImage, FrontedImagePurpose.Background);
        if (imageSource is null)
        {
            return null;
        }

        // 背景只覆盖画布本身。设计器为了显示画布外控件会扩展 Canvas，不能用 Stretch=Fill
        // 让背景跟随整个扩展区域，否则背景会被异常拉伸。
        var brush = new ImageBrush(imageSource)
        {
            Stretch = Stretch.Fill,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0D, 0D, Math.Max(1D, canvasWidth), Math.Max(1D, canvasHeight))
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
        if (brush.CanFreeze)
        {
            // 背景画刷创建后不再变化，冻结可减少 WPF 变更跟踪与跨线程复制开销。
            brush.Freeze();
        }

        return brush;
    }
}
