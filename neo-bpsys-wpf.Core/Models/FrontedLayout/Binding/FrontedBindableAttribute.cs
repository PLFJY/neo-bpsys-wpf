namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// 在设计器 v3 绑定目录中包含或自定义某个属性。
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
