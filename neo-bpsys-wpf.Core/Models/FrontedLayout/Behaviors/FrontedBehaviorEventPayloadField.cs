namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 前台行为事件负载字段定义。
/// </summary>
public sealed class FrontedBehaviorEventPayloadField
{
    /// <summary>
    /// 负载字段路径。
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称的本地化键。
    /// </summary>
    public string DisplayNameKey { get; set; } = string.Empty;

    /// <summary>
    /// 描述的本地化键。
    /// </summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型名称。
    /// </summary>
    public string TypeName { get; set; } = "string";

    /// <summary>
    /// 负载数据来源。
    /// </summary>
    public FrontedBehaviorPayloadSource Source { get; set; }

    /// <summary>
    /// 来源路径。
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// 是否为常用过滤器目标。
    /// </summary>
    public bool IsCommonFilterTarget { get; set; }

    /// <summary>
    /// 获取或设置此负载字段接受的稳定枚举名称列表。
    /// </summary>
    public List<string> EnumValues { get; set; } = [];
}
