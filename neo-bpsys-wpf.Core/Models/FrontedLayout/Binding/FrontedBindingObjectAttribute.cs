namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// Marks a type as a Designer v3 binding catalog object.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class FrontedBindingObjectAttribute : Attribute
{
    public bool IncludePublicProperties { get; init; } = true;

    public int MaxDepth { get; init; } = 6;

    public string? DisplayNameKey { get; init; }

    public string? DescriptionKey { get; init; }
}
