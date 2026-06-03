using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

public enum DesignerLayerNodeKind
{
    Control
}

/// <summary>
/// Layer panel node used by Designer v3. Nodes represent top-level controls only.
/// </summary>
public class DesignerLayerNode : ObservableObject
{
    private bool _isSelected;

    public DesignerLayerNodeKind Kind { get; init; }

    public FrontedControlDesignItem? ControlItem { get; init; }

    public bool CanSelect { get; init; } = true;

    public bool CanReorder { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Metadata { get; init; } = string.Empty;

    public int ZIndex { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
