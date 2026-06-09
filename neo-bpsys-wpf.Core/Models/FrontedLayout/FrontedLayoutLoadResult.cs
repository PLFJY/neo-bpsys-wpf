namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Source used when loading a v3 fronted layout.
/// </summary>
public enum FrontedLayoutSource
{
    User,
    BuiltIn,
    PluginDefault,
    MissingOrError
}

/// <summary>
/// Result metadata for loading a v3 fronted layout.
/// </summary>
public sealed class FrontedLayoutLoadResult
{
    /// <summary>
    /// Loaded window-centric layout config.
    /// </summary>
    public FrontedWindowConfig? Config { get; init; }

    /// <summary>
    /// Source that provided the loaded config.
    /// </summary>
    public FrontedLayoutSource Source { get; init; }

    /// <summary>
    /// Path used to load the config, when available.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Load error details collected before fallback, when available.
    /// </summary>
    public string? Error { get; init; }
}
