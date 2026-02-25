namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 通用识别区域布局定义（结构驱动）。
/// </summary>
public sealed class RegionLayoutDefinition
{
    /// <summary>
    /// 场景展示名（仅用于 UI 标题与提示）。
    /// </summary>
    public string SceneDisplayName { get; set; } = "Region";

    /// <summary>
    /// 根节点列表。每个根节点的 Rect 都是相对于整帧。
    /// </summary>
    public List<RegionLayoutNode> Roots { get; set; } = [];
}

/// <summary>
/// 通用识别区域节点。
/// Rect 总是相对于父节点；根节点相对于整帧。
/// </summary>
public sealed class RegionLayoutNode
{
    /// <summary>
    /// 节点唯一标识（建议在同一场景内稳定）。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 节点显示名称。
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型：单一元素或模板实例元素。
    /// </summary>
    public RegionLayoutNodeType NodeType { get; set; } = RegionLayoutNodeType.Single;

    /// <summary>
    /// 模板分组标识。相同分组可执行“一键应用模板”。
    /// 单一元素可为空。
    /// </summary>
    public string TemplateGroupId { get; set; } = string.Empty;

    /// <summary>
    /// 节点区域。根节点相对整帧；子节点相对父节点。
    /// </summary>
    public RelativeRect Rect { get; set; }

    /// <summary>
    /// 是否限制在父节点内编辑。
    /// </summary>
    public bool ClampToParent { get; set; } = true;

    /// <summary>
    /// 子节点集合（支持任意层级）。
    /// </summary>
    public List<RegionLayoutNode> Children { get; set; } = [];
}

/// <summary>
/// 通用节点类型。
/// </summary>
public enum RegionLayoutNodeType
{
    /// <summary>
    /// 普通单一元素，不参与模板批量应用。
    /// </summary>
    Single = 0,

    /// <summary>
    /// 模板实例元素，可与同模板组元素互相套用布局。
    /// </summary>
    TemplateItem = 1
}
