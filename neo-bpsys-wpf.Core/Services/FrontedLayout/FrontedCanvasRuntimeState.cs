using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Designer v3 Canvas 状态已解析的运行时视图。
/// </summary>
public sealed class FrontedCanvasRuntimeState
{
    public required double CanvasWidth { get; init; }

    public required double CanvasHeight { get; init; }

    public string? BackgroundImage { get; init; }

    public required IReadOnlyList<FrontedPluginDependency> RequiredPlugins { get; init; }

    public required IReadOnlyDictionary<string, FrontedControlConfigBase> Controls { get; init; }

    public bool IsFallback { get; init; }
}
