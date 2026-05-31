namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Node displayed by Designer v3 Binding Browser.
/// </summary>
public sealed class FrontedBindingTreeNode
{
    public string DisplayName { get; init; } = string.Empty;

    public string? FullPath { get; init; }

    public string? TypeName { get; init; }

    public Type? ValueType { get; init; }

    public IReadOnlyList<FrontedBindingTreeNode> Children { get; init; } = [];

    public bool IsSelectable => !string.IsNullOrWhiteSpace(FullPath) && Children.Count == 0;

    public IEnumerable<FrontedBindingTreeNode> Flatten()
    {
        yield return this;
        foreach (var child in Children.SelectMany(child => child.Flatten()))
        {
            yield return child;
        }
    }
}
