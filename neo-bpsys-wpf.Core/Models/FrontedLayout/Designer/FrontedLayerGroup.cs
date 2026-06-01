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

    public int ZIndex { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public ObservableCollection<FrontedControlDesignItem> Items { get; } = [];

    public int Count => Items.Count;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsDropTarget
    {
        get => _isDropTarget;
        set => SetProperty(ref _isDropTarget, value);
    }
}
