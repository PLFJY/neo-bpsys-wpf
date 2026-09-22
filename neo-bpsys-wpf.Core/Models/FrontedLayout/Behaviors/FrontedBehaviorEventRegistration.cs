namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 描述一个由宿主或插件注册的前台行为语义事件。
/// </summary>
public sealed class FrontedBehaviorEventRegistration
{
    /// <summary>
    /// 获取规范事件类型，例如 <c>plugin:top.example.overlay/ShowPlayerCard</c>。
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// 获取提供方作用域内的局部事件标识。
    /// </summary>
    public required string LocalEventId { get; init; }

    /// <summary>
    /// 获取插件包 ID；宿主内置事件为 <see langword="null"/>。
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// 获取事件是否由宿主内置代码提供。
    /// </summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// 获取直接显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 获取可选的宿主本地化键。
    /// </summary>
    public string DisplayNameKey { get; init; } = string.Empty;

    /// <summary>
    /// 获取直接描述文本。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 获取可选的描述本地化键。
    /// </summary>
    public string DescriptionKey { get; init; } = string.Empty;

    /// <summary>
    /// 获取稳定分类标识。
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// 获取直接分类显示名称。
    /// </summary>
    public string CategoryDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 获取可选的分类显示名称本地化键。
    /// </summary>
    public string CategoryDisplayNameKey { get; init; } = string.Empty;

    /// <summary>
    /// 获取分类内排序序号。
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// 获取事件所属的运行时触发链路。
    /// </summary>
    public FrontedBehaviorEventUsage SupportedUsages { get; init; } = FrontedBehaviorEventUsage.EventBus;

    /// <summary>
    /// 获取事件负载字段定义。
    /// </summary>
    public IReadOnlyList<FrontedBehaviorEventPayloadField> PayloadFields { get; init; } = [];
}
