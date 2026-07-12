using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Designer v3 图层面板中的节点类型。
/// </summary>
public enum DesignerLayerNodeKind
{
    /// <summary>
    /// 控件节点。
    /// </summary>
    Control
}

/// <summary>
/// 设计器 v3 使用的图层面板节点。节点仅表示顶层控件。
/// </summary>
public class DesignerLayerNode : ObservableObject
{
    private bool _isSelected;

    /// <summary>
    /// 节点类型。
    /// </summary>
    public DesignerLayerNodeKind Kind { get; init; }

    /// <summary>
    /// 关联的控件设计项。
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
    /// 是否被选中。
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
