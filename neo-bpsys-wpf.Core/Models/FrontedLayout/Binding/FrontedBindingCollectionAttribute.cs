namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// Describes collection expansion without reading runtime collection values.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FrontedBindingCollectionAttribute : Attribute
{
    /// <summary>
    /// 固定的集合元素数量，-1 表示不限。
    /// </summary>
    public int FixedCount { get; init; } = -1;

    /// <summary>
    /// 已知的键列表。
    /// </summary>
    public string[]? KnownKeys { get; init; }
}
