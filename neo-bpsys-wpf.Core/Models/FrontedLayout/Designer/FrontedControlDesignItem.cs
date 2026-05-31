using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// v3 前台布局编辑器中的单个控件设计项。
/// </summary>
public class FrontedControlDesignItem : ObservableObject
{
    private string _name = string.Empty;
    private FrontedControlConfigBase _config = new();
    private bool _isSelected;
    private bool _isRuntimeCritical;
    private IReadOnlyList<FrontedLayoutValidationMessage> _validationMessages = [];

    /// <summary>
    /// 控件名，保存时写回 <see cref="FrontedCanvasConfig.Controls"/> 的 dictionary key。
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// 控件配置对象。配置对象本身不保存名称。
    /// </summary>
    public FrontedControlConfigBase Config
    {
        get => _config;
        set => SetProperty(ref _config, value);
    }

    /// <summary>
    /// 是否被编辑器选中。
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// 是否属于运行时关键名称。
    /// </summary>
    public bool IsRuntimeCritical
    {
        get => _isRuntimeCritical;
        set => SetProperty(ref _isRuntimeCritical, value);
    }

    /// <summary>
    /// 当前设计项相关校验消息。
    /// </summary>
    public IReadOnlyList<FrontedLayoutValidationMessage> ValidationMessages
    {
        get => _validationMessages;
        set => SetProperty(ref _validationMessages, value);
    }
}
