using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>
/// In-overlay confirmation card used before skipping a tutorial.
/// </summary>
public sealed class SkipTutorialConfirmDialog : ContentControl
{
    /// <summary>Identifies the title dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SkipTutorialConfirmDialog), new PropertyMetadata(new DefaultTutorialTextProvider().SkipConfirmTitle));

    /// <summary>Identifies the message dependency property.</summary>
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(SkipTutorialConfirmDialog), new PropertyMetadata(new DefaultTutorialTextProvider().SkipConfirmDescription));

    private readonly ITutorialTextProvider _textProvider;

    /// <summary>Gets or sets the dialog title.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the dialog message.</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Occurs when the user confirms the skip action.</summary>
    public event EventHandler? Confirmed;

    /// <summary>Occurs when the user cancels the skip action.</summary>
    public event EventHandler? Canceled;

    /// <summary>Initializes a new instance of the <see cref="SkipTutorialConfirmDialog"/> class.</summary>
    public SkipTutorialConfirmDialog()
        : this(new DefaultTutorialTextProvider())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SkipTutorialConfirmDialog"/> class.</summary>
    /// <param name="textProvider">Fixed UI text provider.</param>
    public SkipTutorialConfirmDialog(ITutorialTextProvider textProvider)
    {
        _textProvider = textProvider;
        Title = _textProvider.SkipConfirmTitle;
        Message = _textProvider.SkipConfirmDescription;
        Style = TryFindResource("ProductTourConfirmDialogStyle") as Style;
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
        var title = new TextBlock { FontSize = 22, FontWeight = FontWeights.SemiBold };
        title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Title)) { Source = this });

        var message = new TextBlock { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };
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
            Style = TryFindResource("ProductTourWelcomeCardStyle") as Style,
            MaxWidth = 520,
            Child = new StackPanel { Children = { title, message, buttons } }
        };
    }
}
