namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// Describes collection expansion without reading runtime collection values.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FrontedBindingCollectionAttribute : Attribute
{
    public int FixedCount { get; init; } = -1;

    public string[]? KnownKeys { get; init; }
}
