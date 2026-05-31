namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Source used when loading a v3 fronted layout.
/// </summary>
public enum FrontedLayoutSource
{
    User,
    BuiltIn,
    MissingOrError
}

/// <summary>
/// Result metadata for loading a v3 fronted layout.
/// </summary>
public sealed class FrontedLayoutLoadResult
{
    public FrontedCanvasConfig? Config { get; init; }

    public FrontedLayoutSource Source { get; init; }

    public string? Path { get; init; }

    public string? Error { get; init; }
}
