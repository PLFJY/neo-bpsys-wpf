using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Context for attaching a behavior runtime to a fronted window.
/// </summary>
public sealed class FrontedBehaviorRuntimeContext
{
    /// <summary>
    /// Fronted window identifier.
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// Fronted window type name, e.g. "BpWindow".
    /// </summary>
    public required string WindowType { get; init; }

    /// <summary>
    /// Internal canvas name within the window. Window-centric v3 layouts always use <c>BaseCanvas</c>.
    /// </summary>
    public string CanvasName { get; init; } = FrontedLayoutConstants.BaseCanvasName;

    /// <summary>
    /// The rendered Canvas root element.
    /// </summary>
    public required Canvas RootCanvas { get; init; }

    /// <summary>
    /// Window layout configuration used for the current render.
    /// </summary>
    public required FrontedWindowConfig WindowConfig { get; init; }

    /// <summary>
    /// Shared data service instance for the application.
    /// </summary>
    public required ISharedDataService SharedDataService { get; init; }

    /// <summary>
    /// Whether this is a Designer preview context (not a real fronted window).
    /// </summary>
    public bool IsDesignerPreview { get; init; }

    /// <summary>
    /// Optional logger.
    /// </summary>
    public Microsoft.Extensions.Logging.ILogger? Logger { get; init; }
}
