namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 前台行为文档，存储一个窗口的所有控件行为集。
/// </summary>
public sealed class FrontedBehaviorDocument
{
    /// <summary>
    /// 文档版本。
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 所属前台窗口类型。
    /// </summary>
    public string? WindowType { get; set; }

    /// <summary>
    /// 所属 Canvas 名称。
    /// </summary>
    public string? CanvasName { get; set; }

    /// <summary>
    /// 控件行为集列表。
    /// </summary>
    public List<ControlBehaviorSet> ControlBehaviorSets { get; set; } = [];

    /// <summary>
    /// 根据行为 GUID 查找控件行为集。
    /// </summary>
    /// <param name="behaviorGuid">行为 GUID。</param>
    /// <returns>找到的控件行为集，未找到时返回 null。</returns>
    public ControlBehaviorSet? FindSet(Guid behaviorGuid) =>
        ControlBehaviorSets.FirstOrDefault(set => set.BehaviorGuid == behaviorGuid);

    /// <summary>
    /// 获取或创建控件行为集。
    /// </summary>
    /// <param name="behaviorGuid">行为 GUID。</param>
    /// <param name="displayName">控件显示名称。</param>
    /// <returns>已存在或新创建的控件行为集。</returns>
    public ControlBehaviorSet GetOrCreateSet(Guid behaviorGuid, string? displayName = null)
    {
        var existing = FindSet(behaviorGuid);
        if (existing is not null)
        {
            return existing;
        }

        var created = new ControlBehaviorSet
        {
            BehaviorGuid = behaviorGuid,
            DisplayName = displayName
        };
        ControlBehaviorSets.Add(created);
        return created;
    }

    /// <summary>
    /// 移除控件行为集。
    /// </summary>
    /// <param name="behaviorGuid">行为 GUID。</param>
    /// <returns>是否成功移除。</returns>
    public bool RemoveSet(Guid behaviorGuid)
    {
        var existing = FindSet(behaviorGuid);
        return existing is not null && ControlBehaviorSets.Remove(existing);
    }
}

