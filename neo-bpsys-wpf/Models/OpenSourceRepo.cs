namespace neo_bpsys_wpf.Models;

/// <summary>
/// 表示一个开源仓库信息。
/// </summary>
public class OpenSourceRepo
{
    /// <summary>
    /// 开源仓库名称。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 开源仓库地址。
    /// </summary>
    public string Url { get; init; } = string.Empty;
}
