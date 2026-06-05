using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

public static class ScrollAnimationHelper
{
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(220);

    private static readonly ConditionalWeakTable<ScrollViewer, VerticalScrollAnimation> VerticalAnimations = new();

    public static double ClampVerticalOffset(ScrollViewer scrollViewer, double targetOffset)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        if (double.IsNaN(targetOffset))
        {
            return scrollViewer.VerticalOffset;
        }

        return Math.Clamp(targetOffset, 0, Math.Max(0, scrollViewer.ScrollableHeight));
    }

    public static bool IsVerticalAnimationActive(ScrollViewer scrollViewer)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        return VerticalAnimations.TryGetValue(scrollViewer, out var animation) && animation.IsActive;
    }

    public static double? GetCurrentVerticalAnimationTarget(ScrollViewer scrollViewer)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        return VerticalAnimations.TryGetValue(scrollViewer, out var animation) && animation.IsActive
            ? animation.TargetOffset
            : null;
    }

    public static void CancelVerticalAnimation(ScrollViewer scrollViewer)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        if (!scrollViewer.Dispatcher.CheckAccess())
        {
            scrollViewer.Dispatcher.Invoke(() => CancelVerticalAnimation(scrollViewer));
            return;
        }

        if (VerticalAnimations.TryGetValue(scrollViewer, out var animation))
        {
            animation.Stop();
            VerticalAnimations.Remove(scrollViewer);
        }
    }

    public static void SmoothScrollToVerticalOffset(
        ScrollViewer scrollViewer,
        double targetOffset,
        TimeSpan? duration = null,
        bool animated = true,
        IEasingFunction? easingFunction = null)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        if (!scrollViewer.Dispatcher.CheckAccess())
        {
            scrollViewer.Dispatcher.Invoke(
                () => SmoothScrollToVerticalOffset(scrollViewer, targetOffset, duration, animated, easingFunction));
            return;
        }

        var clampedTarget = ClampVerticalOffset(scrollViewer, targetOffset);
        var effectiveDuration = duration ?? DefaultDuration;

        if (!animated || effectiveDuration <= TimeSpan.Zero || !AreAnimationsEnabled())
        {
            CancelVerticalAnimation(scrollViewer);
            scrollViewer.ScrollToVerticalOffset(clampedTarget);
            return;
        }

        if (VerticalAnimations.TryGetValue(scrollViewer, out var existingAnimation))
        {
            existingAnimation.Retarget(clampedTarget, effectiveDuration, easingFunction ?? CreateDefaultEasingFunction());
            return;
        }

        var animation = new VerticalScrollAnimation(
            scrollViewer,
            clampedTarget,
            effectiveDuration,
            easingFunction ?? CreateDefaultEasingFunction(),
            RemoveAnimation);

        VerticalAnimations.Add(scrollViewer, animation);
        animation.Start();
    }

    private static bool AreAnimationsEnabled() =>
        SystemParameters.ClientAreaAnimation && RenderCapability.Tier > 0;

    private static IEasingFunction CreateDefaultEasingFunction() =>
        new CubicEase { EasingMode = EasingMode.EaseOut };

    private static void RemoveAnimation(ScrollViewer scrollViewer)
    {
        VerticalAnimations.Remove(scrollViewer);
    }

    private sealed class VerticalScrollAnimation
    {
        private readonly WeakReference<ScrollViewer> _scrollViewerReference;
        private readonly Action<ScrollViewer> _remove;
        private TimeSpan _duration;
        private IEasingFunction _easingFunction;
        private DateTime _startedAt;
        private double _startOffset;
        private bool _isRenderingAttached;

        public VerticalScrollAnimation(
            ScrollViewer scrollViewer,
            double targetOffset,
            TimeSpan duration,
            IEasingFunction easingFunction,
            Action<ScrollViewer> remove)
        {
            _scrollViewerReference = new WeakReference<ScrollViewer>(scrollViewer);
            TargetOffset = targetOffset;
            _duration = duration;
            _easingFunction = easingFunction;
            _remove = remove;
        }

        public bool IsActive { get; private set; }

        public double TargetOffset { get; private set; }

        public void Start()
        {
            if (!_scrollViewerReference.TryGetTarget(out var scrollViewer))
            {
                return;
            }

            _startedAt = DateTime.UtcNow;
            _startOffset = scrollViewer.VerticalOffset;
            IsActive = true;
            AttachRendering();
        }

        public void Retarget(double targetOffset, TimeSpan duration, IEasingFunction easingFunction)
        {
            if (!_scrollViewerReference.TryGetTarget(out var scrollViewer))
            {
                Stop();
                return;
            }

            TargetOffset = targetOffset;
            _duration = duration;
            _easingFunction = easingFunction;
            _startedAt = DateTime.UtcNow;
            _startOffset = scrollViewer.VerticalOffset;

            if (!IsActive)
            {
                IsActive = true;
                AttachRendering();
            }
        }

        public void Stop()
        {
            IsActive = false;
            DetachRendering();
        }

        private void AttachRendering()
        {
            if (_isRenderingAttached)
            {
                return;
            }

            CompositionTarget.Rendering += OnRendering;
            _isRenderingAttached = true;
        }

        private void DetachRendering()
        {
            if (!_isRenderingAttached)
            {
                return;
            }

            CompositionTarget.Rendering -= OnRendering;
            _isRenderingAttached = false;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_scrollViewerReference.TryGetTarget(out var scrollViewer))
            {
                Stop();
                return;
            }

            if (!scrollViewer.Dispatcher.CheckAccess())
            {
                scrollViewer.Dispatcher.BeginInvoke(OnRenderingOnDispatcher, DispatcherPriority.Render);
                return;
            }

            Tick(scrollViewer);
        }

        private void OnRenderingOnDispatcher()
        {
            if (_scrollViewerReference.TryGetTarget(out var scrollViewer))
            {
                Tick(scrollViewer);
            }
            else
            {
                Stop();
            }
        }

        private void Tick(ScrollViewer scrollViewer)
        {
            var elapsed = DateTime.UtcNow - _startedAt;
            var progress = Math.Clamp(elapsed.TotalMilliseconds / _duration.TotalMilliseconds, 0, 1);
            var easedProgress = _easingFunction.Ease(progress);
            var nextOffset = _startOffset + ((TargetOffset - _startOffset) * easedProgress);

            scrollViewer.ScrollToVerticalOffset(ClampVerticalOffset(scrollViewer, nextOffset));

            if (progress < 1)
            {
                return;
            }

            scrollViewer.ScrollToVerticalOffset(ClampVerticalOffset(scrollViewer, TargetOffset));
            Stop();
            _remove(scrollViewer);
        }
    }
}
