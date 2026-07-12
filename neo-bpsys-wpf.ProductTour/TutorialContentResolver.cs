namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 从资源键解析本地化的步骤内容（标题、描述、对话台词）。
/// </summary>
/// <remarks>
/// Product Tour 库定义此抽象，以便宿主应用可以提供
/// 由其自身资源族（如 <c>TourContent.resx</c>）支持的解析器。
/// 默认实现原样返回键，适用于测试以及
/// 没有宿主资源族可用的设计时上下文。
/// </remarks>
public interface ITutorialContentResolver
{
    /// <summary>
    /// 从指定资源键解析单个本地化字符串。
    /// </summary>
    /// <param name="key">资源键。如果为 null 或空，则返回空字符串。</param>
    /// <returns>本地化字符串；若未找到翻译则返回键本身。</returns>
    string Resolve(string? key);

    /// <summary>
    /// 从单个资源键解析多行对话台词。
    /// 值会按换行符拆分。
    /// </summary>
    /// <param name="key">资源键。如果为 null 或空，则返回空列表。</param>
    /// <returns>解析得到的对话台词；若未找到翻译则返回仅包含键本身的列表。</returns>
    IReadOnlyList<string> ResolveLines(string? key);
}

/// <summary>
/// 默认内容解析器，原样返回键，不进行本地化。
/// </summary>
public sealed class DefaultTutorialContentResolver : ITutorialContentResolver
{
    /// <inheritdoc />
    public string Resolve(string? key) =>
        string.IsNullOrWhiteSpace(key) ? string.Empty : key;

    /// <inheritdoc />
    public IReadOnlyList<string> ResolveLines(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return [];
        }

        return [key];
    }
}
