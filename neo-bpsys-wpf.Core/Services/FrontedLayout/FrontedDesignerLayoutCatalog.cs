using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Lists Designer v3 fronted layouts that the independent editor can open.
/// </summary>
public class FrontedDesignerLayoutCatalog
{
    private readonly IFrontedWindowRegistry? _windowRegistry;

    /// <summary>
    /// Initializes a catalog that uses the built-in fallback window list.
    /// </summary>
    public FrontedDesignerLayoutCatalog()
    {
    }

    /// <summary>
    /// Initializes a catalog backed by the fronted window registry.
    /// </summary>
    /// <param name="windowRegistry">Registry that provides customizable v3 layout windows.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="windowRegistry"/> is null.</exception>
    public FrontedDesignerLayoutCatalog(IFrontedWindowRegistry windowRegistry)
    {
        ArgumentNullException.ThrowIfNull(windowRegistry);
        _windowRegistry = windowRegistry;
    }

    /// <summary>
    /// Gets the window-centric layout entries that the Designer can open.
    /// </summary>
    /// <returns>Customizable v3 layout windows ordered by the registry, or fallback built-in entries.</returns>
    public IReadOnlyList<FrontedDesignerLayoutCatalogEntry> GetEntries()
    {
        if (_windowRegistry is null)
        {
            return GetFallbackEntries();
        }

        return _windowRegistry.GetCustomizableLayoutWindows()
            .Select(Create)
            .ToArray();
    }

    private static FrontedDesignerLayoutCatalogEntry Create(IFrontedWindowDescriptor descriptor)
    {
        return new FrontedDesignerLayoutCatalogEntry
        {
            WindowTypeName = descriptor.FullWindowType,
            DisplayName = string.IsNullOrWhiteSpace(descriptor.DisplayName)
                ? descriptor.WindowTypeName
                : descriptor.DisplayName,
            WindowId = descriptor.WindowId,
            CanvasName = FrontedLayoutConstants.BaseCanvasName,
            CanvasDisplayName = FrontedLayoutConstants.BaseCanvasName,
            CanvasWidth = null,
            CanvasHeight = null,
            IsMigrated = true,
            IsEditable = descriptor.IsV3LayoutWindow && descriptor.Customizable
        };
    }

    private static IReadOnlyList<FrontedDesignerLayoutCatalogEntry> GetFallbackEntries()
    {
        return
        [
            Create(FrontedWindowType.ScoreSurWindow, "ScoreSurWindow", "BaseCanvas"),
            Create(FrontedWindowType.ScoreHunWindow, "ScoreHunWindow", "BaseCanvas"),
            Create(FrontedWindowType.ScoreGlobalWindow, "ScoreGlobalWindow", "BaseCanvas"),
            Create(FrontedWindowType.CutSceneWindow, "CutSceneWindow", "BaseCanvas"),
            Create(FrontedWindowType.GameDataWindow, "GameDataWindow", "BaseCanvas"),
            Create(FrontedWindowType.BpOverviewWindow, "BpOverviewWindow", "BaseCanvas"),
            Create(FrontedWindowType.MapV2Window, "MapV2Window", "BaseCanvas"),
            Create(FrontedWindowType.BpWindow, "BpWindow", "BaseCanvas")
        ];
    }

    private static FrontedDesignerLayoutCatalogEntry Create(
        FrontedWindowType windowType,
        string windowTypeName,
        string canvasName)
    {
        return new FrontedDesignerLayoutCatalogEntry
        {
            WindowTypeName = windowTypeName,
            DisplayName = windowTypeName,
            WindowId = FrontedWindowHelper.GetFrontedWindowGuid(windowType),
            CanvasName = canvasName,
            CanvasDisplayName = canvasName,
            IsMigrated = true,
            IsEditable = true
        };
    }
}

/// <summary>
/// A single window-centric fronted layout entry.
/// </summary>
public sealed class FrontedDesignerLayoutCatalogEntry
{
    /// <summary>
    /// Full window type used by layout, behavior, and package paths.
    /// </summary>
    public required string WindowTypeName { get; init; }

    /// <summary>
    /// Display name shown in the Designer window selector.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Stable runtime window id.
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// Internal canvas name retained for temporary helper compatibility. Always <c>BaseCanvas</c>.
    /// </summary>
    public required string CanvasName { get; init; }

    /// <summary>
    /// Internal canvas display name retained for temporary helper compatibility. Always <c>BaseCanvas</c>.
    /// </summary>
    public required string CanvasDisplayName { get; init; }

    /// <summary>
    /// Optional design canvas width hint.
    /// </summary>
    public double? CanvasWidth { get; init; }

    /// <summary>
    /// Optional design canvas height hint.
    /// </summary>
    public double? CanvasHeight { get; init; }

    /// <summary>
    /// Whether the entry is backed by a migrated/window-centric layout.
    /// </summary>
    public bool IsMigrated { get; init; }

    /// <summary>
    /// Whether the Designer may save edits for this layout.
    /// </summary>
    public bool IsEditable { get; init; }
}
