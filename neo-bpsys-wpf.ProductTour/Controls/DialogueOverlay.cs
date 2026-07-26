using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ContentDialogHost = Wpf.Ui.Controls.ContentDialogHost;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>
/// 带有打字机效果的 NPC 风格底部对话遮罩。
/// </summary>
public sealed class DialogueOverlay : Grid
{
    private readonly TextBlock _speakerBlock;
    private readonly TextBlock _textBlock;
    private readonly TextBlock _textMeasureBlock;
    private readonly TextBlock _continueBlock;
    private readonly Border _dialogueBox;
    private readonly Button _skipButton;
    private readonly Image _avatarImage;
    private IReadOnlyList<string> _lines = [];
    private int _lineIndex;
    private int _charIndex;
    private readonly Stopwatch _typewriterStopwatch = new();
    private bool _isTypewriterRendering;
    private TaskCompletionSource<TutorialRunResult>? _completion;
    private readonly ITutorialTextProvider _textProvider;
    private readonly ProductTourOptions _options;
    private readonly ITutorialAvatarProvider _avatarProvider;
    private readonly ITutorialContentResolver _contentResolver;
    private readonly ITutorialLanguageService _languageService;
    private string? _currentLinesKey;
    private readonly ITutorialSessionSuppression _sessionSuppression;

    /// <summary>获取或设置打字机间隔。</summary>
    public TimeSpan TypewriterInterval { get; set; } = TimeSpan.FromMilliseconds(28);

    /// <summary>初始化 <see cref="DialogueOverlay"/> 类的新实例。</summary>
    public DialogueOverlay()
        : this(new DefaultTutorialTextProvider(), new ProductTourOptions(), new NoOpTutorialAvatarProvider(),
             new DefaultTutorialContentResolver(), new NoOpTutorialLanguageService())
    {
    }

    /// <summary>初始化 <see cref="DialogueOverlay"/> 类的新实例。</summary>
    /// <param name="textProvider">固定 UI 文本提供器。</param>
    /// <param name="options">Product Tour 显示选项。</param>
    public DialogueOverlay(ITutorialTextProvider textProvider, ProductTourOptions options)
        : this(textProvider, options, new NoOpTutorialAvatarProvider(),
             new DefaultTutorialContentResolver(), new NoOpTutorialLanguageService())
    {
    }

    /// <summary>初始化 <see cref="DialogueOverlay"/> 类的新实例。</summary>
    /// <param name="textProvider">固定 UI 文本提供器。</param>
    /// <param name="options">Product Tour 显示选项。</param>
    /// <param name="avatarProvider">教程头像提供器。</param>
    /// <param name="contentResolver">用于本地化对话台词的教程内容解析器。</param>
    /// <param name="languageService">用于热切换的教程语言服务。</param>
    public DialogueOverlay(
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
        TypewriterInterval = _options.TypewriterInterval;
        Style = TryFindResource("ProductTourDialogueOverlayStyle") as Style;
        Background = CreateMaskBrush(_options.DialogueMaskOpacity);
        Panel.SetZIndex(this, 10000);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Opacity = 0;

        _speakerBlock = new TextBlock();
        _speakerBlock.Style = TryFindResource("ProductTourDialogueSpeakerStyle") as Style;

        _textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _textBlock.Style = TryFindResource("ProductTourDialogueTextStyle") as Style;
        _textMeasureBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Hidden,
            IsHitTestVisible = false
        };
        _textMeasureBlock.Style = TryFindResource("ProductTourDialogueTextStyle") as Style;

        var dialogueTextHost = new Grid
        {
            Children = { _textMeasureBlock, _textBlock }
        };

        _continueBlock = new TextBlock
        {
            Text = _textProvider.ClickToContinue,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        _continueBlock.Style = TryFindResource("ProductTourDialogueContinueStyle") as Style;

        var blink = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        _continueBlock.BeginAnimation(OpacityProperty, blink);

        _avatarImage = new Image
        {
            Width = _options.DialogueAvatarWidth,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false,
            Margin = _options.AvatarMargin,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _avatarImage.Style = TryFindResource("ProductTourDialogueAvatarImageStyle") as Style;
        Panel.SetZIndex(_avatarImage, 1);

        _dialogueBox = new Border
        {
            Style = TryFindResource("ProductTourDialogueBoxStyle") as Style,
            Width = double.NaN,
            MaxWidth = _options.DialogueBoxMaxWidth,
            Opacity = Math.Max(_options.DialogueBoxMinOpacity, 0.94),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = _options.DialogueBoxMargin,
            RenderTransform = new TranslateTransform(0, _options.DialogueInitialTranslateY),
            Child = new StackPanel { Children = { _speakerBlock, dialogueTextHost, _continueBlock } }
        };
        Panel.SetZIndex(_dialogueBox, 2);

        _skipButton = new Button
        {
            Content = _textProvider.Skip,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 20, 24, 0)
        };
        _skipButton.Style = TryFindResource("ProductTourSkipButtonStyle") as Style;
        _skipButton.Click += (_, _) => ShowConfirmDialog();
        Panel.SetZIndex(_skipButton, 4);

        Children.Add(_avatarImage);
        Children.Add(_dialogueBox);
        Children.Add(_skipButton);
        MouseLeftButtonDown += (_, _) => Advance();

        _languageService.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) =>
        {
            StopTypewriter();
            _languageService.LanguageChanged -= OnLanguageChanged;
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(RefreshDialogueLanguage));
    }

    /// <summary>
    /// 从资源键重新解析对话台词，并使用新解析的文本为当前行重启打字机。
    /// </summary>
    private void RefreshDialogueLanguage()
    {
        if (!string.IsNullOrWhiteSpace(_currentLinesKey))
        {
            _lines = _contentResolver.ResolveLines(_currentLinesKey);
        }

        if (_lineIndex >= _lines.Count)
        {
            _lineIndex = Math.Max(0, _lines.Count - 1);
        }

        _skipButton.Content = _textProvider.Skip;
        StartLine();
    }

    /// <summary>显示对话台词。</summary>
    /// <param name="speaker">说话者名称。</param>
    /// <param name="lines">对话台词。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="linesKey">用于解析本地化对话台词的可选资源键。非 null 时，台词从资源中解析。</param>
    /// <returns>对话运行结果。</returns>
    public async Task<TutorialRunResult> ShowAsync(
        string speaker,
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken,
        string? linesKey = null)
    {
        var avatar = _options.ShowAvatar ? _avatarProvider.GetAvatar(TutorialAvatarPose.Idle) : null;
        if (avatar != null)
        {
            _avatarImage.Source = avatar.ImageSource;
            _avatarImage.Visibility = Visibility.Visible;
        }
        else
        {
            _avatarImage.Source = null;
            _avatarImage.Visibility = Visibility.Collapsed;
        }

        _speakerBlock.Text = avatar != null && IsDefaultSpeaker(speaker)
            ? avatar.DisplayName
            : speaker;
        _currentLinesKey = linesKey;
        _lines = !string.IsNullOrWhiteSpace(linesKey)
            ? _contentResolver.ResolveLines(linesKey)
            : lines;
        _lineIndex = 0;
        _completion = new TaskCompletionSource<TutorialRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        StartLine();
        await FadeInAsync();
        await using var registration = cancellationToken.Register(() => _completion.TrySetResult(TutorialRunResult.Canceled));
        var result = await _completion.Task;
        StopTypewriter();
        await FadeOutAsync();
        return result;
    }

    /// <summary>
    /// 立即结束当前对话，并将其作为已取消的教程运行返回。
    /// </summary>
    /// <remarks>
    /// 用于教程调试跳转等需要中断当前流项、随后重新解析队列的场景。
    /// </remarks>
    public void Cancel()
    {
        StopTypewriter();
        _completion?.TrySetResult(TutorialRunResult.Canceled);
    }

    private static bool IsDefaultSpeaker(string speaker)
    {
        return string.IsNullOrWhiteSpace(speaker)
            || string.Equals(speaker, "neo-bpsys-wpf", StringComparison.Ordinal);
    }

    private void StartLine()
    {
        StopTypewriter();
        _charIndex = 0;
        _textBlock.Text = string.Empty;
        _continueBlock.Visibility = Visibility.Hidden;

        if (_lineIndex >= _lines.Count)
        {
            _textMeasureBlock.Text = string.Empty;
            return;
        }

        var line = _lines[_lineIndex];
        _textMeasureBlock.Text = line;
        if (line.Length == 0 || TypewriterInterval <= TimeSpan.Zero)
        {
            CompleteCurrentLine(line);
            return;
        }

        _typewriterStopwatch.Restart();
        CompositionTarget.Rendering += OnTypewriterRendering;
        _isTypewriterRendering = true;
    }

    private void OnTypewriterRendering(object? sender, EventArgs e)
    {
        if (_lineIndex >= _lines.Count)
        {
            StopTypewriter();
            return;
        }

        var line = _lines[_lineIndex];
        var targetCharIndex = CalculateVisibleCharacterCount(
            line.Length,
            _typewriterStopwatch.Elapsed,
            TypewriterInterval);
        if (targetCharIndex <= _charIndex)
        {
            return;
        }

        _charIndex = targetCharIndex;
        _textBlock.Text = line[.._charIndex];
        if (_charIndex >= line.Length)
        {
            CompleteCurrentLine(line);
        }
    }

    internal static int CalculateVisibleCharacterCount(
        int lineLength,
        TimeSpan elapsed,
        TimeSpan characterInterval)
    {
        if (lineLength <= 0)
        {
            return 0;
        }

        if (characterInterval <= TimeSpan.Zero)
        {
            return lineLength;
        }

        var elapsedIntervals = elapsed.Ticks / (double)characterInterval.Ticks;
        return Math.Min(lineLength, Math.Max(0, (int)Math.Floor(elapsedIntervals)));
    }

    private void CompleteCurrentLine(string line)
    {
        StopTypewriter();
        _textBlock.Text = line;
        _charIndex = line.Length;
        _continueBlock.Visibility = Visibility.Visible;
    }

    private void StopTypewriter()
    {
        _typewriterStopwatch.Stop();
        if (!_isTypewriterRendering)
        {
            return;
        }

        CompositionTarget.Rendering -= OnTypewriterRendering;
        _isTypewriterRendering = false;
    }

    private void Advance()
    {
        if (_completion == null || _lineIndex >= _lines.Count)
        {
            return;
        }

        var line = _lines[_lineIndex];
        if (_charIndex < line.Length)
        {
            CompleteCurrentLine(line);
            return;
        }

        _lineIndex++;
        if (_lineIndex >= _lines.Count)
        {
            _completion.TrySetResult(TutorialRunResult.Completed);
            return;
        }

        StartLine();
    }

    private Task FadeInAsync()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storyboard = new Storyboard();
        var opacity = new DoubleAnimation(0, 1, _options.DialogueFadeInDuration);
        Storyboard.SetTarget(opacity, this);
        Storyboard.SetTargetProperty(opacity, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacity);
        var y = new DoubleAnimation(_options.DialogueInitialTranslateY, 0, _options.DialogueBoxEnterDuration);
        Storyboard.SetTarget(y, _dialogueBox);
        Storyboard.SetTargetProperty(y, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        storyboard.Children.Add(y);
        storyboard.Completed += (_, _) => source.TrySetResult();
        storyboard.Begin();
        return source.Task;
    }

    private Task FadeOutAsync()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storyboard = new Storyboard();
        var opacity = new DoubleAnimation(0, _options.DialogueFadeOutDuration) { From = Opacity };
        Storyboard.SetTarget(opacity, this);
        Storyboard.SetTargetProperty(opacity, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacity);
        var y = new DoubleAnimation(0, Math.Max(0, _options.DialogueInitialTranslateY / 2), _options.DialogueFadeOutDuration);
        Storyboard.SetTarget(y, _dialogueBox);
        Storyboard.SetTargetProperty(y, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        storyboard.Children.Add(y);
        storyboard.Completed += (_, _) => source.TrySetResult();
        storyboard.Begin();
        return source.Task;
    }

    private async void ShowConfirmDialog()
    {
        var choice = await TutorialSkipContentDialog.ShowAsync(
            OverlayHost.GetContentDialogHost(this),
            _textProvider,
            _textProvider.FirstRunSkipConfirmDescription,
            _sessionSuppression);
        if (choice == TutorialSkipChoice.Continue)
        {
            return;
        }

        _completion?.TrySetResult(choice switch
        {
            TutorialSkipChoice.SkipForCurrentSession => TutorialRunResult.Skipped,
            TutorialSkipChoice.SkipPermanently => TutorialRunResult.SkippedPermanently,
            _ => TutorialRunResult.Canceled
        });
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
