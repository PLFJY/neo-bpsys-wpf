using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

/// <summary>
/// 前台窗口注册的强类型基类。
/// </summary>
/// <remarks>
/// <see cref="Id"/> 是 Canonical ID：内置窗口等于 <see cref="LocalId"/>；
/// 插件窗口为 <c>plugin:{PackageId}/{LocalId}</c>。<see cref="Kind"/> 由派生类固定返回。
/// 该基类不承载窗口 CLR 类型或布局资源信息，这些由派生类按承载方式分别提供。
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
    /// 窗口的显示名称。插件默认回退 <see cref="LocalId"/>，内置后续由本地化覆盖。
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 窗口的承载方式。派生类返回固定值。
    /// </summary>
    public abstract FrontedWindowRegistrationKind Kind { get; }

    /// <summary>
    /// 稳定的管理分组键。为空时由注册表回退到 <c>Plugin</c> 或 <c>BuiltIn</c>。
    /// </summary>
    public string? GroupKey { get; init; }

    /// <summary>
    /// 管理分组内稳定的显示顺序。为空时排序靠后。
    /// </summary>
    public int? DisplayOrder { get; init; }

    /// <summary>
    /// 可选的本地化显示名称，以具体应用语言为键。内置窗口使用此字段提供多语言显示名。
    /// </summary>
    public IReadOnlyDictionary<LanguageKey, string>? I18nDisplayNames { get; init; }
}
