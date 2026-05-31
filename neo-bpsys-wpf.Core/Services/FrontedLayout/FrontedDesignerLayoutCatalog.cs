using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Lists migrated v3 fronted layouts that the independent editor can open.
/// </summary>
public class FrontedDesignerLayoutCatalog
{
    private static readonly IReadOnlyList<FrontedDesignerLayoutCatalogEntry> Entries =
    [
        Create(FrontedWindowType.ScoreSurWindow, "ScoreSurWindow", "BaseCanvas"),
        Create(FrontedWindowType.ScoreHunWindow, "ScoreHunWindow", "BaseCanvas"),
        Create(FrontedWindowType.ScoreGlobalWindow, "ScoreGlobalWindow", "BaseCanvas"),
        Create(FrontedWindowType.CutSceneWindow, "CutSceneWindow", "BaseCanvas"),
        Create(FrontedWindowType.GameDataWindow, "GameDataWindow", "BaseCanvas"),
        Create(FrontedWindowType.WidgetsWindow, "WidgetsWindow", "MapBpCanvas"),
        Create(FrontedWindowType.WidgetsWindow, "WidgetsWindow", "BpOverViewCanvas"),
        Create(FrontedWindowType.WidgetsWindow, "WidgetsWindow", "MapV2Canvas"),
        Create(FrontedWindowType.BpWindow, "BpWindow", "BaseCanvas")
    ];

    /// <summary>
    /// Gets all editable migrated layout entries.
    /// </summary>
    public IReadOnlyList<FrontedDesignerLayoutCatalogEntry> GetEntries()
    {
        return Entries;
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
/// A single migrated fronted window/canvas layout entry.
/// </summary>
public sealed class FrontedDesignerLayoutCatalogEntry
{
    /// <summary>
    /// Fronted window type name, for example BpWindow.
    /// </summary>
    public required string WindowTypeName { get; init; }

    /// <summary>
    /// Display name for the window selector.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Fronted window runtime ID.
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// Canvas name, for example BaseCanvas.
    /// </summary>
    public required string CanvasName { get; init; }

    /// <summary>
    /// Display name for the canvas selector.
    /// </summary>
    public required string CanvasDisplayName { get; init; }

    /// <summary>
    /// Optional known canvas width.
    /// </summary>
    public double? CanvasWidth { get; init; }

    /// <summary>
    /// Optional known canvas height.
    /// </summary>
    public double? CanvasHeight { get; init; }

    /// <summary>
    /// Whether the layout has been migrated to v3 JSON.
    /// </summary>
    public bool IsMigrated { get; init; }

    /// <summary>
    /// Whether this entry is editable by the independent editor.
    /// </summary>
    public bool IsEditable { get; init; }
}
