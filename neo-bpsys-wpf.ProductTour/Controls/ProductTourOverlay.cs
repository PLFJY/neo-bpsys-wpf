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
    private readonly Button _globalSkipButton;
    private readonly TextBlock _waitingText;
    private readonly TextBlock _errorText;
    private readonly ITutorialTextProvider _textProvider;
    private readonly ProductTourOptions _options;
    private TaskCompletionSource<ProductTourStepAction>? _completion;
    private bool _signalReceived;
    private FrameworkElement? _currentOwner;
    private FrameworkElement? _currentTarget;
    private ProductTourPlacement _currentPlacement = ProductTourPlacement.Center;
    private ProductTourInteractionMode _currentInteractionMode = ProductTourInteractionMode.BlockAll;
    private ProductTourArrowKind _currentArrowKind = ProductTourArrowKind.Triangle;
    private SkipTutorialConfirmDialog? _confirmDialog;

    /// <summary>Initializes a new instance of the <see cref="ProductTourOverlay"/> class.</summary>
    public ProductTourOverlay()
        : this(new DefaultTutorialTextProvider(), new ProductTourOptions())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ProductTourOverlay"/> class.</summary>
    /// <param name="textProvider">Fixed UI text provider.</param>
    /// <param name="options">Product tour display options.</param>
    public ProductTourOverlay(ITutorialTextProvider textProvider, ProductTourOptions options)
    {
        _textProvider = textProvider;
        _options = options;
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

        _skipButton = new Button { Content = _textProvider.Skip, MinWidth = 70, Margin = new Thickness(8, 0, 0, 0) };
        _skipButton.Style = TryFindResource("ProductTourSkipButtonStyle") as Style;
        _skipButton.Click += (_, _) => ShowConfirmDialog();

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
            Children = { _previousButton, _nextButton, _skipButton }
        };

        _card = new Border
        {
            Name = "Card",
            Style = TryFindResource("ProductTourCardStyle") as Style,
            Width = _options.CardWidth,
            MaxHeight = _options.CardMaxHeight,
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
        _progress.Visibility = _options.ShowStepProgress ? Visibility.Visible : Visibility.Collapsed;
        _previousButton.IsEnabled = context.StepIndex > 0;
        _nextButton.Content = context.StepIndex == context.StepCount - 1 ? _textProvider.Finish : _textProvider.Next;
        _nextButton.IsEnabled = _signalReceived;
        _skipButton.Visibility = _options.ShowSkipButton ? Visibility.Visible : Visibility.Collapsed;
        _waitingText.Visibility = _signalReceived ? Visibility.Collapsed : Visibility.Visible;
        _errorText.Visibility = Visibility.Collapsed;
        _errorText.Text = string.Empty;
        var arrowKind = step.ArrowKind ?? _options.DefaultArrowKind;
        _currentArrowKind = _options.ShowArrow ? arrowKind : ProductTourArrowKind.None;

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
        _nextButton.Content = _textProvider.Continue;
        _waitingText.Visibility = Visibility.Collapsed;
        _errorText.Text = message;
        _errorText.Visibility = Visibility.Visible;
    }

    /// <summary>Plays the exit animation.</summary>
    /// <returns>A task that completes when the animation finishes.</returns>
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
        var width = owner?.ActualWidth > 0 ? owner.ActualWidth : ActualWidth;
        var height = owner?.ActualHeight > 0 ? owner.ActualHeight : ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Width = width;
        Height = height;
        _globalSkipButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(_globalSkipButton, Math.Max(0, width - _globalSkipButton.DesiredSize.Width - 24));
        SetTop(_globalSkipButton, 20);

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
            Math.Max(0, targetRect.X - _options.SpotlightPadding),
            Math.Max(0, targetRect.Y - _options.SpotlightPadding),
            Math.Min(width, targetRect.Width + _options.SpotlightPadding * 2),
            Math.Min(height, targetRect.Height + _options.SpotlightPadding * 2));
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
        LayoutConfirmDialog();
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

    private ProductTourPlacement ChooseAutoPlacement(double width, double height, Rect target)
    {
        if (width - target.Right >= _options.CardWidth + _options.Gap) return ProductTourPlacement.Right;
        if (height - target.Bottom >= _options.CardMaxHeight + _options.Gap) return ProductTourPlacement.Bottom;
        if (target.Left >= _options.CardWidth + _options.Gap) return ProductTourPlacement.Left;
        if (target.Top >= _options.CardMaxHeight + _options.Gap) return ProductTourPlacement.Top;
        return ProductTourPlacement.Center;
    }

    private Point CalculateCardPoint(double width, double height, Rect target, ProductTourPlacement placement)
    {
        var x = placement switch
        {
            ProductTourPlacement.Left or ProductTourPlacement.LeftTop or ProductTourPlacement.LeftBottom => target.Left - _options.CardWidth - _options.Gap,
            ProductTourPlacement.Right or ProductTourPlacement.RightTop or ProductTourPlacement.RightBottom => target.Right + _options.Gap,
            ProductTourPlacement.TopLeft or ProductTourPlacement.BottomLeft => target.Left,
            ProductTourPlacement.TopRight or ProductTourPlacement.BottomRight => target.Right - _options.CardWidth,
            ProductTourPlacement.Center => width / 2 - _options.CardWidth / 2,
            _ => target.Left + target.Width / 2 - _options.CardWidth / 2
        };
        var y = placement switch
        {
            ProductTourPlacement.Top or ProductTourPlacement.TopLeft or ProductTourPlacement.TopRight => target.Top - _options.CardMaxHeight - _options.Gap,
            ProductTourPlacement.Bottom or ProductTourPlacement.BottomLeft or ProductTourPlacement.BottomRight => target.Bottom + _options.Gap,
            ProductTourPlacement.LeftTop or ProductTourPlacement.RightTop => target.Top,
            ProductTourPlacement.LeftBottom or ProductTourPlacement.RightBottom => target.Bottom - _options.CardMaxHeight,
            ProductTourPlacement.Center => height / 2 - _options.CardMaxHeight / 2,
            _ => target.Top + target.Height / 2 - _options.CardMaxHeight / 2
        };
        return new Point(
            Math.Clamp(x, _options.CardMargin, Math.Max(_options.CardMargin, width - _options.CardWidth - _options.CardMargin)),
            Math.Clamp(y, _options.CardMargin, Math.Max(_options.CardMargin, height - _options.CardMaxHeight - _options.CardMargin)));
    }

    private void LayoutArrow(Rect target, Point card, ProductTourPlacement placement)
    {
        if (placement == ProductTourPlacement.Center || _currentArrowKind != ProductTourArrowKind.Triangle)
        {
            _arrow.Visibility = Visibility.Collapsed;
            return;
        }

        _arrow.Visibility = Visibility.Visible;
        var targetCenter = new Point(target.Left + target.Width / 2, target.Top + target.Height / 2);
        var cardCenter = new Point(card.X + _options.CardWidth / 2, card.Y + _options.CardMaxHeight / 2);
        var x = (targetCenter.X + cardCenter.X) / 2;
        var y = (targetCenter.Y + cardCenter.Y) / 2;
        SetLeft(_arrow, x);
        SetTop(_arrow, y);
        var angle = Math.Atan2(targetCenter.Y - cardCenter.Y, targetCenter.X - cardCenter.X) * 180 / Math.PI;
        _arrow.RenderTransform = new RotateTransform(angle, 8, 8);
    }

    private void ShowConfirmDialog()
    {
        if (_confirmDialog != null)
        {
            return;
        }

        _confirmDialog = new SkipTutorialConfirmDialog(_textProvider, _options);
        Children.Add(_confirmDialog);
        Panel.SetZIndex(_confirmDialog, 5);
        _confirmDialog.Canceled += (_, _) =>
        {
            Children.Remove(_confirmDialog);
            _confirmDialog = null;
        };
        _confirmDialog.Confirmed += (_, _) =>
        {
            Children.Remove(_confirmDialog);
            _confirmDialog = null;
            _completion?.TrySetResult(ProductTourStepAction.Skip);
        };
        LayoutConfirmDialog();
    }

    private void LayoutConfirmDialog()
    {
        if (_confirmDialog == null)
        {
            return;
        }

        _confirmDialog.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SetLeft(_confirmDialog, Math.Max(0, (ActualWidth > 0 ? ActualWidth : Width) / 2 - _confirmDialog.DesiredSize.Width / 2));
        SetTop(_confirmDialog, Math.Max(0, (ActualHeight > 0 ? ActualHeight : Height) / 2 - _confirmDialog.DesiredSize.Height / 2));
    }
}
