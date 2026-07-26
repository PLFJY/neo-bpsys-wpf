using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 列出独立编辑器可打开的设计器 v3 前台布局。
/// </summary>
/// <remarks>
/// 该目录只从 <see cref="IFrontedWindowRegistry.GetV3LayoutWindows"/> 获取窗口，
/// 不存在硬编码 fallback 或内置窗口清单。
/// </remarks>
public class FrontedDesignerLayoutCatalog
{
    private readonly IFrontedWindowRegistry _windowRegistry;

    /// <summary>
    /// 初始化由前台窗口注册表支持的目录。
    /// </summary>
    /// <param name="windowRegistry">提供可自定义 v3 布局窗口的注册表。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="windowRegistry"/> 为 <see langword="null"/> 时抛出。</exception>
    public FrontedDesignerLayoutCatalog(IFrontedWindowRegistry windowRegistry)
    {
        ArgumentNullException.ThrowIfNull(windowRegistry);
        _windowRegistry = windowRegistry;
    }

    /// <summary>
    /// 获取设计器可打开的以窗口为中心的布局条目。
    /// </summary>
    /// <returns>注册表中所有 v3 布局窗口对应的条目，按注册顺序返回。</returns>
    public IReadOnlyList<FrontedDesignerLayoutCatalogEntry> GetEntries()
    {
        return _windowRegistry.GetV3LayoutWindows()
            .Select(Create)
            .ToArray();
    }

    private static FrontedDesignerLayoutCatalogEntry Create(FrontedV3LayoutWindowRegistration registration)
    {
        return new FrontedDesignerLayoutCatalogEntry
        {
            CanonicalWindowId = registration.Id,
            DisplayName = string.IsNullOrWhiteSpace(registration.DisplayName)
                ? registration.LocalId
                : registration.DisplayName,
            CanvasWidth = null,
            CanvasHeight = null,
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
    /// 窗口的 Canonical ID。同时用于 layout / behavior 路径和运行时身份。
    /// </summary>
    public required string CanonicalWindowId { get; init; }

    /// <summary>
    /// 在设计器窗口选择器中显示的显示名称。
    /// </summary>
    public required string DisplayName { get; init; }

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
