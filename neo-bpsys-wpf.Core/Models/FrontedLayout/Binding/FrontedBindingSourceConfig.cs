namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// One ordered source in a text binding expression.
/// </summary>
public sealed class FrontedBindingSourceConfig
{
    /// <summary>
    /// Binding path relative to <see cref="Abstractions.Services.ISharedDataService"/>.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Optional designer-only display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Reserved per-source format.
    /// </summary>
    public string? Format { get; set; }
}
