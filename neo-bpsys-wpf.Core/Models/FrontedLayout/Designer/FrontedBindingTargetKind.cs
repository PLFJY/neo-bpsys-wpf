namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Expected value category for a Designer v3 binding target.
/// </summary>
public enum FrontedBindingTargetKind
{
    /// <summary>
    /// Accept any selectable binding value.
    /// </summary>
    Any,

    /// <summary>
    /// Accept string and numeric values suitable for text display.
    /// </summary>
    Text,

    /// <summary>
    /// Accept image source values.
    /// </summary>
    Image,

    /// <summary>
    /// Accept game progress enum values.
    /// </summary>
    GameProgress,

    /// <summary>
    /// Accept map enum values.
    /// </summary>
    Map,

    /// <summary>
    /// Accept boolean values.
    /// </summary>
    Boolean,

    /// <summary>
    /// Accept numeric values.
    /// </summary>
    Number,

    /// <summary>
    /// Accept string values.
    /// </summary>
    String,

    /// <summary>
    /// Accept talent model values.
    /// </summary>
    Talent,

    /// <summary>
    /// Accept trait model values.
    /// </summary>
    Trait
}
