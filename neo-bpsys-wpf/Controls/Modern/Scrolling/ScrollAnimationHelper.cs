using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

/// <summary>
/// 提供 <see cref="ScrollViewer"/> 垂直滚动动画的辅助方法。
/// </summary>
public static class ScrollAnimationHelper
{
    /// <summary>
    /// 默认动画持续时间（220 毫秒）。
    /// </summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(220);

    private static readonly ConditionalWeakTable<ScrollViewer, VerticalScrollAnimation> VerticalAnimations = new();

    /// <summary>
    /// 将垂直偏移量限制在 <see cref="ScrollViewer"/> 的有效范围内。
    /// </summary>
    /// <param name="scrollViewer">要限制偏移量的 <see cref="ScrollViewer"/>。</param>
    /// <param name="targetOffset">目标偏移量。</param>
    /// <returns>限制后的有效偏移量。</returns>
    public static double ClampVerticalOffset(ScrollViewer scrollViewer, double targetOffset)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        if (double.IsNaN(targetOffset))
        {
            return scrollViewer.VerticalOffset;
        }

        return Math.Clamp(targetOffset, 0, Math.Max(0, scrollViewer.ScrollableHeight));
    }

    /// <summary>
    /// 检查指定 <see cref="ScrollViewer"/> 的垂直滚动动画是否正在进行中。
    /// </summary>
    /// <param name="scrollViewer">要检查的 <see cref="ScrollViewer"/>。</param>
    /// <returns>如果动画正在进行中则为 <c>true</c>。</returns>
    public static bool IsVerticalAnimationActive(ScrollViewer scrollViewer)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        return VerticalAnimations.TryGetValue(scrollViewer, out var animation) && animation.IsActive;
    }

    /// <summary>
    /// 获取当前垂直滚动动画的目标偏移量。
    /// </summary>
    /// <param name="scrollViewer">要查询的 <see cref="ScrollViewer"/>。</param>
    /// <returns>目标偏移量，如果没有动画则为 <c>null</c>。</returns>
    public static double? GetCurrentVerticalAnimationTarget(ScrollViewer scrollViewer)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        return VerticalAnimations.TryGetValue(scrollViewer, out var animation) && animation.IsActive
            ? animation.TargetOffset
            : null;
    }

    /// <summary>
    /// 取消指定 <see cref="ScrollViewer"/> 的垂直滚动动画。
    /// </summary>
    /// <param name="scrollViewer">要取消动画的 <see cref="ScrollViewer"/>。</param>
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

    /// <summary>
    /// 平滑滚动 <see cref="ScrollViewer"/> 到指定的垂直偏移量。
    /// </summary>
    /// <param name="scrollViewer">要滚动的 <see cref="ScrollViewer"/>。</param>
    /// <param name="targetOffset">目标垂直偏移量。</param>
    /// <param name="duration">动画持续时间，如果为 <c>null</c> 则使用默认值。</param>
    /// <param name="animated">是否使用动画，默认为 <c>true</c>。</param>
    /// <param name="easingFunction">缓动函数，如果为 <c>null</c> 则使用默认缓动函数。</param>
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
