using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 描述 v3 前台窗口注册表已知的 WPF 前台输出窗口。
/// </summary>
/// <remarks>
/// <see cref="WindowId"/> 是运行时标识，应为稳定的 GUID。
/// <see cref="FullWindowType"/> 是布局/包标识。内置窗口使用如
/// <c>BpWindow</c> 或 <c>ScoreGlobalWindow</c> 的名称；插件窗口使用
/// <c>plugin:{PackageId}/{WindowTypeName}</c>。
/// </remarks>
public interface IFrontedWindowDescriptor
{
    /// <summary>
    /// 稳定的运行时窗口标识。插件作者应生成一个 GUID 并保持不变。
    /// </summary>
    string WindowId { get; }

    /// <summary>
    /// 提供方内部的短窗口类型名，例如 <c>BpWindow</c> 或插件的本地窗口名。
    /// </summary>
    string WindowTypeName { get; }

    /// <summary>
    /// 布局和包标识。插件值使用 <c>plugin:{PackageId}/{WindowTypeName}</c>。
    /// </summary>
    string FullWindowType { get; }

    /// <summary>
    /// 当 <see cref="DisplayNameKey"/> 未被宿主本地化时使用的回退显示名称。
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 可选的本地化显示名称，以具体应用语言为键。
    /// </summary>
    IReadOnlyDictionary<LanguageKey, string>? I18nDisplayNames { get; }

    /// <summary>
    /// 窗口显示名称的可选本地化键。
    /// </summary>
    string? DisplayNameKey { get; }

    /// <summary>
    /// 回退的可读描述。
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// 窗口描述的可选本地化键。
    /// </summary>
    string? DescriptionKey { get; }

    /// <summary>
    /// 稳定的管理分组键。
    /// </summary>
    string? GroupKey { get; }

    /// <summary>
    /// 管理分组内稳定的显示顺序。
    /// </summary>
    int? DisplayOrder { get; }

    /// <summary>
    /// 此窗口是否在前台管理页面可见。
    /// </summary>
    bool IsVisibleInFrontManage { get; }

    /// <summary>
    /// 此窗口是否由以窗口为中心的 v3 布局宿主渲染。
    /// </summary>
    bool IsV3LayoutWindow { get; }

    /// <summary>
    /// 窗口布局是否可定制。
    /// </summary>
    bool Customizable { get; }

    /// <summary>
    /// 此前台窗口的提供方和编辑模式。
    /// </summary>
    FrontedWindowKind Kind { get; }

    /// <summary>
    /// 描述符是否来自插件贡献者。
    /// </summary>
    bool IsPlugin { get; }

    /// <summary>
    /// 当 <see cref="IsPlugin"/> 为 true 时的插件包 ID；否则为 <see langword="null"/>。
    /// </summary>
    string? PackageId { get; }

}
