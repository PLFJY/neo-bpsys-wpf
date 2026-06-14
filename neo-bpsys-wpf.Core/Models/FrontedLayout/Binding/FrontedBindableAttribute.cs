namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// Includes or customizes a property in the Designer v3 binding catalog.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FrontedBindableAttribute : Attribute
{
    /// <summary>
    /// 可选的本地化显示名称键。
    /// </summary>
    public string? DisplayNameKey { get; init; }

    /// <summary>
    /// 可选的本地化描述键。
    /// </summary>
    public string? DescriptionKey { get; init; }

    /// <summary>
    /// 是否包含子节点。
    /// </summary>
    public bool IncludeChildren { get; init; } = true;

    /// <summary>
    /// 是否可在绑定浏览器中选中。
    /// </summary>
    public bool IsSelectable { get; init; }
}
