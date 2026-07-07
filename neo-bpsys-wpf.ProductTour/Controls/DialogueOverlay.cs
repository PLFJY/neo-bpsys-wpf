using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>
/// NPC-style bottom dialogue overlay with a typewriter effect.
/// </summary>
public sealed class DialogueOverlay : Grid
{
    private readonly TextBlock _speakerBlock;
    private readonly TextBlock _textBlock;
    private readonly TextBlock _continueBlock;
    private readonly Border _dialogueBox;
    private IReadOnlyList<string> _lines = [];
    private int _lineIndex;
    private int _charIndex;
    private DispatcherTimer? _timer;
    private TaskCompletionSource<TutorialRunResult>? _completion;

    /// <summary>Gets or sets the typewriter interval.</summary>
    public TimeSpan TypewriterInterval { get; set; } = TimeSpan.FromMilliseconds(28);

    /// <summary>Initializes a new instance of the <see cref="DialogueOverlay"/> class.</summary>
    public DialogueOverlay()
    {
        Style = TryFindResource("ProductTourDialogueOverlayStyle") as Style;
        Panel.SetZIndex(this, 10000);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Opacity = 0;

        _speakerBlock = new TextBlock();
        _speakerBlock.Style = TryFindResource("ProductTourDialogueSpeakerStyle") as Style;

        _textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _textBlock.Style = TryFindResource("ProductTourDialogueTextStyle") as Style;

        _continueBlock = new TextBlock
        {
            Text = "点击继续",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var blink = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(700))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        _continueBlock.BeginAnimation(OpacityProperty, blink);

        _dialogueBox = new Border
        {
            Style = TryFindResource("ProductTourDialogueBoxStyle") as Style,
            Width = double.NaN,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(48),
            RenderTransform = new TranslateTransform(0, 24),
            Child = new StackPanel { Children = { _speakerBlock, _textBlock, _continueBlock } }
        };

        Children.Add(_dialogueBox);
        MouseLeftButtonDown += (_, _) => Advance();
    }

    /// <summary>Shows dialogue lines.</summary>
    /// <param name="speaker">Speaker name.</param>
    /// <param name="lines">Dialogue lines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dialogue run result.</returns>
    public async Task<TutorialRunResult> ShowAsync(string speaker, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        _speakerBlock.Text = speaker;
        _lines = lines;
        _lineIndex = 0;
        _completion = new TaskCompletionSource<TutorialRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        StartLine();
        await FadeInAsync();
        await using var registration = cancellationToken.Register(() => _completion.TrySetResult(TutorialRunResult.Canceled));
        var result = await _completion.Task;
        await FadeOutAsync();
        return result;
    }

    private void StartLine()
    {
        _timer?.Stop();
        _charIndex = 0;
        _textBlock.Text = string.Empty;
        _continueBlock.Visibility = Visibility.Hidden;
        _timer = new DispatcherTimer { Interval = TypewriterInterval };
        _timer.Tick += (_, _) =>
        {
            if (_lineIndex >= _lines.Count)
            {
                _timer.Stop();
                return;
            }

            var line = _lines[_lineIndex];
            if (_charIndex >= line.Length)
            {
                _timer.Stop();
                _continueBlock.Visibility = Visibility.Visible;
                return;
            }

            _textBlock.Text += line[_charIndex];
            _charIndex++;
        };
        _timer.Start();
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
            _timer?.Stop();
            _textBlock.Text = line;
            _charIndex = line.Length;
            _continueBlock.Visibility = Visibility.Visible;
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
        var opacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280));
        Storyboard.SetTarget(opacity, this);
        Storyboard.SetTargetProperty(opacity, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacity);
        var y = new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(300));
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
        var opacity = new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)) { From = Opacity };
        Storyboard.SetTarget(opacity, this);
        Storyboard.SetTargetProperty(opacity, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacity);
        var y = new DoubleAnimation(0, 12, TimeSpan.FromMilliseconds(240));
        Storyboard.SetTarget(y, _dialogueBox);
        Storyboard.SetTargetProperty(y, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        storyboard.Children.Add(y);
        storyboard.Completed += (_, _) => source.TrySetResult();
        storyboard.Begin();
        return source.Task;
    }
}
