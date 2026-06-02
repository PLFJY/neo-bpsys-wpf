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

    public FrontedDesignerLayoutCatalog()
    {
    }

    public FrontedDesignerLayoutCatalog(IFrontedWindowRegistry windowRegistry)
    {
        _windowRegistry = windowRegistry;
    }

    public IReadOnlyList<FrontedDesignerLayoutCatalogEntry> GetEntries()
    {
        if (_windowRegistry is null)
        {
            return GetFallbackEntries();
        }

        return _windowRegistry.GetCustomizableLayoutWindows()
            .SelectMany(descriptor => descriptor.Canvases
                .Where(canvas => canvas.Customizable)
                .Select(canvas => Create(descriptor, canvas)))
            .ToArray();
    }

    private static FrontedDesignerLayoutCatalogEntry Create(
        IFrontedWindowDescriptor descriptor,
        FrontedCanvasDescriptor canvas)
    {
        return new FrontedDesignerLayoutCatalogEntry
        {
            WindowTypeName = descriptor.FullWindowType,
            DisplayName = string.IsNullOrWhiteSpace(descriptor.DisplayName)
                ? descriptor.WindowTypeName
                : descriptor.DisplayName,
            WindowId = descriptor.WindowId,
            CanvasName = canvas.CanvasName,
            CanvasDisplayName = string.IsNullOrWhiteSpace(canvas.DisplayName)
                ? canvas.CanvasName
                : canvas.DisplayName,
            CanvasWidth = canvas.DefaultWidth,
            CanvasHeight = canvas.DefaultHeight,
            IsMigrated = true,
            IsEditable = descriptor.Kind is FrontedWindowKind.BuiltIn or FrontedWindowKind.PluginLayout
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
            Create(FrontedWindowType.WidgetsWindow, "WidgetsWindow", "MapBpCanvas"),
            Create(FrontedWindowType.WidgetsWindow, "WidgetsWindow", "BpOverViewCanvas"),
            Create(FrontedWindowType.WidgetsWindow, "WidgetsWindow", "MapV2Canvas"),
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
/// A single fronted window/canvas layout entry.
/// </summary>
public sealed class FrontedDesignerLayoutCatalogEntry
{
    public required string WindowTypeName { get; init; }

    public required string DisplayName { get; init; }

    public required string WindowId { get; init; }

    public required string CanvasName { get; init; }

    public required string CanvasDisplayName { get; init; }

    public double? CanvasWidth { get; init; }

    public double? CanvasHeight { get; init; }

    public bool IsMigrated { get; init; }

    public bool IsEditable { get; init; }
}
