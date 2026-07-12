namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

/// <summary>
/// 文本和本地化文本控件使用的有序多源绑定表达式。
/// </summary>
public sealed class FrontedTextBindingExpression
{
    /// <summary>
    /// 有序的绑定源列表。其索引映射到复合格式的占位符。
    /// </summary>
    public List<FrontedBindingSourceConfig> Sources { get; set; } = [];

    /// <summary>
    /// 复合格式，例如 "{0} : {1}"。
    /// </summary>
    public string? StringFormat { get; set; }

    /// <summary>
    /// 当 <see cref="StringFormat"/> 为空时使用的分隔符。
    /// </summary>
    public string JoinSeparator { get; set; } = string.Empty;

    /// <summary>
    /// 用于替换 null 源值的文本。
    /// </summary>
    public string? NullText { get; set; } = string.Empty;

    /// <summary>
    /// 当源不可用或格式化失败时返回的文本。
    /// </summary>
    public string? FallbackText { get; set; } = string.Empty;

    /// <summary>
    /// 返回运行时使用的非空源列表。
    /// </summary>
    public IReadOnlyList<FrontedBindingSourceConfig> GetActiveSources() =>
        Sources.Where(source => !string.IsNullOrWhiteSpace(source.Path)).ToArray();
}
