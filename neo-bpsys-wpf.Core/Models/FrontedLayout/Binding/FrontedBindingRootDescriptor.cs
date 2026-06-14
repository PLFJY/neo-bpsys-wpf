namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// Declares a binding root scanned by Designer v3.
/// </summary>
/// <param name="Name">绑定根的名称。</param>
/// <param name="ValueType">绑定根的值类型。</param>
/// <param name="DisplayNameKey">可选的本地化显示名称键。</param>
/// <param name="DescriptionKey">可选的本地化描述键。</param>
/// <param name="FixedCount">固定的集合元素数量。</param>
/// <param name="KnownKeys">已知的键列表。</param>
public sealed record FrontedBindingRootDescriptor(
    string Name,
    Type ValueType,
    string? DisplayNameKey = null,
    string? DescriptionKey = null,
    int? FixedCount = null,
    IReadOnlyList<string>? KnownKeys = null);
