using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 列出独立编辑器可打开的设计器 v3 前台布局。
/// </summary>
public class FrontedDesignerLayoutCatalog
{
    private readonly IFrontedWindowRegistry? _windowRegistry;

    /// <summary>
    /// 初始化使用内置回退窗口列表的目录。
    /// </summary>
    public FrontedDesignerLayoutCatalog()
    {
    }

    /// <summary>
    /// 初始化由前台窗口注册表支持的目录。
    /// </summary>
    /// <param name="windowRegistry">提供可自定义 v3 布局窗口的注册表。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="windowRegistry"/> 为 null 时抛出。</exception>
    public FrontedDesignerLayoutCatalog(IFrontedWindowRegistry windowRegistry)
    {
        ArgumentNullException.ThrowIfNull(windowRegistry);
        _windowRegistry = windowRegistry;
    }

    /// <summary>
    /// 获取设计器可打开的以窗口为中心的布局条目。
    /// </summary>
    /// <returns>按注册表排序的可自定义 v3 布局窗口，或回退的内置条目。</returns>
    public IReadOnlyList<FrontedDesignerLayoutCatalogEntry> GetEntries()
    {
        if (_windowRegistry is null)
        {
            return GetFallbackEntries();
        }

        return _windowRegistry.GetV3LayoutWindows()
            .Select(Create)
            .ToArray();
    }

    private static FrontedDesignerLayoutCatalogEntry Create(FrontedV3LayoutWindowRegistration registration)
    {
        return new FrontedDesignerLayoutCatalogEntry
        {
            WindowTypeName = registration.Id,
            DisplayName = string.IsNullOrWhiteSpace(registration.DisplayName)
                ? registration.LocalId
                : registration.DisplayName,
            I18nDisplayNames = registration.I18nDisplayNames,
            WindowId = registration.Id,
            CanvasWidth = null,
            CanvasHeight = null,
            IsMigrated = true,
            IsEditable = true
        };
    }

    private static IReadOnlyList<FrontedDesignerLayoutCatalogEntry> GetFallbackEntries()
    {
        return
        [
            Create(FrontedWindowType.ScoreSurWindow, "ScoreSurWindow"),
            Create(FrontedWindowType.ScoreHunWindow, "ScoreHunWindow"),
            Create(FrontedWindowType.ScoreGlobalWindow, "ScoreGlobalWindow"),
            Create(FrontedWindowType.CutSceneWindow, "CutSceneWindow"),
            Create(FrontedWindowType.GameDataWindow, "GameDataWindow"),
            Create(FrontedWindowType.BpOverviewWindow, "BpOverviewWindow"),
            Create(FrontedWindowType.MapV2Window, "MapV2Window"),
            Create(FrontedWindowType.BpWindow, "BpWindow")
        ];
    }

    private static FrontedDesignerLayoutCatalogEntry Create(
        FrontedWindowType windowType,
        string windowTypeName)
    {
        return new FrontedDesignerLayoutCatalogEntry
        {
            WindowTypeName = windowTypeName,
            DisplayName = windowTypeName,
            WindowId = FrontedWindowHelper.GetFrontedWindowGuid(windowType),
            IsMigrated = true,
            IsEditable = true
        };
    }
}

/// <summary>
/// 由设计器目录管理的单个以窗口为中心的前台布局条目。
/// </summary>
public sealed class FrontedDesignerLayoutCatalogEntry
{
    /// <summary>
    /// 布局、行为和包路径使用的完整窗口类型。
    /// </summary>
    public required string WindowTypeName { get; init; }

    /// <summary>
    /// 在设计器窗口选择器中显示的显示名称。
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 按具体应用语言索引的可选本地化显示名称。
    /// </summary>
    public IReadOnlyDictionary<LanguageKey, string>? I18nDisplayNames { get; init; }

    /// <summary>
    /// 稳定的运行时窗口标识。
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// 可选的设计画布宽度提示。
    /// </summary>
    public double? CanvasWidth { get; init; }

    /// <summary>
    /// 可选的设计画布高度提示。
    /// </summary>
    public double? CanvasHeight { get; init; }

    /// <summary>
    /// 该条目是否由已迁移/以窗口为中心的布局支持。
    /// </summary>
    public bool IsMigrated { get; init; }

    /// <summary>
    /// 设计器是否可以保存此布局的编辑。
    /// </summary>
    public bool IsEditable { get; init; }
}
