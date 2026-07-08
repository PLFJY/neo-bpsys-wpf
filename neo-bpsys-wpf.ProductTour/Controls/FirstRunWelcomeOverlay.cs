using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>
/// Full-screen first-run welcome overlay.
/// </summary>
public sealed class FirstRunWelcomeOverlay : Grid
{
    private readonly Border _card;
    private readonly ComboBox _languageComboBox;
    private readonly ITutorialTextProvider _textProvider;
    private SkipTutorialConfirmDialog? _confirmDialog;

    /// <summary>Occurs when the user starts the tutorial.</summary>
    public event EventHandler<string>? StartRequested;

    /// <summary>Occurs when the user confirms skipping the tutorial.</summary>
    public event EventHandler? SkipConfirmed;

    /// <summary>Initializes a new instance of the <see cref="FirstRunWelcomeOverlay"/> class.</summary>
    public FirstRunWelcomeOverlay()
        : this(new DefaultTutorialTextProvider())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FirstRunWelcomeOverlay"/> class.</summary>
    /// <param name="textProvider">Fixed UI text provider.</param>
    public FirstRunWelcomeOverlay(ITutorialTextProvider textProvider)
    {
        _textProvider = textProvider;
        Style = TryFindResource("ProductTourWelcomeOverlayStyle") as Style;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Opacity = 0;
        Panel.SetZIndex(this, 10000);

        var skipButton = new Button
        {
            Content = _textProvider.Skip,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 20, 24, 0)
        };
        skipButton.Style = TryFindResource("ProductTourSkipButtonStyle") as Style;
        skipButton.Click += (_, _) => ShowConfirmDialog();

        var title = new TextBlock
        {
            Text = _textProvider.WelcomeTitle,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        title.Style = TryFindResource("ProductTourWelcomeTitleStyle") as Style;

        var description = new TextBlock
        {
            Text = _textProvider.WelcomeDescription,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0)
        };
        description.Style = TryFindResource("ProductTourWelcomeDescriptionStyle") as Style;

        _languageComboBox = new ComboBox
        {
            Width = 180,
            ItemsSource = new[] { "zh-CN", "en-US" },
            SelectedIndex = CultureInfo.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0
        };

        var languagePanel = new Grid
        {
            Margin = new Thickness(0, 26, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        languagePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        languagePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var languageLabel = new TextBlock
        {
            Text = _textProvider.LanguageLabel,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(languageLabel, 0);
        Grid.SetColumn(_languageComboBox, 1);
        languagePanel.Children.Add(languageLabel);
        languagePanel.Children.Add(_languageComboBox);

        var startButton = new Button
        {
            Content = _textProvider.StartTour,
            MinWidth = 140,
            Height = 38,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0)
        };
        startButton.Style = TryFindResource("ProductTourPrimaryButtonStyle") as Style;
        startButton.Click += async (_, _) =>
        {
            await FadeOutAsync();
            StartRequested?.Invoke(this, _languageComboBox.SelectedItem?.ToString() ?? "zh-CN");
        };

        var footnote = new TextBlock
        {
            Text = _textProvider.RestartAvailableHint,
            Opacity = 0.72,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0)
        };

        _card = new Border
        {
            Style = TryFindResource("ProductTourWelcomeCardStyle") as Style,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 620,
            Width = 620,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Effect = null,
            RenderTransform = new TranslateTransform(0, 16),
            Child = new StackPanel
            {
                MaxWidth = 620,
                Margin = new Thickness(0, 72, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { title, description, languagePanel, startButton, footnote }
            }
        };

        Children.Add(skipButton);
        Children.Add(_card);
    }

    /// <summary>Plays the entrance animation.</summary>
    /// <returns>A task that completes when the animation finishes.</returns>
    public Task FadeInAsync()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storyboard = new Storyboard();
        var overlayOpacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240));
        Storyboard.SetTarget(overlayOpacity, this);
        Storyboard.SetTargetProperty(overlayOpacity, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(overlayOpacity);

        var cardOpacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        Storyboard.SetTarget(cardOpacity, _card);
        Storyboard.SetTargetProperty(cardOpacity, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(cardOpacity);

        var cardY = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(300));
        Storyboard.SetTarget(cardY, _card);
        Storyboard.SetTargetProperty(cardY, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        storyboard.Children.Add(cardY);
        storyboard.Completed += (_, _) => source.TrySetResult();
        storyboard.Begin();
        return source.Task;
    }

    /// <summary>Plays the exit animation.</summary>
    /// <returns>A task that completes when the animation finishes.</returns>
    public Task FadeOutAsync()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(280)) { From = Opacity };
        animation.Completed += (_, _) => source.TrySetResult();
        BeginAnimation(OpacityProperty, animation);
        return source.Task;
    }

    private void ShowConfirmDialog()
    {
        if (_confirmDialog != null)
        {
            return;
        }

        _confirmDialog = new SkipTutorialConfirmDialog(_textProvider)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _confirmDialog.Canceled += (_, _) =>
        {
            Children.Remove(_confirmDialog);
            _confirmDialog = null;
        };
        _confirmDialog.Confirmed += async (_, _) =>
        {
            await FadeOutAsync();
            SkipConfirmed?.Invoke(this, EventArgs.Empty);
        };
        Children.Add(_confirmDialog);
    }
}
