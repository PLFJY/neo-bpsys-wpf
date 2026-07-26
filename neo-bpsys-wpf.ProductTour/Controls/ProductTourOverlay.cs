using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ContentDialogHost = Wpf.Ui.Controls.ContentDialogHost;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>
/// 表示结束某个产品导览步骤的用户操作。
/// </summary>
public enum ProductTourStepAction
{
    /// <summary>用户请求下一步或完成了最后一步。</summary>
    Next,
    /// <summary>用户请求上一步。</summary>
    Previous,
    /// <summary>用户请求跳过当前教程。</summary>
    Skip,
    /// <summary>步骤被取消。</summary>
    Cancel,
    /// <summary>步骤打开了一个拥有教程的子窗口并让出回放控制。</summary>
    ChildWindowHandoff,
    /// <summary>用户仅跳过当前播放。</summary>
    SkipForCurrentSession,
    /// <summary>用户永久跳过教程。</summary>
    SkipPermanently
}

/// <summary>
/// 指向目标控件的聚光灯式产品导览遮罩。
/// </summary>
public sealed class ProductTourOverlay : Canvas
{
    private readonly Border _blockAllMask;
    private readonly Border _topMask;
    private readonly Border _leftMask;
    private readonly Border _rightMask;
    private readonly Border _bottomMask;
    private readonly Border _spotlight;
    private readonly Border _card;
    private readonly TextBlock _title;
    private readonly TextBlock _description;
    private readonly TextBlock _progress;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _globalSkipButton;
    private readonly Image _avatarImage;
    private readonly TextBlock _waitingText;
    private readonly TextBlock _errorText;
    private readonly ITutorialTextProvider _textProvider;
    private readonly ProductTourOptions _options;
    private readonly ITutorialAvatarProvider _avatarProvider;
    private readonly ITutorialContentResolver _contentResolver;
    private readonly ITutorialLanguageService _languageService;
    private readonly ProductTourOverlayLayoutEngine _layoutEngine = new();
    private TaskCompletionSource<ProductTourStepAction>? _completion;
    private bool _signalReceived;
    private FrameworkElement? _currentOwner;
    private FrameworkElement? _currentTarget;
    private ProductTourStep? _currentStep;
    private int _currentStepIndex;
    private int _currentStepCount;
    private ProductTourPlacement _currentPlacement = ProductTourPlacement.Center;
    private ProductTourInteractionMode _currentInteractionMode = ProductTourInteractionMode.BlockAll;
    private ProductTourAvatarPlacement _currentAvatarPlacement = ProductTourAvatarPlacement.Auto;
    private TutorialAvatarPose? _currentAvatarPose;
    private Point _currentCardOffset;
    private readonly ITutorialSessionSuppression _sessionSuppression;

    /// <summary>初始化 <see cref="ProductTourOverlay"/> 类的新实例。</summary>
    public ProductTourOverlay()
        : this(new DefaultTutorialTextProvider(), new ProductTourOptions(), new NoOpTutorialAvatarProvider(),
             new DefaultTutorialContentResolver(), new NoOpTutorialLanguageService())
    {
    }

    /// <summary>初始化 <see cref="ProductTourOverlay"/> 类的新实例。</summary>
    /// <param name="textProvider">固定 UI 文本提供器。</param>
    /// <param name="options">Product Tour 显示选项。</param>
    public ProductTourOverlay(ITutorialTextProvider textProvider, ProductTourOptions options)
        : this(textProvider, options, new NoOpTutorialAvatarProvider(),
             new DefaultTutorialContentResolver(), new NoOpTutorialLanguageService())
    {
    }

    /// <summary>初始化 <see cref="ProductTourOverlay"/> 类的新实例。</summary>
    /// <param name="textProvider">固定 UI 文本提供器。</param>
    /// <param name="options">Product Tour 显示选项。</param>
    /// <param name="avatarProvider">教程头像提供器。</param>
    /// <param name="contentResolver">用于本地化步骤文本的教程内容解析器。</param>
    /// <param name="languageService">用于热切换的教程语言服务。</param>
    public ProductTourOverlay(
        ITutorialTextProvider textProvider,
        ProductTourOptions options,
        ITutorialAvatarProvider avatarProvider,
        ITutorialContentResolver? contentResolver = null,
        ITutorialLanguageService? languageService = null,
        ITutorialSessionSuppression? sessionSuppression = null)
    {
        _textProvider = textProvider;
        _options = options;
        _avatarProvider = avatarProvider;
        _contentResolver = contentResolver ?? new DefaultTutorialContentResolver();
        _languageService = languageService ?? new NoOpTutorialLanguageService();
        _sessionSuppression = sessionSuppression ?? new TutorialSessionSuppression();
        Style = TryFindResource("ProductTourOverlayStyle") as Style;
        Panel.SetZIndex(this, 10000);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Opacity = 0;
        Background = null;

        _blockAllMask = CreateMask("BlockAllMask");
        _topMask = CreateMask("TopMask");
        _leftMask = CreateMask("LeftMask");
        _rightMask = CreateMask("RightMask");
        _bottomMask = CreateMask("BottomMask");
        Children.Add(_blockAllMask);
        Children.Add(_topMask);
        Children.Add(_leftMask);
        Children.Add(_rightMask);
        Children.Add(_bottomMask);

        _spotlight = new Border
        {
            Name = "Spotlight",
            Style = TryFindResource("ProductTourSpotlightStyle") as Style,
            CornerRadius = new CornerRadius(_options.SpotlightCornerRadius),
            IsHitTestVisible = false
        };
        Children.Add(_spotlight);

        _title = new TextBlock();
        _title.Style = TryFindResource("ProductTourCardTitleStyle") as Style;
        _description = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        _description.Style = TryFindResource("ProductTourCardDescriptionStyle") as Style;
        _progress = new TextBlock { Margin = new Thickness(0, 10, 0, 0), Opacity = 0.72 };
        _progress.Style = TryFindResource("ProductTourCardProgressStyle") as Style;
        _waitingText = new TextBlock { Text = _textProvider.WaitingForAction, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        _waitingText.Style = TryFindResource("ProductTourCardWaitingStyle") as Style;
        _errorText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _errorText.Style = TryFindResource("ProductTourCardErrorStyle") as Style;

        _previousButton = new Button { Content = _textProvider.Previous, MinWidth = 78 };
        _previousButton.Style = TryFindResource("ProductTourSecondaryButtonStyle") as Style;
        _previousButton.Click += (_, _) => _completion?.TrySetResult(ProductTourStepAction.Previous);

        _nextButton = new Button { Content = _textProvider.Next, MinWidth = 78, Margin = new Thickness(8, 0, 0, 0) };
        _nextButton.Style = TryFindResource("ProductTourPrimaryButtonStyle") as Style;
        _nextButton.Click += (_, _) => _completion?.TrySetResult(ProductTourStepAction.Next);

        _globalSkipButton = new Button
        {
            Content = _textProvider.Skip,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            MinWidth = 70,
            Margin = new Thickness(0, 20, 24, 0)
        };
        _globalSkipButton.Style = TryFindResource("ProductTourSkipButtonStyle") as Style;
        _globalSkipButton.Click += (_, _) => ShowConfirmDialog();
        Panel.SetZIndex(_globalSkipButton, 4);
        Children.Add(_globalSkipButton);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { _previousButton, _nextButton }
        };

        var bottomBar = buttons;
        bottomBar.Margin = new Thickness(0, 12, 0, 0);

        _card = new Border
        {
            Name = "Card",
            Style = TryFindResource("ProductTourCardStyle") as Style,
            Width = _options.CardWidth,
            MaxHeight = _options.CardMaxHeight,
            Child = new StackPanel { Children = { _title, _description, _progress, _waitingText, _errorText, bottomBar } }
        };
        Children.Add(_card);

        _avatarImage = new Image
        {
            Width = _options.ProductTourAvatarWidth,
            Stretch = Stretch.Uniform,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        _avatarImage.Style = TryFindResource("ProductTourCardAvatarImageStyle") as Style;
        Children.Add(_avatarImage);

        _languageService.LanguageChanged += OnLanguageChanged;

        SizeChanged += (_, _) => LayoutCurrent(_currentOwner, _currentTarget, _currentPlacement);
        Unloaded += (_, _) => _languageService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(RefreshLanguage));
    }

    /// <summary>
    /// 使用当前区域性从资源键重新解析所有已显示的文本，
    /// 刷新标题、描述、进度、按钮和等待文本。
    /// </summary>
    private void RefreshLanguage()
    {
        if (_currentStep is null)
        {
            return;
        }

        var step = _currentStep;
        _title.Text = !string.IsNullOrWhiteSpace(step.TitleKey)
            ? _contentResolver.Resolve(step.TitleKey)
            : step.Title;
        _description.Text = !string.IsNullOrWhiteSpace(step.DescriptionKey)
            ? _contentResolver.Resolve(step.DescriptionKey)
            : step.Description;

        _previousButton.Content = _textProvider.Previous;
        _globalSkipButton.Content = _textProvider.Skip;
        UpdateNextButtonPresentation();
        _waitingText.Text = _textProvider.WaitingForAction;


        LayoutCurrent(_currentOwner, _currentTarget, _currentPlacement);
    }

    /// <summary>显示一个产品导览步骤。</summary>
    /// <param name="step">步骤定义。</param>
    /// <param name="target">目标元素。</param>
    /// <param name="context">步骤上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>结束该步骤的用户操作。</returns>
    public async Task<ProductTourStepAction> ShowStepAsync(
        ProductTourStep step,
        FrameworkElement? target,
        ProductTourStepContext context,
        CancellationToken cancellationToken)
    {
        _completion = new TaskCompletionSource<ProductTourStepAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        _signalReceived = string.IsNullOrWhiteSpace(step.WaitForSignalId);
        _currentOwner = context.Owner;
        _currentTarget = target;
        _currentStep = step;
        _currentStepIndex = context.StepIndex;
        _currentStepCount = context.StepCount;
        _currentPlacement = step.Placement;
        _currentInteractionMode = step.InteractionMode;
        _currentAvatarPlacement = step.AvatarPlacement;
        _currentAvatarPose = step.AvatarPose;
        _currentCardOffset = step.CardOffset;
        _title.Text = !string.IsNullOrWhiteSpace(step.TitleKey)
            ? _contentResolver.Resolve(step.TitleKey)
            : step.Title;
        _description.Text = !string.IsNullOrWhiteSpace(step.DescriptionKey)
            ? _contentResolver.Resolve(step.DescriptionKey)
            : step.Description;
        _progress.Text = $"{context.StepIndex + 1} / {context.StepCount}";
        _progress.Visibility = _options.ShowStepProgress ? Visibility.Visible : Visibility.Collapsed;
        _previousButton.IsEnabled = context.StepIndex > 0;
        _previousButton.Visibility = context.StepIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
        _nextButton.IsEnabled = _signalReceived;
        _nextButton.Visibility = Visibility.Visible;
        UpdateNextButtonPresentation();
        _waitingText.Visibility = _signalReceived ? Visibility.Collapsed : Visibility.Visible;
        _errorText.Visibility = Visibility.Collapsed;
        _errorText.Text = string.Empty;

        LayoutCurrent(context.Owner, target, step.Placement);
        await FadeInAsync();

        await using var registration = cancellationToken.Register(() => _completion.TrySetResult(ProductTourStepAction.Cancel));
        return await _completion.Task;
    }

    /// <summary>
    /// 强制以指定操作完成当前步骤，绕过正常的用户交互。
    /// 由回放协调器在子窗口交接期间让出父步骤时使用。
    /// </summary>
    /// <param name="action">用于强制完成该步骤的操作。</param>
    public void ForceComplete(ProductTourStepAction action) => _completion?.TrySetResult(action);

    /// <summary>标记所等待的步骤动作已完成。</summary>
    public void MarkSignalCompleted()
    {
        _signalReceived = true;
        _nextButton.IsEnabled = true;
        _nextButton.Visibility = Visibility.Visible;
        UpdateNextButtonPresentation();
        _waitingText.Visibility = Visibility.Collapsed;
        _errorText.Visibility = Visibility.Collapsed;
        _errorText.Text = string.Empty;
        _completion?.TrySetResult(ProductTourStepAction.Next);
    }

    /// <summary>标记所等待的步骤动作已超时，并让用户决定如何继续。</summary>
    /// <param name="message">可读的超时消息。</param>
    public void MarkSignalTimedOut(string message)
    {
        _signalReceived = false;
        _nextButton.IsEnabled = false;
        _nextButton.Visibility = Visibility.Visible;
        UpdateNextButtonPresentation();
        _waitingText.Visibility = Visibility.Collapsed;
        _errorText.Text = message;
        _errorText.Visibility = Visibility.Visible;
    }

    private void UpdateNextButtonPresentation()
    {
        var isWaitingForSignal = !_signalReceived;
        _nextButton.Content = isWaitingForSignal || _currentStepIndex < _currentStepCount - 1
            ? _textProvider.Next
            : _textProvider.Finish;
        _nextButton.Style = TryFindResource(
            isWaitingForSignal ? "ProductTourSecondaryButtonStyle" : "ProductTourPrimaryButtonStyle") as Style;
    }

    /// <summary>播放退出动画。</summary>
    /// <returns>在动画完成时完成的任务。</returns>
    public Task FadeOutAsync()
    {
        return AnimateOpacityAsync(0, _options.OverlayFadeOutDuration);
    }

    private Task FadeInAsync()
    {
        return AnimateOpacityAsync(1, _options.OverlayFadeInDuration, 0);
    }

    private async Task AnimateOpacityAsync(double to, TimeSpan duration, double? from = null)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new DoubleAnimation(to, duration) { From = from ?? Opacity };
        animation.Completed += (_, _) => source.TrySetResult();
        BeginAnimation(OpacityProperty, animation);
        await Task.WhenAny(source.Task, Task.Delay(duration + TimeSpan.FromMilliseconds(80)));
        BeginAnimation(OpacityProperty, null);
        Opacity = to;
    }

    private Border CreateMask(string name)
    {
        var mask = new Border
        {
            Name = name,
            Background = CreateMaskBrush(_options.ProductTourMaskOpacity),
            IsHitTestVisible = true
        };
        return mask;
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

    private void LayoutCurrent(FrameworkElement? owner, FrameworkElement? target, ProductTourPlacement placement)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _globalSkipButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(_globalSkipButton, Math.Max(0, width - _globalSkipButton.DesiredSize.Width - 24));
        SetTop(_globalSkipButton, 20);

        Rect targetRect;
        if (target != null)
        {
            try
            {
                var point = target.TransformToVisual(this).Transform(new Point(0, 0));
                targetRect = new Rect(point.X, point.Y, target.ActualWidth, target.ActualHeight);
            }
            catch (Exception)
            {
                targetRect = new Rect(width / 2 - 1, height / 2 - 1, 2, 2);
                placement = ProductTourPlacement.Center;
            }
        }
        else
        {
            targetRect = new Rect(width / 2 - 1, height / 2 - 1, 2, 2);
            placement = ProductTourPlacement.Center;
        }

        var spotlightRect = new Rect(
            Math.Max(0, targetRect.X - _options.SpotlightPadding),
            Math.Max(0, targetRect.Y - _options.SpotlightPadding),
            Math.Min(width, targetRect.Width + _options.SpotlightPadding * 2),
            Math.Min(height, targetRect.Height + _options.SpotlightPadding * 2));
        SetLeft(_spotlight, spotlightRect.X);
        SetTop(_spotlight, spotlightRect.Y);
        _spotlight.Width = spotlightRect.Width;
        _spotlight.Height = spotlightRect.Height;
        LayoutMasks(width, height, spotlightRect);

        // Resolve the avatar image before measuring so DesiredSize reflects the
        // actual rendered dimensions (image aspect ratio × configured width).
        // Without this, the Image has no Source during measure and DesiredSize
        // collapses to (width, 0), causing the layout engine to plan for a
        // square avatar that is shorter than the real non-square portrait.
        if (_options.ShowAvatar)
        {
            var measurePose = _currentAvatarPose ?? TutorialAvatarPose.Idle;
            _avatarImage.Source = _avatarProvider.GetAvatar(measurePose)?.ImageSource;
        }

        _card.Measure(new Size(width, height));
        _avatarImage.Measure(new Size(width, height));
        var cardDesired = _card.DesiredSize;
        if (cardDesired.Width <= 0 || cardDesired.Height <= 0)
        {
            cardDesired = new Size(_options.CardWidth, _options.CardMaxHeight);
        }

        var aliceDesired = _avatarImage.DesiredSize;
        if (aliceDesired.Width <= 0 || aliceDesired.Height <= 0)
        {
            aliceDesired = new Size(_options.ProductTourAvatarWidth, _options.ProductTourAvatarWidth);
        }

        var preferredCard = placement is ProductTourPlacement.Auto or ProductTourPlacement.Center
            ? (ProductTourPlacement?)null
            : placement;
        var skipButtonRect = new Rect(
            Math.Max(0, width - _globalSkipButton.DesiredSize.Width - 24),
            20,
            _globalSkipButton.DesiredSize.Width,
            _globalSkipButton.DesiredSize.Height);
        var request = new ProductTourOverlayLayoutRequest
        {
            SafeArea = new Rect(
                _options.CardMargin,
                _options.CardMargin,
                Math.Max(0, width - 2 * _options.CardMargin),
                Math.Max(0, height - 2 * _options.CardMargin)),
            SpotlightRect = spotlightRect,
            CardDesiredSize = cardDesired,
            AliceDesiredSize = aliceDesired,
            PreferredCardPlacement = preferredCard,
            PreferredAlicePlacement = _currentAvatarPlacement,
            MinimumGap = _options.Gap,
            EdgePadding = _options.CardMargin,
            AliceVisible = _options.ShowAvatar,
            Obstacles = [skipButtonRect]
        };
        var layout = _layoutEngine.Arrange(request);

        var cardX = Math.Clamp(
            layout.CardPosition.X + _currentCardOffset.X,
            _options.CardMargin,
            Math.Max(_options.CardMargin, width - cardDesired.Width - _options.CardMargin));
        var cardY = Math.Clamp(
            layout.CardPosition.Y + _currentCardOffset.Y,
            _options.CardMargin,
            Math.Max(_options.CardMargin, height - cardDesired.Height - _options.CardMargin));
        SetLeft(_card, cardX);
        SetTop(_card, cardY);

        LayoutAvatarResult(layout);
    }

    private void LayoutMasks(double width, double height, Rect spotlightRect)
    {
        if (_currentInteractionMode == ProductTourInteractionMode.AllowAll)
        {
            SetMask(_blockAllMask, 0, 0, 0, 0, false);
            SetMask(_topMask, 0, 0, 0, 0, false);
            SetMask(_leftMask, 0, 0, 0, 0, false);
            SetMask(_rightMask, 0, 0, 0, 0, false);
            SetMask(_bottomMask, 0, 0, 0, 0, false);
            return;
        }

        if (_currentInteractionMode == ProductTourInteractionMode.BlockAll)
        {
            SetMask(_blockAllMask, 0, 0, width, height, true);
            SetMask(_topMask, 0, 0, 0, 0, false);
            SetMask(_leftMask, 0, 0, 0, 0, false);
            SetMask(_rightMask, 0, 0, 0, 0, false);
            SetMask(_bottomMask, 0, 0, 0, 0, false);
            return;
        }

        SetMask(_blockAllMask, 0, 0, 0, 0, false);
        SetMask(_topMask, 0, 0, width, spotlightRect.Top, true);
        SetMask(_leftMask, 0, spotlightRect.Top, spotlightRect.Left, spotlightRect.Height, true);
        SetMask(_rightMask, spotlightRect.Right, spotlightRect.Top, Math.Max(0, width - spotlightRect.Right), spotlightRect.Height, true);
        SetMask(_bottomMask, 0, spotlightRect.Bottom, width, Math.Max(0, height - spotlightRect.Bottom), true);
    }

    private static void SetMask(Border mask, double x, double y, double width, double height, bool isVisible)
    {
        SetLeft(mask, x);
        SetTop(mask, y);
        mask.Width = Math.Max(0, width);
        mask.Height = Math.Max(0, height);
        mask.Visibility = isVisible && width > 0 && height > 0 ? Visibility.Visible : Visibility.Collapsed;
        mask.IsHitTestVisible = isVisible;
    }

    private void LayoutAvatarResult(ProductTourOverlayLayoutResult layout)
    {
        if (!layout.AliceVisible || !_options.ShowAvatar)
        {
            _avatarImage.Visibility = Visibility.Collapsed;
            return;
        }

        var pose = _currentAvatarPose ?? layout.AlicePose;
        var avatar = _avatarProvider.GetAvatar(pose);
        if (avatar == null)
        {
            _avatarImage.Visibility = Visibility.Collapsed;
            return;
        }

        _avatarImage.Source = avatar.ImageSource;
        _avatarImage.Width = _options.ProductTourAvatarWidth;
        _avatarImage.Visibility = Visibility.Visible;
        SetLeft(_avatarImage, layout.AlicePosition.X);
        SetTop(_avatarImage, layout.AlicePosition.Y);
    }

    private async void ShowConfirmDialog()
    {
        var choice = await TutorialSkipContentDialog.ShowAsync(
            OverlayHost.GetContentDialogHost(this),
            _textProvider,
            _textProvider.SequenceSkipConfirmDescription,
            _sessionSuppression);
        if (choice == TutorialSkipChoice.Continue)
        {
            return;
        }

        _completion?.TrySetResult(choice switch
        {
            TutorialSkipChoice.SkipForCurrentSession => ProductTourStepAction.SkipForCurrentSession,
            TutorialSkipChoice.SkipPermanently => ProductTourStepAction.SkipPermanently,
            _ => ProductTourStepAction.Cancel
        });
    }
}
