using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>
/// 在跳过教程之前使用的遮罩内确认卡片。
/// </summary>
public sealed class SkipTutorialConfirmDialog : ContentControl
{
    /// <summary>标识 Title 依赖属性。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SkipTutorialConfirmDialog), new PropertyMetadata(new DefaultTutorialTextProvider().SkipConfirmTitle));

    /// <summary>标识 Message 依赖属性。</summary>
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(SkipTutorialConfirmDialog), new PropertyMetadata(new DefaultTutorialTextProvider().SkipConfirmDescription));

    private readonly ITutorialTextProvider _textProvider;
    private readonly ProductTourOptions _options;

    /// <summary>获取或设置对话框标题。</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>获取或设置对话框消息。</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>当用户确认跳过操作时发生。</summary>
    public event EventHandler? Confirmed;

    /// <summary>当用户取消跳过操作时发生。</summary>
    public event EventHandler? Canceled;

    /// <summary>初始化 <see cref="SkipTutorialConfirmDialog"/> 类的新实例。</summary>
    public SkipTutorialConfirmDialog()
        : this(new DefaultTutorialTextProvider(), new ProductTourOptions())
    {
    }

    /// <summary>初始化 <see cref="SkipTutorialConfirmDialog"/> 类的新实例。</summary>
    /// <param name="textProvider">固定 UI 文本提供器。</param>
    public SkipTutorialConfirmDialog(ITutorialTextProvider textProvider)
        : this(textProvider, new ProductTourOptions())
    {
    }

    /// <summary>初始化 <see cref="SkipTutorialConfirmDialog"/> 类的新实例。</summary>
    /// <param name="textProvider">固定 UI 文本提供器。</param>
    /// <param name="options">Product Tour 显示选项。</param>
    public SkipTutorialConfirmDialog(ITutorialTextProvider textProvider, ProductTourOptions options)
    {
        _textProvider = textProvider;
        _options = options;
        Title = _textProvider.SkipConfirmTitle;
        Message = _textProvider.SkipConfirmDescription;
        Focusable = true;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Canceled?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        };
        Loaded += (_, _) => Focus();
        BuildContent();
    }

    private void BuildContent()
    {
        var title = new TextBlock();
        title.Style = TryFindResource("ProductTourConfirmTitleStyle") as Style;
        title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Title)) { Source = this });

        var message = new TextBlock { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };
        message.Style = TryFindResource("ProductTourConfirmMessageStyle") as Style;
        message.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Message)) { Source = this });

        var continueButton = new Button { Content = _textProvider.SkipConfirmContinue, MinWidth = 110 };
        continueButton.Style = TryFindResource("ProductTourSecondaryButtonStyle") as Style;
        continueButton.Click += (_, _) => Canceled?.Invoke(this, EventArgs.Empty);

        var confirmButton = new Button { Content = _textProvider.SkipConfirmConfirm, MinWidth = 110, Margin = new Thickness(12, 0, 0, 0) };
        confirmButton.Style = TryFindResource("ProductTourPrimaryButtonStyle") as Style;
        confirmButton.Click += (_, _) => Confirmed?.Invoke(this, EventArgs.Empty);

        var buttons = new StackPanel
        {
            Margin = new Thickness(0, 20, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Orientation = Orientation.Horizontal,
            Children = { continueButton, confirmButton }
        };

        Content = new Border
        {
            Style = TryFindResource("ProductTourConfirmDialogStyle") as Style,
            MaxWidth = Math.Max(420, _options.CardWidth + 120),
            Child = new StackPanel { Children = { title, message, buttons } }
        };
    }
}
