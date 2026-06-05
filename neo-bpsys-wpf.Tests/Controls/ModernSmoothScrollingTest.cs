#nullable enable

using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Controls.Modern.Scrolling;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

public class ModernSmoothScrollingTest
{
    [Fact]
    public void ClampVerticalOffsetClampsToScrollableRange()
    {
        RunSta(() =>
        {
            WithScrollableViewer(scrollViewer =>
            {
                Assert.Equal(0, ScrollAnimationHelper.ClampVerticalOffset(scrollViewer, -10));
                Assert.Equal(scrollViewer.ScrollableHeight, ScrollAnimationHelper.ClampVerticalOffset(scrollViewer, scrollViewer.ScrollableHeight + 100));
                Assert.Equal(40, ScrollAnimationHelper.ClampVerticalOffset(scrollViewer, 40));
            });
        });
    }

    [Fact]
    public void DurationLessThanOrEqualZeroScrollsImmediately()
    {
        RunSta(() =>
        {
            WithScrollableViewer(scrollViewer =>
            {
                ScrollAnimationHelper.SmoothScrollToVerticalOffset(scrollViewer, 90, TimeSpan.Zero);
                scrollViewer.UpdateLayout();

                Assert.Equal(90, scrollViewer.VerticalOffset);
                Assert.False(ScrollAnimationHelper.IsVerticalAnimationActive(scrollViewer));
            });
        });
    }

    [Fact]
    public void AnimatedFalseScrollsImmediately()
    {
        RunSta(() =>
        {
            WithScrollableViewer(scrollViewer =>
            {
                ScrollAnimationHelper.SmoothScrollToVerticalOffset(scrollViewer, 90, TimeSpan.FromSeconds(1), animated: false);
                scrollViewer.UpdateLayout();

                Assert.Equal(90, scrollViewer.VerticalOffset);
                Assert.False(ScrollAnimationHelper.IsVerticalAnimationActive(scrollViewer));
            });
        });
    }

    [Fact]
    public void NewAnimationRetargetsExistingVerticalAnimation()
    {
        RunSta(() =>
        {
            WithScrollableViewer(scrollViewer =>
            {
                ScrollAnimationHelper.SmoothScrollToVerticalOffset(scrollViewer, 80, TimeSpan.FromSeconds(1));
                ScrollAnimationHelper.SmoothScrollToVerticalOffset(scrollViewer, 160, TimeSpan.FromSeconds(1));

                Assert.True(ScrollAnimationHelper.IsVerticalAnimationActive(scrollViewer));
                Assert.Equal(160, ScrollAnimationHelper.GetCurrentVerticalAnimationTarget(scrollViewer));

                ScrollAnimationHelper.CancelVerticalAnimation(scrollViewer);
            });
        });
    }

    [Fact]
    public void FindNearestScrollableAncestorReturnsNullWhenNoScrollViewerExists()
    {
        RunSta(() =>
        {
            var target = new Button();
            var parent = new Grid();
            parent.Children.Add(target);

            Assert.Null(ScrollViewerSearchHelper.FindNearestScrollableAncestor(target));
        });
    }

    [Fact]
    public void FindNearestScrollableAncestorFindsScrollableAncestor()
    {
        RunSta(() =>
        {
            var target = new Button();
            var panel = new StackPanel();
            panel.Children.Add(target);
            panel.Children.Add(new Border { Height = 600 });

            WithScrollableViewer(scrollViewer =>
            {
                Assert.Same(scrollViewer, ScrollViewerSearchHelper.FindNearestScrollableAncestor(target));
            }, panel);
        });
    }

    [Fact]
    public void FindNearestScrollableAncestorSkipsNonScrollableInnerViewer()
    {
        RunSta(() =>
        {
            var target = new Button();
            var innerViewer = CreateNonScrollableViewer(target);
            var outerContent = new StackPanel();
            outerContent.Children.Add(innerViewer);
            outerContent.Children.Add(new Border { Height = 600 });

            WithScrollableViewer(outerViewer =>
            {
                Assert.Same(outerViewer, ScrollViewerSearchHelper.FindNearestScrollableAncestor(target));
            }, outerContent);
        });
    }

    private static void WithScrollableViewer(Action<ScrollViewer> action, UIElement? content = null)
    {
        var scrollViewer = new ScrollViewer
        {
            Width = 100,
            Height = 100,
            Content = content ?? new Border { Height = 500, Width = 100 }
        };

        var window = new Window
        {
            Width = 120,
            Height = 120,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Content = scrollViewer
        };

        try
        {
            window.Show();
            window.UpdateLayout();
            scrollViewer.UpdateLayout();
            action(scrollViewer);
        }
        finally
        {
            window.Close();
        }
    }

    private static ScrollViewer CreateNonScrollableViewer(UIElement content)
    {
        var scrollViewer = new ScrollViewer
        {
            Width = 100,
            Height = 100,
            Content = content
        };

        MeasureAndArrange(scrollViewer);
        return scrollViewer;
    }

    private static void MeasureAndArrange(FrameworkElement element)
    {
        element.Measure(new Size(element.Width, element.Height));
        element.Arrange(new Rect(0, 0, element.Width, element.Height));
        element.UpdateLayout();
    }

    private static void RunSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }
    }
}
