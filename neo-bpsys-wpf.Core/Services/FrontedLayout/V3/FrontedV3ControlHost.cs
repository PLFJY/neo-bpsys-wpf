using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.PluginSdk;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3;

/// <summary>
/// v3 前台控件的根布局宿主，唯一负责根级属性的应用与错误占位。
/// </summary>
/// <remarks>
/// <para>
/// 结构统一为 <c>Canvas → FrontedV3ControlHost → FrontedV3ControlBase</c>。
/// Host 是 v3 控件路径中唯一被直接加入 Canvas 的元素，所有根级属性都由 Host 拥有，
/// 包装的 Control 只负责矩形区域内的视觉内容，不得设置自己的 Canvas 坐标。
/// </para>
/// <para>
/// Host 唯一负责以下根属性：
/// <list type="bullet">
/// <item><see cref="System.Windows.Controls.Canvas.LeftProperty"/>、<see cref="System.Windows.Controls.Canvas.TopProperty"/></item>
/// <item><see cref="System.Windows.Controls.Panel.ZIndexProperty"/></item>
/// <item><see cref="FrameworkElement.Width"/>、<see cref="FrameworkElement.Height"/></item>
/// <item><see cref="UIElement.Visibility"/></item>
/// <item><see cref="System.Windows.Media.Effects.Effect"/>（高斯模糊）</item>
/// <item><see cref="FrontedRendererProperties.BehaviorGuidProperty"/> 标记</item>
/// <item><see cref="FrontedRendererProperties.IsGeneratedControlProperty"/> 运行时生成标记</item>
/// <item>Designer 选中与根 Move/Resize（通过 <see cref="RootControlGeometryTarget"/>）</item>
/// <item>错误占位（<see cref="ShowError"/>）</item>
/// </list>
/// </para>
/// <para>
/// 本 Host 是所有 v3 前台控件的唯一根布局实现。
/// </para>
/// </remarks>
public sealed class FrontedV3ControlHost : Decorator
{
    private const string ErrorPlaceholderTitle = "V3 Control Error";
    private const string DesignerPlaceholderTitle = "V3 Control Error (Designer)";

    private readonly FrontedV3ControlRegistration _registration;
    private readonly FrontedControlConfigBase _config;
    private readonly bool _isDesignerPreview;
    private RootControlGeometryTarget? _geometryTarget;

    /// <summary>
    /// 初始化 <see cref="FrontedV3ControlHost"/> 并保存根布局所需的状态。
    /// </summary>
    /// <param name="registration">v3 控件注册信息。</param>
    /// <param name="config">控件配置实例，作为根级字段的单一事实来源。</param>
    /// <param name="isDesignerPreview">指示当前是否为 Designer 预览渲染，影响错误占位文案。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="registration"/> 或 <paramref name="config"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3ControlHost(
        FrontedV3ControlRegistration registration,
        FrontedControlConfigBase config,
        bool isDesignerPreview)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(config);
        _registration = registration;
        _config = config;
        _isDesignerPreview = isDesignerPreview;
    }

    /// <summary>
    /// 获取本 Host 关联的 v3 控件注册信息。
    /// </summary>
    public FrontedV3ControlRegistration Registration => _registration;

    /// <summary>
    /// 获取本 Host 关联的控件配置实例，是根级字段的单一事实来源。
    /// </summary>
    public FrontedControlConfigBase Config => _config;

    /// <summary>
    /// 获取本 Host 是否用于 Designer 预览渲染。
    /// </summary>
    public bool IsDesignerPreview => _isDesignerPreview;

    /// <summary>
    /// 获取被 Host 包装的 v3 控件实例；未成功创建时为 <see langword="null"/>。
    /// </summary>
    public FrameworkElement? Control { get; private set; }

    /// <summary>
    /// 获取控件初始化失败时捕获的异常；初始化成功时为 <see langword="null"/>。
    /// </summary>
    public Exception? InitializationError { get; private set; }

    /// <summary>
    /// 获取一个值指示 Host 当前是否处于错误占位状态。
    /// </summary>
    public bool HasError => InitializationError is not null;

    /// <summary>
    /// 获取控件初始化成功后由 <see cref="FrontedV3PartVisualRuntimeBinder"/> 产生的 Part Visual 发现结果；
    /// 未初始化或控件无固定 Part 时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// Designer 可据此读取 Missing/Duplicate Visual 诊断并展示给用户；Runtime 仅用于日志。
    /// </remarks>
    public FrontedV3PartVisualDiscoveryResult? PartVisualDiscovery { get; private set; }

    /// <summary>
    /// 将成功创建并初始化的 v3 控件附加到 Host。
    /// </summary>
    /// <param name="control">要包装的 v3 控件实例。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="control"/> 为 <see langword="null"/> 时抛出。</exception>
    public void AttachControl(FrameworkElement control)
    {
        ArgumentNullException.ThrowIfNull(control);
        Control = control;
        InitializationError = null;
        Child = control;
    }

    /// <summary>
    /// 将 Host 切换为错误占位状态，替换视觉内容为安全占位元素。
    /// </summary>
    /// <param name="error">导致初始化失败的异常。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="error"/> 为 <see langword="null"/> 时抛出。</exception>
    public void ShowError(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        InitializationError = error;
        Control = null;
        Child = CreateErrorPlaceholder(error);
    }

    /// <summary>
    /// 创建并初始化 v3 控件，将其附加到 Host；失败时切换到错误占位状态。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该方法是 v3 控件创建路径的错误边界，覆盖以下失败场景：
    /// <list type="bullet">
    /// <item>控件构造函数失败（含 XAML <c>InitializeComponent</c> 失败）。</item>
    /// <item><c>InitializeFrontedV3</c> 调用失败（含 Binding 初始化、服务解析失败）。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 控件通过 <see cref="ActivatorUtilities.CreateInstance"/> 创建，支持构造函数依赖注入；
    /// 随后直接调用 <see cref="FrontedV3ControlBase.InitializeFrontedV3"/> 完成上下文注入。
    /// </para>
    /// <para>
    /// 根布局（<see cref="ApplyRootLayout"/>）在创建控件之前应用，确保无论成功还是失败 Host 都具备
    /// <c>Left</c>/<c>Top</c>/<c>Width</c>/<c>Height</c>/<c>ZIndex</c>/<c>Visibility</c>、
    /// <c>BehaviorGuid</c> 与 <c>IsGeneratedControl</c> 标记。失败路径的错误占位同样可被
    /// <c>FrontedRenderer</c> 通过 <c>IsGeneratedControl</c> 识别并清理，避免重复堆积。
    /// </para>
    /// <para>
    /// 失败时：Runtime 记录 warning 并显示安全占位；Designer 通过 <see cref="IsDesignerPreview"/>
    /// 显示带 Designer 标识的错误占位；原 <see cref="Config"/> 保留，不写默认值覆盖。
    /// </para>
    /// </remarks>
    /// <param name="context">控件运行时上下文，将传递给 <c>InitializeFrontedV3</c>。</param>
    /// <param name="logger">可选日志，用于记录初始化失败警告。</param>
    /// <returns>成功初始化时为 <see langword="true"/>；失败时为 <see langword="false"/>（已切换到错误占位）。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="context"/> 为 <see langword="null"/> 时抛出。</exception>
    public bool TryInitialize(FrontedV3ControlContext context, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 先应用根布局：确保无论后续创建/初始化是否成功，Host 自身都具备根级字段与 generated 标记。
        // 错误占位也需被 Renderer 通过 IsGeneratedControl 识别并清理。
        ApplyRootLayout();

        try
        {
            // 通过 ActivatorUtilities 创建控件，支持构造函数 DI；当控件无 DI 依赖时回退到无参构造。
            var control = ActivatorUtilities.CreateInstance(context.Services, _registration.ControlType);

            if (control is not FrontedV3ControlBase v3Control)
            {
                throw new InvalidOperationException(
                    $"Control type '{_registration.ControlType.FullName}' did not produce a FrontedV3ControlBase instance. " +
                    "Ensure the type inherits from FrontedV3ControlBase.");
            }

            v3Control.InitializeFrontedV3(context);

            AttachControl(v3Control);

            // 控件附加后，框架统一接管 Part Visual 的运行时几何绑定：
            // 通过 FrontedV3PartVisualResolver 发现 PartId → Visual 映射，
            // 并将 Storage 中的 Width/Height/X/Y 应用到对应 FrameworkElement。
            // 插件作者无需在 OnInitializeFrontedV3 中手写几何读取代码。
            if (_registration.FixedParts.Count > 0)
            {
                PartVisualDiscovery = FrontedV3PartVisualRuntimeBinder.Bind(
                    v3Control,
                    _registration.FixedParts,
                    _config,
                    logger);

                if (PartVisualDiscovery.HasDiagnostics)
                {
                    foreach (var diagnostic in PartVisualDiscovery.Diagnostics)
                    {
                        logger?.LogWarning(
                            "Part visual diagnostic for {CanonicalControlType} PartId {PartId}: {Message}",
                            _registration.CanonicalControlType,
                            diagnostic.PartId,
                            diagnostic.Message);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Failed to initialize v3 control {CanonicalControlType}.",
                _registration.CanonicalControlType);

            ShowError(UnwrapReflectionException(ex));
            return false;
        }
    }

    /// <summary>
    /// 从当前 Config 重新应用所有根级属性到 Host 自身。
    /// </summary>
    /// <remarks>
    /// 调用方在 Config 字段被外部修改后可调用本方法恢复视觉一致性。
    /// </remarks>
    public void ApplyRootLayout()
    {
        Canvas.SetLeft(this, _config.Left);
        Canvas.SetTop(this, _config.Top);
        Panel.SetZIndex(this, _config.ZIndex);

        // Width/Height: null 时回退为 NaN（自适应），与旧链路中控件自身设 Width/Height 行为对齐。
        Width = _config.Width ?? double.NaN;
        Height = _config.Height ?? double.NaN;

        Visibility = MapVisibility(_config.Visibility);

        ApplyGaussianBlur();

        FrontedRendererProperties.SetIsGeneratedControl(this, true);
        FrontedRendererProperties.SetBehaviorGuid(this, _config.BehaviorGuid);
    }

    /// <summary>
    /// 返回本 Host 的根几何操作目标，供 Designer 的 Move/Resize 调用。
    /// </summary>
    /// <returns>绑定到本 Host 与 Config 的 <see cref="RootControlGeometryTarget"/>。</returns>
    public IFrontedV3GeometryTarget GetGeometryTarget()
    {
        return _geometryTarget ??= new RootControlGeometryTarget(this, _config);
    }

    private static Visibility MapVisibility(FrontedControlVisibility visibility) =>
        visibility switch
        {
            FrontedControlVisibility.Hidden => Visibility.Hidden,
            FrontedControlVisibility.Collapsed => Visibility.Collapsed,
            _ => Visibility.Visible
        };

    private static Exception UnwrapReflectionException(Exception ex)
    {
        // Activator.CreateInstance 与 MethodInfo.Invoke 会把实际异常包装为 TargetInvocationException，
        // 错误占位与日志应展示真实原因（如 XamlParseException）。
        if (ex is TargetInvocationException tie && tie.InnerException is not null)
        {
            return tie.InnerException;
        }

        return ex;
    }

    private void ApplyGaussianBlur()
    {
        if (!_config.IsGaussianBlurEnabled
            || !double.IsFinite(_config.GaussianBlurRadius)
            || _config.GaussianBlurRadius <= 0D)
        {
            Effect = null;
            return;
        }

        Effect = new BlurEffect
        {
            Radius = _config.GaussianBlurRadius,
            RenderingBias = RenderingBias.Performance
        };
    }

    private FrameworkElement CreateErrorPlaceholder(Exception error)
    {
        var title = _isDesignerPreview ? DesignerPlaceholderTitle : ErrorPlaceholderTitle;
        var detail = error.GetType().Name;

        var border = new Border
        {
            BorderBrush = Brushes.OrangeRed,
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(96, 60, 20, 20)),
            Child = new TextBlock
            {
                Text = $"{title}\n{_registration.CanonicalControlType}\n{detail}",
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };

        return border;
    }
}
