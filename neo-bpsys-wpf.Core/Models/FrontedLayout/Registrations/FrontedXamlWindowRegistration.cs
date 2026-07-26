namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

/// <summary>
/// XAML 前台窗口注册（含内置与插件）。
/// </summary>
/// <remarks>
/// 此类承载由插件或宿主直接提供的 WPF <see cref="System.Windows.Window"/> CLR 类型。
/// <see cref="Kind"/> 固定返回 <see cref="FrontedWindowRegistrationKind.Xaml"/>。
/// </remarks>
public sealed class FrontedXamlWindowRegistration : FrontedWindowRegistration
{
    /// <summary>
    /// WPF 窗口类型，必须可赋值给 <see cref="System.Windows.Window"/>。
    /// </summary>
    public required Type WindowType { get; init; }

    /// <inheritdoc />
    public override FrontedWindowRegistrationKind Kind => FrontedWindowRegistrationKind.Xaml;
}
