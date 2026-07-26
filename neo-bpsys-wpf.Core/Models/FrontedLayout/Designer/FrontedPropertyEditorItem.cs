using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 设计器 v3 属性网格中的单个可编辑行。
/// </summary>
public class FrontedPropertyEditorItem : ObservableObject
{
    private string _displayName = string.Empty;
    private string _propertyName = string.Empty;
    private string? _description;
    private Type _propertyType = typeof(string);
    private FrontedPropertyEditorKind _editorKind;
    private object? _value;
    private string? _displayValue;
    private string? _editText;
    private Color _colorValue = FrontedPropertyColorHelper.FallbackColor;
    private bool _isReadOnly;
    private bool _isRequired;
    private bool _hasEditError;
    private string? _editError;
    private IReadOnlyList<string> _validationErrors = [];
    private IReadOnlyList<FrontedLayoutValidationMessage> _validationMessages = [];
    private IReadOnlyList<object>? _options;
    private string? _groupName;
    private string? _groupDisplayName;
    private bool _isGroupHeaderVisible;
    private bool _canBrowseBinding;
    private bool _canBrowseResource;
    private bool _isMultiSelectionMixedValue;
    private bool _isMultiSelectionBatchEditable = true;
    private bool _requiresExplicitCommit;
    private bool _canToggleInheritance;
    private bool _isInheritedFromParent;
    private string? _browseButtonText;
    private string? _browseDialogTitle;
    private FrontedBindingTargetKind _bindingTargetKind = FrontedBindingTargetKind.Any;
    private string? _expectedBindingTypeName;
    private IReadOnlyList<string> _allowedBindingTypeNames = [];

    /// <summary>
    /// 面向用户的行标签。
    /// </summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    /// 底层设计项或配置属性名称。
    /// </summary>
    public string PropertyName
    {
        get => _propertyName;
        set => SetProperty(ref _propertyName, value);
    }

    /// <summary>
    /// 属性的可选帮助文本。
    /// </summary>
    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// 底层 CLR 属性类型。
    /// </summary>
    public Type PropertyType
    {
        get => _propertyType;
        set => SetProperty(ref _propertyType, value);
    }

    /// <summary>
    /// 为该属性选择的编辑器类型。
    /// </summary>
    public FrontedPropertyEditorKind EditorKind
    {
        get => _editorKind;
        set => SetProperty(ref _editorKind, value);
    }

    /// <summary>
    /// 当前属性值。
    /// </summary>
    public object? Value
    {
        get => _value;
        set
        {
            if (!SetProperty(ref _value, value))
            {
                return;
            }

            if (EditorKind == FrontedPropertyEditorKind.Color)
            {
                SetProperty(
                    ref _colorValue,
                    FrontedPropertyColorHelper.TryParseArgbColor(value as string, out var color)
                        ? color
                        : FrontedPropertyColorHelper.FallbackColor,
                    nameof(ColorValue));
            }
        }
    }

    /// <summary>
    /// 只读值的可选面向用户显示文本。
    /// </summary>
    public string? DisplayValue
    {
        get => _displayValue;
        set => SetProperty(ref _displayValue, value);
    }

    /// <summary>
    /// 显式提交类文本行的用户编辑缓冲区。
    /// </summary>
    public string? EditText
    {
        get => _editText;
        set
        {
            if (!SetProperty(ref _editText, value))
            {
                return;
            }

            ClearEditError();
            if (EditorKind == FrontedPropertyEditorKind.Color
                && FrontedPropertyColorHelper.TryParseArgbColor(value, out var color))
            {
                SetProperty(ref _colorValue, color, nameof(ColorValue));
            }
        }
    }

    /// <summary>
    /// 颜色字符串行的 ColorPicker 友好值。
    /// </summary>
    public Color ColorValue
    {
        get => _colorValue;
        set
        {
            if (!SetProperty(ref _colorValue, value))
            {
                return;
            }

            var colorText = FrontedPropertyColorHelper.ToArgbString(value);
            Value = colorText;
            EditText = colorText;
        }
    }

    /// <summary>
    /// 指示该行是否为只读。
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => SetProperty(ref _isReadOnly, value);
    }

    private bool _isEditingDisabled;

    /// <summary>
    /// 指示该行的编辑器控件是否完全禁用交互（IsEnabled=false）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="IsReadOnly"/> 的区别：<see cref="IsReadOnly"/> 主要针对文本输入（光标可进入但不可修改），
    /// 而 <see cref="IsEditingDisabled"/> 通过 <c>IsEnabled=false</c> 完全禁用编辑器（包括 ComboBox/CheckBox/ColorPicker 等）。
    /// 典型场景：子控件属性"跟随父控件"时，编辑器应完全禁用，仅显示从父控件继承的值。
    /// </remarks>
    public bool IsEditingDisabled
    {
        get => _isEditingDisabled;
        set => SetProperty(ref _isEditingDisabled, value);
    }

    /// <summary>
    /// 指示该属性是否为必需。
    /// </summary>
    public bool IsRequired
    {
        get => _isRequired;
        set => SetProperty(ref _isRequired, value);
    }

    /// <summary>
    /// 指示最近一次显式文本提交是否未通过验证。
    /// </summary>
    public bool HasEditError
    {
        get => _hasEditError;
        set => SetProperty(ref _hasEditError, value);
    }

    /// <summary>
    /// 最近一次失败的显式文本提交的验证消息。
    /// </summary>
    public string? EditError
    {
        get => _editError;
        set => SetProperty(ref _editError, value);
    }

    /// <summary>
    /// 附加到此属性行的验证消息列表。
    /// </summary>
    public IReadOnlyList<string> ValidationErrors
    {
        get => _validationErrors;
        set => SetProperty(ref _validationErrors, value);
    }

    /// <summary>
    /// 附加到此属性行的、带有严重级别的验证消息列表。
    /// </summary>
    public IReadOnlyList<FrontedLayoutValidationMessage> ValidationMessages
    {
        get => _validationMessages;
        set => SetProperty(ref _validationMessages, value);
    }

    /// <summary>
    /// 枚举类编辑器的可选值列表。
    /// </summary>
    public IReadOnlyList<object>? Options
    {
        get => _options;
        set => SetProperty(ref _options, value);
    }

    /// <summary>
    /// 逻辑分组名称。
    /// </summary>
    public string? GroupName
    {
        get => _groupName;
        set => SetProperty(ref _groupName, value);
    }

    /// <summary>
    /// 用于显示的本地化分组标签。
    /// </summary>
    public string? GroupDisplayName
    {
        get => _groupDisplayName;
        set => SetProperty(ref _groupDisplayName, value);
    }

    /// <summary>
    /// 指示此行是否应显示其分组标题。
    /// </summary>
    public bool IsGroupHeaderVisible
    {
        get => _isGroupHeaderVisible;
        set => SetProperty(ref _isGroupHeaderVisible, value);
    }

    /// <summary>
    /// 指示此文本类行是否能打开绑定浏览器。
    /// </summary>
    public bool CanBrowseBinding
    {
        get => _canBrowseBinding;
        set => SetProperty(ref _canBrowseBinding, value);
    }

    /// <summary>
    /// 指示此文本类行是否能打开资源浏览器。
    /// </summary>
    public bool CanBrowseResource
    {
        get => _canBrowseResource;
        set => SetProperty(ref _canBrowseResource, value);
    }

    /// <summary>
    /// 获取或设置指示该行是否表示具有不同当前值的选中控件的值。
    /// </summary>
    public bool IsMultiSelectionMixedValue
    {
        get => _isMultiSelectionMixedValue;
        set => SetProperty(ref _isMultiSelectionMixedValue, value);
    }

    /// <summary>
    /// 获取或设置指示此行是否可对当前多选进行批量编辑的值。
    /// </summary>
    public bool IsMultiSelectionBatchEditable
    {
        get => _isMultiSelectionBatchEditable;
        set => SetProperty(ref _isMultiSelectionBatchEditable, value);
    }

    /// <summary>
    /// 获取或设置指示该行是否在写入配置前等待 Enter、应用或浏览器选择的值。
    /// </summary>
    public bool RequiresExplicitCommit
    {
        get => _requiresExplicitCommit;
        set => SetProperty(ref _requiresExplicitCommit, value);
    }

    /// <summary>
    /// 获取或设置指示该属性是否支持"跟随父控件 / 独立设定"切换的值。
    /// </summary>
    /// <remarks>
    /// 仅对 <see cref="neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer.FrontedV3PropertyInheritance.ParentFallback"/>
    /// 继承模式的子控件属性为 <see langword="true"/>。PropertyGrid 在该值为 <see langword="true"/> 时
    /// 显示一个 CheckBox，允许用户切换 override 与跟随父控件两种状态。
    /// </remarks>
    public bool CanToggleInheritance
    {
        get => _canToggleInheritance;
        set => SetProperty(ref _canToggleInheritance, value);
    }

    /// <summary>
    /// 获取或设置指示该属性当前是否从父控件继承（即子控件未设置 override）的值。
    /// </summary>
    /// <remarks>
    /// 当 <see cref="CanToggleInheritance"/> 为 <see langword="true"/> 时，该值驱动 PropertyGrid 中
    /// 切换 CheckBox 的选中状态：选中表示跟随父控件（无 override），未选中表示独立设定（有 override）。
    /// 切换该值时不会直接修改底层 Config，需由 ViewModel 命令处理清除或写入 override。
    /// </remarks>
    public bool IsInheritedFromParent
    {
        get => _isInheritedFromParent;
        set => SetProperty(ref _isInheritedFromParent, value);
    }

    /// <summary>
    /// 可选的短浏览按钮文本。
    /// </summary>
    public string? BrowseButtonText
    {
        get => _browseButtonText;
        set => SetProperty(ref _browseButtonText, value);
    }

    /// <summary>
    /// 可选的浏览对话框标题键。
    /// </summary>
    public string? BrowseDialogTitle
    {
        get => _browseDialogTitle;
        set => SetProperty(ref _browseDialogTitle, value);
    }

    /// <summary>
    /// 绑定浏览器过滤使用的预期绑定目标类别。
    /// </summary>
    public FrontedBindingTargetKind BindingTargetKind
    {
        get => _bindingTargetKind;
        set => SetProperty(ref _bindingTargetKind, value);
    }

    /// <summary>
    /// 预期绑定类型的短显示名称。
    /// </summary>
    public string? ExpectedBindingTypeName
    {
        get => _expectedBindingTypeName;
        set => SetProperty(ref _expectedBindingTypeName, value);
    }

    /// <summary>
    /// 绑定浏览器为此行接受的类型名称列表。
    /// </summary>
    public IReadOnlyList<string> AllowedBindingTypeNames
    {
        get => _allowedBindingTypeNames;
        set => SetProperty(ref _allowedBindingTypeNames, value);
    }

    /// <summary>
    /// 应用失败的编辑状态，不丢弃用户的编辑缓冲区。
    /// </summary>
    public void SetEditError(string message)
    {
        HasEditError = true;
        EditError = message;
    }

    private void ClearEditError()
    {
        if (!HasEditError && string.IsNullOrEmpty(EditError))
        {
            return;
        }

        HasEditError = false;
        EditError = null;
    }
}
