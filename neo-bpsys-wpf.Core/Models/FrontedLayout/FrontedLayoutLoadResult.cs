namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 加载 v3 前台布局时使用的来源。
/// </summary>
public enum FrontedLayoutSource
{
    User,
    BuiltIn,
    EmptyTemplate
}

/// <summary>
/// 加载 v3 前台布局的结果元数据。
/// </summary>
public sealed class FrontedLayoutLoadResult
{
    /// <summary>
    /// 已加载的以窗口为中心的布局配置。
    /// </summary>
    public FrontedWindowConfig? Config { get; init; }

    /// <summary>
    /// 提供已加载配置的来源。
    /// </summary>
    public FrontedLayoutSource Source { get; init; }

    /// <summary>
    /// 用于加载配置的路径（若可用）。
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// 回退之前收集的加载错误详情（若可用）。
    /// </summary>
    public string? Error { get; init; }
}
