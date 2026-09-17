using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 提供二次确认浮层的删除按钮控件。
/// </summary>
public class DeleteConfirmationButton : Control
{
    private ButtonBase? _triggerButton;
    private ButtonBase? _cancelButton;
    private ButtonBase? _confirmButton;

    static DeleteConfirmationButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DeleteConfirmationButton),
            new FrameworkPropertyMetadata(typeof(DeleteConfirmationButton)));
    }

    /// <summary>
    /// <see cref="ButtonText"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ButtonTextProperty = DependencyProperty.Register(
        nameof(ButtonText),
        typeof(string),
        typeof(DeleteConfirmationButton),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// 获取或设置删除按钮显示的文本。
    /// </summary>
    public string ButtonText
    {
        get => (string)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    /// <summary>
    /// <see cref="ConfirmationText"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ConfirmationTextProperty = DependencyProperty.Register(
        nameof(ConfirmationText),
        typeof(string),
        typeof(DeleteConfirmationButton),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// 获取或设置删除前显示的确认提示文本。
    /// </summary>
    public string ConfirmationText
    {
        get => (string)GetValue(ConfirmationTextProperty);
        set => SetValue(ConfirmationTextProperty, value);
    }

    /// <summary>
    /// <see cref="Command"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(DeleteConfirmationButton),
        new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置用户确认删除后执行的命令。
    /// </summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// <see cref="CommandParameter"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter),
        typeof(object),
        typeof(DeleteConfirmationButton),
        new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置传递给 <see cref="Command"/> 的参数。
    /// </summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>
    /// <see cref="IsConfirmationOpen"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsConfirmationOpenProperty = DependencyProperty.Register(
        nameof(IsConfirmationOpen),
        typeof(bool),
        typeof(DeleteConfirmationButton),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 获取或设置一个值，指示删除确认浮层是否打开。
    /// 此状态通常由控件内部管理，使用者无需绑定。
    /// </summary>
    public bool IsConfirmationOpen
    {
        get => (bool)GetValue(IsConfirmationOpenProperty);
        set => SetValue(IsConfirmationOpenProperty, value);
    }

    /// <summary>
    /// 应用控件模板并连接确认浮层的交互事件。
    /// </summary>
    public override void OnApplyTemplate()
    {
        DetachTemplateEventHandlers();
        base.OnApplyTemplate();

        _triggerButton = GetTemplateChild("PART_TriggerButton") as ButtonBase;
        _cancelButton = GetTemplateChild("PART_CancelButton") as ButtonBase;
        _confirmButton = GetTemplateChild("PART_ConfirmButton") as ButtonBase;

        if (_triggerButton is not null)
        {
            _triggerButton.Click += OnTriggerButtonClick;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.Click += OnCancelButtonClick;
        }

        if (_confirmButton is not null)
        {
            _confirmButton.Click += OnConfirmButtonClick;
        }
    }

    private void DetachTemplateEventHandlers()
    {
        if (_triggerButton is not null)
        {
            _triggerButton.Click -= OnTriggerButtonClick;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.Click -= OnCancelButtonClick;
        }

        if (_confirmButton is not null)
        {
            _confirmButton.Click -= OnConfirmButtonClick;
        }
    }

    private void OnTriggerButtonClick(object sender, RoutedEventArgs e)
    {
        IsConfirmationOpen = true;
    }

    private void OnCancelButtonClick(object sender, RoutedEventArgs e)
    {
        IsConfirmationOpen = false;
    }

    private void OnConfirmButtonClick(object sender, RoutedEventArgs e)
    {
        IsConfirmationOpen = false;
    }
}
