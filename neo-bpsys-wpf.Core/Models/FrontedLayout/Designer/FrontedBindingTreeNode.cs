namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Node displayed by Designer v3 Binding Browser.
/// </summary>
public sealed class FrontedBindingTreeNode
{
    /// <summary>
    /// 显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 完整绑定路径。
    /// </summary>
    public string? FullPath { get; init; }

    /// <summary>
    /// 类型名称。
    /// </summary>
    public string? TypeName { get; init; }

    /// <summary>
    /// 值类型。
    /// </summary>
    public Type? ValueType { get; init; }

    /// <summary>
    /// 子节点列表。
    /// </summary>
    public IReadOnlyList<FrontedBindingTreeNode> Children { get; init; } = [];

    /// <summary>
    /// 是否可被选中（有完整路径且无子节点）。
    /// </summary>
    public bool IsSelectable => !string.IsNullOrWhiteSpace(FullPath) && Children.Count == 0;

    /// <summary>
    /// 将树展开为平铺列表。
    /// </summary>
    /// <returns>平铺后的节点列表。</returns>
    public IEnumerable<FrontedBindingTreeNode> Flatten()
    {
        yield return this;
        foreach (var child in Children.SelectMany(child => child.Flatten()))
        {
            yield return child;
        }
    }
}
