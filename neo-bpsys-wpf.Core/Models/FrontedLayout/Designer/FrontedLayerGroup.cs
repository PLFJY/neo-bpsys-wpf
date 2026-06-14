using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 前台布局编辑器左侧图层面板中的同 ZIndex 控件分组。
/// </summary>
public class FrontedLayerGroup : ObservableObject
{
    private bool _isExpanded = true;
    private bool _isDropTarget;

    /// <summary>
    /// 层级序号。
    /// </summary>
    public int ZIndex { get; init; }

    /// <summary>
    /// 显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 分组内节点列表。
    /// </summary>
    public ObservableCollection<DesignerLayerNode> Items { get; } = [];

    /// <summary>
    /// 分组内节点数量。
    /// </summary>
    public int Count => Items.Count;

    /// <summary>
    /// 是否展开。
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    /// 是否为拖放目标。
    /// </summary>
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetProperty(ref _isDropTarget, value);
    }
}
