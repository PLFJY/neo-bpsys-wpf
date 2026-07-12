using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 现有内置 WPF 前台窗口的描述符包装。
/// </summary>
/// <remarks>
/// 内置 <see cref="IFrontedWindowDescriptor.FullWindowType"/> 值为窗口类型名称，
/// 例如 <c>BpWindow</c>、<c>BpOverviewWindow</c> 或 <c>ScoreGlobalWindow</c>。
/// </remarks>
public sealed class FrontedBuiltInWindowDescriptor : IFrontedWindowDescriptor
{
    /// <inheritdoc />
    public string WindowId { get; init; } = string.Empty;

    /// <inheritdoc />
    public string WindowTypeName { get; init; } = string.Empty;

    /// <inheritdoc />
    public string FullWindowType => WindowTypeName;

    /// <inheritdoc />
    public string DisplayName { get; init; } = string.Empty;

    /// <inheritdoc />
    public IReadOnlyDictionary<LanguageKey, string>? I18nDisplayNames { get; init; }

    /// <inheritdoc />
    public string? DisplayNameKey { get; init; }

    /// <inheritdoc />
    public string? Description { get; init; }

    /// <inheritdoc />
    public string? DescriptionKey { get; init; }

    /// <inheritdoc />
    public string? GroupKey { get; init; }

    /// <inheritdoc />
    public int? DisplayOrder { get; init; }

    /// <inheritdoc />
    public bool IsVisibleInFrontManage { get; init; } = true;

    /// <inheritdoc />
    public bool IsV3LayoutWindow { get; init; }

    /// <inheritdoc />
    public bool Customizable { get; init; } = true;

    /// <inheritdoc />
    public FrontedWindowKind Kind => FrontedWindowKind.BuiltIn;

    /// <inheritdoc />
    public bool IsPlugin => false;

    /// <inheritdoc />
    public string? PackageId => null;

    /// <summary>
    /// 由 <see cref="Attributes.FrontedWindowInfo"/> 注册的具体 WPF 窗口类型。
    /// 仅用于未来的内置 XAML 前台窗口（当 <see cref="IsV3LayoutWindow"/> 为 <see langword="false"/> 时）。
    /// </summary>
    public Type? WindowType { get; init; }

    /// <summary>
    /// 从现有内置窗口属性元数据创建描述符。
    /// </summary>
    public static FrontedBuiltInWindowDescriptor FromInfo(FrontedWindowInfo info)
    {
        return new FrontedBuiltInWindowDescriptor
        {
            WindowId = info.Id,
            WindowTypeName = info.Name,
            DisplayName = info.Name,
            DisplayNameKey = $"Designer.Window.{info.Name}",
            GroupKey = "BuiltIn",
            DisplayOrder = GetBuiltInDisplayOrder(info.Name),
            WindowType = info.WindowType,
            IsV3LayoutWindow = false
        };
    }

    private static int GetBuiltInDisplayOrder(string windowTypeName)
    {
        return Enum.TryParse<FrontedWindowType>(windowTypeName, ignoreCase: false, out var windowType)
            ? (int)windowType * 100
            : int.MaxValue;
    }
}
