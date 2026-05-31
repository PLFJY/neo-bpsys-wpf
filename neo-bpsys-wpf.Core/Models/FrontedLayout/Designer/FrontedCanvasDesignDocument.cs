using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// v3 前台布局编辑器中的单 Canvas 设计文档。
/// </summary>
public class FrontedCanvasDesignDocument : ObservableObject
{
    private string _windowTypeName = string.Empty;
    private string _canvasName = string.Empty;
    private FrontedCanvasConfig _canvasConfig = new();
    private ObservableCollection<FrontedControlDesignItem> _controls = [];
    private bool _isDirty;

    /// <summary>
    /// 前台窗口类型名，例如 BpWindow。
    /// </summary>
    public string WindowTypeName
    {
        get => _windowTypeName;
        set => SetProperty(ref _windowTypeName, value);
    }

    /// <summary>
    /// Canvas 名称，例如 BaseCanvas。
    /// </summary>
    public string CanvasName
    {
        get => _canvasName;
        set => SetProperty(ref _canvasName, value);
    }

    /// <summary>
    /// Canvas 级配置。
    /// </summary>
    public FrontedCanvasConfig CanvasConfig
    {
        get => _canvasConfig;
        set => SetProperty(ref _canvasConfig, value);
    }

    /// <summary>
    /// 控件设计项集合，适合后续 ObservableCollection 绑定。
    /// </summary>
    public ObservableCollection<FrontedControlDesignItem> Controls
    {
        get => _controls;
        set => SetProperty(ref _controls, value);
    }

    /// <summary>
    /// 当前文档是否有未保存修改。
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }
}
