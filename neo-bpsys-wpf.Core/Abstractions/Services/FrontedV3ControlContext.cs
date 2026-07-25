using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Options;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台控件的运行时上下文，由宿主在创建控件时提供。
/// </summary>
/// <remarks>
/// <para>
/// 该上下文替代旧 <see cref="FrontedControlBuildContext"/> 中面向 v3 控件的部分，
/// 但在 Phase 1 期间两者并存：旧链路继续使用 <see cref="FrontedControlBuildContext"/>，
/// 新 v3 控件链路使用本类型。
/// </para>
/// <para>
/// 上下文携带控件运行所需的服务、所属窗口/Canvas 信息、当前 <see cref="Config"/> 实例，
/// 以及由属性 Schema 构建的 <see cref="Options"/> 动态代理视图。控件 XAML 通过绑定
/// <c>{Binding Options.Appearance.TextColor}</c> 等路径读写属性，最终委托到对应
/// <c>IFrontedV3StorageAccessor</c>。
/// </para>
/// </remarks>
public sealed class FrontedV3ControlContext
{
    /// <summary>
    /// DI 服务提供器。
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// 共享数据服务。
    /// </summary>
    public required ISharedDataService SharedDataService { get; init; }

    /// <summary>
    /// 前台资源解析器。
    /// </summary>
    public required IFrontedResourceResolver ResourceResolver { get; init; }

    /// <summary>
    /// 前台窗口 ID。
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// Canvas 名称。
    /// </summary>
    public required string CanvasName { get; init; }

    /// <summary>
    /// 当前控件配置实例。Options 视图直接代理该实例，不缓存独立值。
    /// </summary>
    public required FrontedControlConfigBase Config { get; init; }

    /// <summary>
    /// 控件在画布布局中的名称，用于内部动画部件注册等场景。
    /// </summary>
    public string? ControlName { get; init; }

    /// <summary>
    /// 由属性 Schema 构建的 Options 动态代理视图；无属性时为 <see langword="null"/>。
    /// </summary>
    public FrontedV3OptionsView? Options { get; init; }

    /// <summary>
    /// 经过运行时状态解析后生效的画布背景图片。
    /// </summary>
    public string? CanvasBackgroundImage { get; init; }

    /// <summary>
    /// Canvas 宽度。
    /// </summary>
    public double CanvasWidth { get; init; }

    /// <summary>
    /// Canvas 高度。
    /// </summary>
    public double CanvasHeight { get; init; }

    /// <summary>
    /// 指示控件是否为设计器预览而构建。
    /// </summary>
    public bool IsDesignerPreview { get; init; }

    /// <summary>
    /// 可选日志。
    /// </summary>
    public ILogger? Logger { get; init; }

    /// <summary>
    /// 基于当前 v3 上下文构建一个 <see cref="FrontedControlBuildContext"/>，用于传递给尚未迁移签名的内置控件工厂辅助方法。
    /// </summary>
    /// <returns>携带相同服务与画布信息的构建上下文。</returns>
    public FrontedControlBuildContext ToBuildContext()
    {
        return new FrontedControlBuildContext
        {
            Services = Services,
            SharedDataService = SharedDataService,
            ResourceResolver = ResourceResolver,
            WindowId = WindowId,
            CanvasName = CanvasName,
            CanvasBackgroundImage = CanvasBackgroundImage,
            CanvasWidth = CanvasWidth,
            CanvasHeight = CanvasHeight,
            IsDesignerPreview = IsDesignerPreview,
            Logger = Logger
        };
    }
}
