namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// Marks a type as a Designer v3 binding catalog object.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class FrontedBindingObjectAttribute : Attribute
{
    /// <summary>
    /// 是否包含公开属性。
    /// </summary>
    public bool IncludePublicProperties { get; init; } = true;

    /// <summary>
    /// 最大深度。
    /// </summary>
    public int MaxDepth { get; init; } = 6;

    /// <summary>
    /// 可选的本地化显示名称键。
    /// </summary>
    public string? DisplayNameKey { get; init; }

    /// <summary>
    /// 可选的本地化描述键。
    /// </summary>
    public string? DescriptionKey { get; init; }
}
