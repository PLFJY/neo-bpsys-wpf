namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// Ordered multi-source binding expression used by Text and LocalizedText controls.
/// </summary>
public sealed class FrontedTextBindingExpression
{
    /// <summary>
    /// Ordered binding sources. Their indexes map to composite format placeholders.
    /// </summary>
    public List<FrontedBindingSourceConfig> Sources { get; set; } = [];

    /// <summary>
    /// Composite format, for example "{0} : {1}".
    /// </summary>
    public string? StringFormat { get; set; }

    /// <summary>
    /// Separator used when <see cref="StringFormat"/> is empty.
    /// </summary>
    public string JoinSeparator { get; set; } = string.Empty;

    /// <summary>
    /// Text substituted for null source values.
    /// </summary>
    public string? NullText { get; set; } = string.Empty;

    /// <summary>
    /// Text returned when a source is unavailable or formatting fails.
    /// </summary>
    public string? FallbackText { get; set; } = string.Empty;

    /// <summary>
    /// Returns the non-empty sources used by the runtime.
    /// </summary>
    public IReadOnlyList<FrontedBindingSourceConfig> GetActiveSources() =>
        Sources.Where(source => !string.IsNullOrWhiteSpace(source.Path)).ToArray();
}
