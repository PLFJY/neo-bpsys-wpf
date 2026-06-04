namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// Declares a binding root scanned by Designer v3.
/// </summary>
public sealed record FrontedBindingRootDescriptor(
    string Name,
    Type ValueType,
    string? DisplayNameKey = null,
    string? DescriptionKey = null,
    int? FixedCount = null,
    IReadOnlyList<string>? KnownKeys = null);
