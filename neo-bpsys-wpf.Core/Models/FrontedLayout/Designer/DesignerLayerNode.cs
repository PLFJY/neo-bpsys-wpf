using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Designer v3 图层面板中的节点类型。
/// </summary>
public enum DesignerLayerNodeKind
{
    /// <summary>
    /// 控件节点。
    /// </summary>
    Control,

    /// <summary>
    /// 固定 Part 节点，表示控件内部的固定部件（如 BorderedImage 的内层 Image）。
    /// </summary>
    Part,

    /// <summary>
    /// PartCollection 集合项节点，表示控件内部可变集合中的一个项（如 GlobalScoreRow 的某个 Cell）。
    /// </summary>
    CollectionItem
}

/// <summary>
/// 设计器 v3 使用的图层面板节点。控件节点表示顶层控件，Part/CollectionItem 节点表示控件内部的可编辑子部件。
/// </summary>
public class DesignerLayerNode : ObservableObject
{
    private bool _isSelected;

    /// <summary>
    /// 节点类型。
    /// </summary>
    public DesignerLayerNodeKind Kind { get; init; }

    /// <summary>
    /// 关联的控件设计项。控件节点为该控件本身；Part/CollectionItem 节点为所属父控件。
    /// </summary>
    public FrontedControlDesignItem? ControlItem { get; init; }

    /// <summary>
    /// 是否可被选中。
    /// </summary>
    public bool CanSelect { get; init; } = true;

    /// <summary>
    /// 是否可被重新排序。
    /// </summary>
    public bool CanReorder { get; init; }

    /// <summary>
    /// 显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 元数据信息。
    /// </summary>
    public string Metadata { get; init; } = string.Empty;

    /// <summary>
    /// 层级序号。
    /// </summary>
    public int ZIndex { get; init; }

    /// <summary>
    /// Part 节点的 Part 标识；仅当 <see cref="Kind"/> 为 <see cref="DesignerLayerNodeKind.Part"/> 时有效。
    /// </summary>
    public string? PartId { get; init; }

    /// <summary>
    /// CollectionItem 节点的集合标识；仅当 <see cref="Kind"/> 为 <see cref="DesignerLayerNodeKind.CollectionItem"/> 时有效。
    /// </summary>
    public string? CollectionId { get; init; }

    /// <summary>
    /// CollectionItem 节点的集合项唯一键；仅当 <see cref="Kind"/> 为 <see cref="DesignerLayerNodeKind.CollectionItem"/> 时有效。
    /// </summary>
    public string? ItemKey { get; init; }

    /// <summary>
    /// 控件节点下的子节点列表（Part/CollectionItem 节点）。控件节点可包含子节点；Part/CollectionItem 节点无子节点。
    /// </summary>
    public ObservableCollection<DesignerLayerNode> Children { get; } = [];

    /// <summary>
    /// 是否被选中。
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// Designer 子控件（Part/CollectionItem）的命中框与装饰器信息。
/// 由 ViewModel 构造，供 View 创建透明 hitbox、selection adorner 与 resize handles。
/// </summary>
/// <remarks>
/// 几何值（<see cref="Left"/>/<see cref="Top"/>/<see cref="Width"/>/<see cref="Height"/>）
/// 相对于父控件，View 需要叠加父控件的画布坐标得到绝对位置。
/// <see cref="CanMove"/>/<see cref="CanResize"/> 来自 <c>FrontedV3PartCapabilities</c>，
/// View 根据能力决定是否显示移动光标与缩放手柄。
/// </remarks>
public sealed class DesignerChildTargetInfo
{
    /// <summary>
    /// 父控件设计项。
    /// </summary>
    public required FrontedControlDesignItem ParentItem { get; init; }

    /// <summary>
    /// Part 标识或 PartCollection 标识。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// PartCollection 集合项唯一键；为 <see langword="null"/> 时表示固定 Part。
    /// </summary>
    public string? ItemKey { get; init; }

    /// <summary>
    /// 是否为 PartCollection 集合项（true）或固定 Part（false）。
    /// </summary>
    public required bool IsCollectionItem { get; init; }

    /// <summary>
    /// 子控件相对于父控件的左侧坐标。
    /// </summary>
    public required double Left { get; init; }

    /// <summary>
    /// 子控件相对于父控件的顶部坐标。
    /// </summary>
    public required double Top { get; init; }

    /// <summary>
    /// 子控件宽度。
    /// </summary>
    public required double Width { get; init; }

    /// <summary>
    /// 子控件高度。
    /// </summary>
    public required double Height { get; init; }

    /// <summary>
    /// 是否允许移动。
    /// </summary>
    public required bool CanMove { get; init; }

    /// <summary>
    /// 是否允许缩放。
    /// </summary>
    public required bool CanResize { get; init; }
}
