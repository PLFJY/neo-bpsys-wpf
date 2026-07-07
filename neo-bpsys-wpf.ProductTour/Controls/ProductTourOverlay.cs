using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>
/// Represents the user action that ended a product tour step.
/// </summary>
public enum ProductTourStepAction
{
    /// <summary>The user requested the next step or finished the last step.</summary>
    Next,
    /// <summary>The user requested the previous step.</summary>
    Previous,
    /// <summary>The user requested skipping the current tutorial.</summary>
    Skip,
    /// <summary>The step was canceled.</summary>
    Cancel
}

/// <summary>
/// Spotlight product tour overlay that points at a target control.
/// </summary>
public sealed class ProductTourOverlay : Canvas
{
    private const double CardWidth = 380;
    private const double CardMaxHeight = 260;
    private const double Gap = 16;
    private const double SpotlightPadding = 8;
    private readonly Border _blockAllMask;
    private readonly Border _topMask;
    private readonly Border _leftMask;
    private readonly Border _rightMask;
    private readonly Border _bottomMask;
    private readonly Border _spotlight;
    private readonly Border _card;
    private readonly Path _arrow;
    private readonly TextBlock _title;
    private readonly TextBlock _description;
    private readonly TextBlock _progress;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _skipButton;
    private readonly TextBlock _waitingText;
    private readonly TextBlock _errorText;
    private TaskCompletionSource<ProductTourStepAction>? _completion;
    private bool _signalReceived;
    private FrameworkElement? _currentOwner;
    private FrameworkElement? _currentTarget;
    private ProductTourPlacement _currentPlacement = ProductTourPlacement.Center;
    private ProductTourInteractionMode _currentInteractionMode = ProductTourInteractionMode.BlockAll;

    /// <summary>Initializes a new instance of the <see cref="ProductTourOverlay"/> class.</summary>
    public ProductTourOverlay()
    {
        Style = TryFindResource("ProductTourOverlayStyle") as Style;
        Panel.SetZIndex(this, 10000);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Opacity = 0;

        _blockAllMask = CreateMask();
        _topMask = CreateMask();
        _leftMask = CreateMask();
        _rightMask = CreateMask();
        _bottomMask = CreateMask();
        Children.Add(_blockAllMask);
        Children.Add(_topMask);
        Children.Add(_leftMask);
        Children.Add(_rightMask);
        Children.Add(_bottomMask);

        _spotlight = new Border { Style = TryFindResource("ProductTourSpotlightStyle") as Style, IsHitTestVisible = false };
        Children.Add(_spotlight);

        _title = new TextBlock();
        _title.Style = TryFindResource("ProductTourCardTitleStyle") as Style;
        _description = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        _description.Style = TryFindResource("ProductTourCardDescriptionStyle") as Style;
        _progress = new TextBlock { Margin = new Thickness(0, 10, 0, 0), Opacity = 0.72 };
        _waitingText = new TextBlock { Text = "等待操作...", Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        _errorText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed,
            Foreground = Brushes.OrangeRed
        };

        _previousButton = new Button { Content = "上一步", MinWidth = 78 };
        _previousButton.Style = TryFindResource("ProductTourSecondaryButtonStyle") as Style;
        _previousButton.Click += (_, _) => _completion?.TrySetResult(ProductTourStepAction.Previous);

        _nextButton = new Button { Content = "下一步", MinWidth = 78, Margin = new Thickness(8, 0, 0, 0) };
        _nextButton.Style = TryFindResource("ProductTourPrimaryButtonStyle") as Style;
        _nextButton.Click += (_, _) => _completion?.TrySetResult(ProductTourStepAction.Next);

        _skipButton = new Button { Content = "跳过", MinWidth = 70, Margin = new Thickness(8, 0, 0, 0) };
        _skipButton.Style = TryFindResource("ProductTourSkipButtonStyle") as Style;
        _skipButton.Click += (_, _) => _completion?.TrySetResult(ProductTourStepAction.Skip);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Children = { _previousButton, _nextButton, _skipButton }
        };

        _card = new Border
        {
            Style = TryFindResource("ProductTourCardStyle") as Style,
            Width = CardWidth,
            MaxHeight = CardMaxHeight,
            Child = new StackPanel { Children = { _title, _description, _progress, _waitingText, _errorText, buttons } }
        };
        Children.Add(_card);

        _arrow = new Path
        {
            Data = Geometry.Parse("M 0 0 L 16 8 L 0 16 Z"),
            Style = TryFindResource("ProductTourArrowPathStyle") as Style,
            IsHitTestVisible = false
        };
        Children.Add(_arrow);

        SizeChanged += (_, _) => LayoutCurrent(_currentOwner, _currentTarget, _currentPlacement);
    }

    /// <summary>Shows a product tour step.</summary>
    /// <param name="step">Step definition.</param>
    /// <param name="target">Target element.</param>
    /// <param name="context">Step context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user action that ended the step.</returns>
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
        _currentPlacement = step.Placement;
        _currentInteractionMode = step.InteractionMode;
        _title.Text = step.Title;
        _description.Text = step.Description;
        _progress.Text = $"{context.StepIndex + 1} / {context.StepCount}";
        _previousButton.IsEnabled = context.StepIndex > 0;
        _nextButton.Content = context.StepIndex == context.StepCount - 1 ? "完成" : "下一步";
        _nextButton.IsEnabled = _signalReceived;
        _waitingText.Visibility = _signalReceived ? Visibility.Collapsed : Visibility.Visible;
        _errorText.Visibility = Visibility.Collapsed;
        _errorText.Text = string.Empty;

        LayoutCurrent(context.Owner, target, step.Placement);
        await FadeInAsync();

        await using var registration = cancellationToken.Register(() => _completion.TrySetResult(ProductTourStepAction.Cancel));
        return await _completion.Task;
    }

    /// <summary>Marks the awaited step action as completed.</summary>
    public void MarkSignalCompleted()
    {
        _signalReceived = true;
        _nextButton.IsEnabled = true;
        _waitingText.Visibility = Visibility.Collapsed;
        _errorText.Visibility = Visibility.Collapsed;
        _errorText.Text = string.Empty;
    }

    /// <summary>Marks the awaited step action as timed out and lets the user decide how to proceed.</summary>
    /// <param name="message">Readable timeout message.</param>
    public void MarkSignalTimedOut(string message)
    {
        _signalReceived = false;
        _nextButton.IsEnabled = true;
        _nextButton.Content = "继续";
        _waitingText.Visibility = Visibility.Collapsed;
        _errorText.Text = message;
        _errorText.Visibility = Visibility.Visible;
    }

    /// <summary>Plays the exit animation.</summary>
    /// <returns>A task that completes when the animation finishes.</returns>
    public Task FadeOutAsync()
    {
        return AnimateOpacityAsync(0, TimeSpan.FromMilliseconds(220));
    }

    private Task FadeInAsync()
    {
        return AnimateOpacityAsync(1, TimeSpan.FromMilliseconds(240), 0);
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

    private static Border CreateMask() =>
        new()
        {
            Background = new SolidColorBrush(Color.FromArgb(176, 0, 0, 0)),
            IsHitTestVisible = true
        };

    private void LayoutCurrent(FrameworkElement? owner, FrameworkElement? target, ProductTourPlacement placement)
    {
        var width = owner?.ActualWidth > 0 ? owner.ActualWidth : ActualWidth;
        var height = owner?.ActualHeight > 0 ? owner.ActualHeight : ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Width = width;
        Height = height;

        Rect targetRect;
        if (owner != null && target != null)
        {
            try
            {
                var point = target.TransformToAncestor(owner).Transform(new Point(0, 0));
                targetRect = new Rect(point.X, point.Y, target.ActualWidth, target.ActualHeight);
            }
            catch (InvalidOperationException)
            {
                targetRect = new Rect(width / 2 - 1, height / 2 - 1, 2, 2);
            }
        }
        else
        {
            targetRect = new Rect(width / 2 - 1, height / 2 - 1, 2, 2);
            placement = ProductTourPlacement.Center;
        }

        var spotlightRect = new Rect(
            Math.Max(0, targetRect.X - SpotlightPadding),
            Math.Max(0, targetRect.Y - SpotlightPadding),
            Math.Min(width, targetRect.Width + SpotlightPadding * 2),
            Math.Min(height, targetRect.Height + SpotlightPadding * 2));
        SetLeft(_spotlight, spotlightRect.X);
        SetTop(_spotlight, spotlightRect.Y);
        _spotlight.Width = spotlightRect.Width;
        _spotlight.Height = spotlightRect.Height;
        LayoutMasks(width, height, spotlightRect);

        var actualPlacement = placement == ProductTourPlacement.Auto
            ? ChooseAutoPlacement(width, height, targetRect)
            : placement;
        var cardPoint = CalculateCardPoint(width, height, targetRect, actualPlacement);
        SetLeft(_card, cardPoint.X);
        SetTop(_card, cardPoint.Y);
        LayoutArrow(targetRect, cardPoint, actualPlacement);
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

    private static ProductTourPlacement ChooseAutoPlacement(double width, double height, Rect target)
    {
        if (width - target.Right >= CardWidth + Gap) return ProductTourPlacement.Right;
        if (height - target.Bottom >= CardMaxHeight + Gap) return ProductTourPlacement.Bottom;
        if (target.Left >= CardWidth + Gap) return ProductTourPlacement.Left;
        if (target.Top >= CardMaxHeight + Gap) return ProductTourPlacement.Top;
        return ProductTourPlacement.Center;
    }

    private static Point CalculateCardPoint(double width, double height, Rect target, ProductTourPlacement placement)
    {
        var x = placement switch
        {
            ProductTourPlacement.Left or ProductTourPlacement.LeftTop or ProductTourPlacement.LeftBottom => target.Left - CardWidth - Gap,
            ProductTourPlacement.Right or ProductTourPlacement.RightTop or ProductTourPlacement.RightBottom => target.Right + Gap,
            ProductTourPlacement.TopLeft or ProductTourPlacement.BottomLeft => target.Left,
            ProductTourPlacement.TopRight or ProductTourPlacement.BottomRight => target.Right - CardWidth,
            ProductTourPlacement.Center => width / 2 - CardWidth / 2,
            _ => target.Left + target.Width / 2 - CardWidth / 2
        };
        var y = placement switch
        {
            ProductTourPlacement.Top or ProductTourPlacement.TopLeft or ProductTourPlacement.TopRight => target.Top - CardMaxHeight - Gap,
            ProductTourPlacement.Bottom or ProductTourPlacement.BottomLeft or ProductTourPlacement.BottomRight => target.Bottom + Gap,
            ProductTourPlacement.LeftTop or ProductTourPlacement.RightTop => target.Top,
            ProductTourPlacement.LeftBottom or ProductTourPlacement.RightBottom => target.Bottom - CardMaxHeight,
            ProductTourPlacement.Center => height / 2 - CardMaxHeight / 2,
            _ => target.Top + target.Height / 2 - CardMaxHeight / 2
        };
        return new Point(
            Math.Clamp(x, 12, Math.Max(12, width - CardWidth - 12)),
            Math.Clamp(y, 12, Math.Max(12, height - CardMaxHeight - 12)));
    }

    private void LayoutArrow(Rect target, Point card, ProductTourPlacement placement)
    {
        if (placement == ProductTourPlacement.Center)
        {
            _arrow.Visibility = Visibility.Collapsed;
            return;
        }

        _arrow.Visibility = Visibility.Visible;
        var targetCenter = new Point(target.Left + target.Width / 2, target.Top + target.Height / 2);
        var cardCenter = new Point(card.X + CardWidth / 2, card.Y + CardMaxHeight / 2);
        var x = (targetCenter.X + cardCenter.X) / 2;
        var y = (targetCenter.Y + cardCenter.Y) / 2;
        SetLeft(_arrow, x);
        SetTop(_arrow, y);
        var angle = Math.Atan2(targetCenter.Y - cardCenter.Y, targetCenter.X - cardCenter.X) * 180 / Math.PI;
        _arrow.RenderTransform = new RotateTransform(angle, 8, 8);
    }
}
