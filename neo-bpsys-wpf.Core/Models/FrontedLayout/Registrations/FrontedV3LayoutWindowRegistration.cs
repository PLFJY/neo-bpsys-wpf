namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

/// <summary>
/// v3 Layout host 前台窗口注册（含内置与插件）。
/// </summary>
/// <remarks>
/// 此类仅描述由宿主 v3 布局渲染器承载的窗口身份，不承载窗口 CLR 类型、默认布局根目录、
/// 插件目录或允许空白默认布局等字段。<see cref="Kind"/> 固定返回
/// <see cref="FrontedWindowRegistrationKind.V3Layout"/>。
/// </remarks>
public sealed class FrontedV3LayoutWindowRegistration : FrontedWindowRegistration
{
    /// <inheritdoc />
    public override FrontedWindowRegistrationKind Kind => FrontedWindowRegistrationKind.V3Layout;
}
