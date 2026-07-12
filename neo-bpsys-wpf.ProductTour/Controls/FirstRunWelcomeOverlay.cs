using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>
/// 全屏首次运行欢迎遮罩。
/// </summary>
public sealed class FirstRunWelcomeOverlay : Grid
{
    private readonly Border _card;
    private readonly TextBlock _title;
    private readonly TextBlock _description;
    private readonly TextBlock _languageLabel;
    private readonly ComboBox _languageComboBox;
    private readonly TextBlock _footnote;
    private readonly Button _startButton;
    private readonly Button _skipButton;
    private readonly ITutorialTextProvider _textProvider;
    private readonly ProductTourOptions _options;
    private readonly ITutorialAvatarProvider _avatarProvider;
    private readonly ITutorialLanguageService _languageService;
    private string _selectedLanguageOptionId;
    private SkipTutorialConfirmDialog? _confirmDialog;

    /// <summary>当用户开始教程时发生，携带所选的语言选项 id。</summary>
    public event EventHandler<string>? StartRequested;

    /// <summary>当用户确认跳过教程时发生。</summary>
    public event EventHandler? SkipConfirmed;

    /// <summary>初始化 <see cref="FirstRunWelcomeOverlay"/> 类的新实例。</summary>
    public FirstRunWelcomeOverlay()
        : this(new DefaultTutorialTextProvider(), new ProductTourOptions(), new NoOpTutorialAvatarProvider(),
             NoOpLanguageOptions(), new NoOpTutorialLanguageService())
    {
    }

    /// <summary>初始化 <see cref="FirstRunWelcomeOverlay"/> 类的新实例。</summary>
    /// <param name="textProvider">固定 UI 文本提供器。</param>
    /// <param name="options">Product Tour 显示选项。</param>
    /// <param name="languageOptions">由宿主应用提供的语言选项。</param>
    public FirstRunWelcomeOverlay(
        ITutorialTextProvider textProvider,
        ProductTourOptions options,
        IReadOnlyList<TutorialLanguageOption>? languageOptions = null)
        : this(textProvider, options, new NoOpTutorialAvatarProvider(), languageOptions, new NoOpTutorialLanguageService())
    {
    }

    /// <summary>初始化 <see cref="FirstRunWelcomeOverlay"/> 类的新实例。</summary>
    /// <param name="textProvider">固定 UI 文本提供器。</param>
    /// <param name="options">Product Tour 显示选项。</param>
    /// <param name="avatarProvider">教程头像提供器。</param>
    /// <param name="languageOptions">由宿主应用提供的语言选项。</param>
    /// <param name="languageService">用于热切换的教程语言服务。</param>
    public FirstRunWelcomeOverlay(
        ITutorialTextProvider textProvider,
        ProductTourOptions options,
        ITutorialAvatarProvider avatarProvider,
        IReadOnlyList<TutorialLanguageOption>? languageOptions = null,
        ITutorialLanguageService? languageService = null)
    {
        _textProvider = textProvider;
        _options = options;
        _avatarProvider = avatarProvider;
        _languageService = languageService ?? new NoOpTutorialLanguageService();
        var optionsList = languageOptions is { Count: > 0 } ? languageOptions : NoOpLanguageOptions();
        _selectedLanguageOptionId = optionsList.FirstOrDefault(option => option.IsSelected)?.Id ?? optionsList[0].Id;
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

        _languageLabel = new TextBlock
        {
            Text = _textProvider.LanguageLabel,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 28, 0, 0)
        };
        _languageLabel.Style = TryFindResource("ProductTourWelcomeDescriptionStyle") as Style;

        _languageComboBox = new ComboBox
        {
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _languageComboBox.Style = TryFindResource("ProductTourWelcomeLanguageComboBoxStyle") as Style;
        foreach (var option in optionsList)
        {
            var item = new ComboBoxItem
            {
                Content = string.IsNullOrWhiteSpace(option.NativeName) || string.Equals(option.DisplayName, option.NativeName, StringComparison.Ordinal)
                    ? option.DisplayName
                    : $"{option.DisplayName} / {option.NativeName}",
                Tag = option.Id
            };

            _languageComboBox.Items.Add(item);
            if (option.Id == _selectedLanguageOptionId)
            {
                _languageComboBox.SelectedItem = item;
            }
        }

        _languageComboBox.SelectionChanged += (_, _) =>
        {
            if (_languageComboBox.SelectedItem is ComboBoxItem { Tag: string optionId })
            {
                _selectedLanguageOptionId = optionId;
            }
        };

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
            StartRequested?.Invoke(this, _selectedLanguageOptionId);
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
            Children = { _title, _description, _languageLabel, _languageComboBox, _startButton, _footnote }
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
    /// 从文本提供器重新读取所有欢迎遮罩文本，使其在语言热切换后
    /// 反映当前区域性。
    /// </summary>
    private void RefreshWelcomeLanguage()
    {
        _title.Text = _textProvider.WelcomeTitle;
        _description.Text = _textProvider.WelcomeDescription;
        _languageLabel.Text = _textProvider.LanguageLabel;
        _startButton.Content = _textProvider.StartTour;
        _footnote.Text = _textProvider.RestartAvailableHint;
        _skipButton.Content = _textProvider.Skip;

        if (_confirmDialog != null)
        {
            Children.Remove(_confirmDialog);
            _confirmDialog = null;
        }
    }

    /// <summary>播放进入动画。</summary>
    /// <returns>在动画完成时完成的任务。</returns>
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

    /// <summary>播放退出动画。</summary>
    /// <returns>在动画完成时完成的任务。</returns>
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

    private static IReadOnlyList<TutorialLanguageOption> NoOpLanguageOptions() =>
    [
        new TutorialLanguageOption { Id = "System", DisplayName = "跟随系统", NativeName = "Follow system", IsSystemDefault = true, IsSelected = true },
        new TutorialLanguageOption { Id = "zh_Hans", DisplayName = "简体中文", NativeName = "简体中文" },
        new TutorialLanguageOption { Id = "en_US", DisplayName = "English", NativeName = "English" },
        new TutorialLanguageOption { Id = "ja_JP", DisplayName = "日本語", NativeName = "日本語" }
    ];
}
