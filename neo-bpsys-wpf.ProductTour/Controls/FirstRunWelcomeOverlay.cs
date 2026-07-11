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
    private readonly TextBlock _title;
    private readonly TextBlock _description;
    private readonly TextBlock _footnote;
    private readonly Button _startButton;
    private readonly Button _skipButton;
    private readonly ITutorialTextProvider _textProvider;
    private readonly ProductTourOptions _options;
    private readonly ITutorialAvatarProvider _avatarProvider;
    private readonly ITutorialLanguageService _languageService;
    private SkipTutorialConfirmDialog? _confirmDialog;

    /// <summary>Occurs when the user starts the tutorial.</summary>
    public event EventHandler? StartRequested;

    /// <summary>Occurs when the user confirms skipping the tutorial.</summary>
    public event EventHandler? SkipConfirmed;

    /// <summary>Initializes a new instance of the <see cref="FirstRunWelcomeOverlay"/> class.</summary>
    public FirstRunWelcomeOverlay()
        : this(new DefaultTutorialTextProvider(), new ProductTourOptions(), new NoOpTutorialAvatarProvider(),
             new NoOpTutorialLanguageService())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FirstRunWelcomeOverlay"/> class.</summary>
    /// <param name="textProvider">Fixed UI text provider.</param>
    /// <param name="options">Product tour display options.</param>
    public FirstRunWelcomeOverlay(
        ITutorialTextProvider textProvider,
        ProductTourOptions options)
        : this(textProvider, options, new NoOpTutorialAvatarProvider(), new NoOpTutorialLanguageService())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FirstRunWelcomeOverlay"/> class.</summary>
    /// <param name="textProvider">Fixed UI text provider.</param>
    /// <param name="options">Product tour display options.</param>
    /// <param name="avatarProvider">Tutorial avatar provider.</param>
    /// <param name="languageService">Tutorial language service for hot-switching.</param>
    public FirstRunWelcomeOverlay(
        ITutorialTextProvider textProvider,
        ProductTourOptions options,
        ITutorialAvatarProvider avatarProvider,
        ITutorialLanguageService? languageService = null)
    {
        _textProvider = textProvider;
        _options = options;
        _avatarProvider = avatarProvider;
        _languageService = languageService ?? new NoOpTutorialLanguageService();
        Style = TryFindResource("ProductTourWelcomeOverlayStyle") as Style;
        Background = CreateMaskBrush(_options.WelcomeMaskOpacity);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Opacity = 0;
        Panel.SetZIndex(this, 10000);

        _skipButton = new Button
        {
            Content = _textProvider.Skip,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 20, 24, 0)
        };
        _skipButton.Style = TryFindResource("ProductTourSkipButtonStyle") as Style;
        Panel.SetZIndex(_skipButton, 2);
        _skipButton.Click += (_, _) => ShowConfirmDialog();

        _title = new TextBlock
        {
            Text = _textProvider.WelcomeTitle,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        _title.Style = TryFindResource("ProductTourWelcomeTitleStyle") as Style;

        _description = new TextBlock
        {
            Text = _textProvider.WelcomeDescription,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0)
        };
        _description.Style = TryFindResource("ProductTourWelcomeDescriptionStyle") as Style;

        _startButton = new Button
        {
            Content = _textProvider.StartTour,
            MinWidth = 140,
            Height = 38,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0)
        };
        _startButton.Style = TryFindResource("ProductTourPrimaryButtonStyle") as Style;
        _startButton.Click += async (_, _) =>
        {
            await FadeOutAsync();
            StartRequested?.Invoke(this, EventArgs.Empty);
        };

        _footnote = new TextBlock
        {
            Text = _textProvider.RestartAvailableHint,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0)
        };
        _footnote.Style = TryFindResource("ProductTourWelcomeFootnoteStyle") as Style;

        var contentPanel = new StackPanel
        {
            MaxWidth = 430,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _title, _description, _startButton, _footnote }
        };
        UIElement cardChild = contentPanel;
        var avatar = _options.ShowAvatar ? _avatarProvider.GetAvatar(TutorialAvatarPose.Idle) : null;
        if (avatar != null)
        {
            var avatarImage = new Image
            {
                Source = avatar.ImageSource,
                Width = _options.WelcomeAvatarWidth,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false,
                Margin = _options.AvatarMargin,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            avatarImage.Style = TryFindResource("ProductTourWelcomeAvatarImageStyle") as Style;

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(contentPanel, 0);
            Grid.SetColumn(avatarImage, 1);
            layout.Children.Add(contentPanel);
            layout.Children.Add(avatarImage);
            cardChild = layout;
        }

        _card = new Border
        {
            Name = "WelcomeCard",
            Style = TryFindResource("ProductTourWelcomeCardStyle") as Style,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 760,
            Width = avatar == null ? 620 : 760,
            RenderTransform = new TranslateTransform(0, _options.WelcomeCardInitialTranslateY),
            Child = cardChild
        };

        Children.Add(_skipButton);
        Children.Add(_card);

        _languageService.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => _languageService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(RefreshWelcomeLanguage));
    }

    /// <summary>
    /// Re-reads all welcome overlay text from the text provider so it reflects
    /// the current culture after a language hot-switch.
    /// </summary>
    private void RefreshWelcomeLanguage()
    {
        _title.Text = _textProvider.WelcomeTitle;
        _description.Text = _textProvider.WelcomeDescription;
        _startButton.Content = _textProvider.StartTour;
        _footnote.Text = _textProvider.RestartAvailableHint;
        _skipButton.Content = _textProvider.Skip;

        if (_confirmDialog != null)
        {
            Children.Remove(_confirmDialog);
            _confirmDialog = null;
        }
    }

    /// <summary>Plays the entrance animation.</summary>
    /// <returns>A task that completes when the animation finishes.</returns>
    public Task FadeInAsync()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storyboard = new Storyboard();
        var overlayOpacity = new DoubleAnimation(0, 1, _options.WelcomeFadeInDuration);
        Storyboard.SetTarget(overlayOpacity, this);
        Storyboard.SetTargetProperty(overlayOpacity, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(overlayOpacity);

        var cardOpacity = new DoubleAnimation(0, 1, _options.WelcomeCardEnterDuration);
        Storyboard.SetTarget(cardOpacity, _card);
        Storyboard.SetTargetProperty(cardOpacity, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(cardOpacity);

        var cardY = new DoubleAnimation(_options.WelcomeCardInitialTranslateY, 0, _options.WelcomeCardEnterDuration);
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
        var animation = new DoubleAnimation(0, _options.WelcomeFadeOutDuration) { From = Opacity };
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

        _confirmDialog = new SkipTutorialConfirmDialog(_textProvider, _options)
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

    private Brush CreateMaskBrush(double opacity)
    {
        if (TryFindResource("ProductTourFallbackMaskBrush") is Brush resourceBrush)
        {
            var clone = resourceBrush.Clone();
            clone.Opacity = Math.Clamp(opacity, 0, 1);
            return clone;
        }

        return new SolidColorBrush(Color.FromRgb(16, 16, 16)) { Opacity = Math.Clamp(opacity, 0, 1) };
    }
}
