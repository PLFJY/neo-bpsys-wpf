namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// Includes or customizes a property in the Designer v3 binding catalog.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FrontedBindableAttribute : Attribute
{
    public string? DisplayNameKey { get; init; }

    public string? DescriptionKey { get; init; }

    public bool IncludeChildren { get; init; } = true;

    public bool IsSelectable { get; init; }
}
