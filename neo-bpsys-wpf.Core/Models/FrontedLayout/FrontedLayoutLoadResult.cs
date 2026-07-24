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
    /// 已加载的以窗口为中心的布局配置。由 <see cref="IFrontedLayoutService"/> 保证非空：
    /// 激活包、内置资源均缺失或损坏时返回内存空模板。
    /// </summary>
    public required FrontedWindowConfig Config { get; init; }

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
    /// <remarks>
    /// 当前实现中 <see cref="FrontedLayoutService"/> 的回退路径不会填充此字段，
    /// 因此该字段始终为 <see langword="null"/>。保留字段以便未来在回退路径中
    /// 收集加载错误详情，调用方可继续读取但当前不应期望获得非空值。
    /// </remarks>
    public string? Error { get; init; }
}
