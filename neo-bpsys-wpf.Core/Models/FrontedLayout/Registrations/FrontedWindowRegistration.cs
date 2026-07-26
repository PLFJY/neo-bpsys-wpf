namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

/// <summary>
/// 前台窗口注册的强类型基类。
/// </summary>
/// <remarks>
/// <see cref="Id"/> 是 Canonical ID：内置窗口等于 <see cref="LocalId"/>；
/// 插件窗口为 <c>plugin:{PackageId}/{LocalId}</c>。<see cref="Kind"/> 由派生类固定返回。
/// 该基类不承载窗口 CLR 类型或布局资源信息，这些由派生类按承载方式分别提供。
/// 来源分组（BuiltIn / Plugin / External）由 UI 层基于 <see cref="IsBuiltIn"/> 与
/// <see cref="PackageId"/> 推导；顺序使用 DI 注册顺序或在 UI 按 <see cref="LocalId"/> 排序；
/// 内置显示名由 UI 层通过现有 resx（<c>Designer.Window.{LocalId}</c>）解析。
/// </remarks>
public abstract class FrontedWindowRegistration
{
    /// <summary>
    /// 窗口的 Canonical ID。内置窗口为 <see cref="LocalId"/>；
    /// 插件窗口为 <c>plugin:{PackageId}/{LocalId}</c>。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 提供方内部的局部窗口标识，例如内置的 <c>BpWindow</c> 或插件的本地窗口名。
    /// </summary>
    public required string LocalId { get; init; }

    /// <summary>
    /// 当注册来自插件时为插件包 ID；内置窗口或非插件宿主直接注册时为 <see langword="null"/>。
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// 是否为宿主内置窗口。
    /// </summary>
    public required bool IsBuiltIn { get; init; }

    /// <summary>
    /// 窗口的显示名称。插件 XAML 使用 Attribute Name，插件 v3 与内置默认回退 <see cref="LocalId"/>；
    /// 内置窗口的本地化显示名由 UI 层通过 resx 覆盖。
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 窗口的承载方式。派生类返回固定值。
    /// </summary>
    public abstract FrontedWindowRegistrationKind Kind { get; }
}
