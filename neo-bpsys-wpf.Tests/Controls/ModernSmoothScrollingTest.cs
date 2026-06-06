#nullable enable

using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    public void ModernScrollViewerHandlesNormalMouseWheelOnPlainContent()
    {
        RunSta(() =>
        {
            var source = new Border { Height = 500, Width = 100 };

            WithModernScrollableViewer(scrollViewer =>
            {
                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);

                var handled = ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    wheelScrollMultiplier: 1,
                    scrollAnimationDuration: 0,
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: source);

                Assert.True(handled);
                Assert.True(args.Handled);
                scrollViewer.UpdateLayout();
                Assert.True(scrollViewer.VerticalOffset > 0);
            }, source);
        });
    }

    [Fact]
    public void ModernScrollViewerDoesNotHandleWheelWhenEventIsAlreadyHandled()
    {
        RunSta(() =>
        {
            WithModernScrollableViewer(scrollViewer =>
            {
                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);
                args.Handled = true;

                var handled = ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    wheelScrollMultiplier: 1,
                    scrollAnimationDuration: 0,
                    isSmoothScrollingEnabled: true,
                    easingFunction: null);

                Assert.False(handled);
                Assert.Equal(0, scrollViewer.VerticalOffset);
            });
        });
    }

    [Fact]
    public void ModernScrollViewerDoesNotHandleWheelWhenSourceIsInsideOpenedComboBox()
    {
        RunSta(() =>
        {
            var comboBox = new ComboBox
            {
                Width = 80,
                ItemsSource = Enumerable.Range(0, 20).Select(index => $"Item {index}").ToArray()
            };
            var content = new StackPanel
            {
                Children =
                {
                    comboBox,
                    new Border { Height = 500, Width = 100 }
                }
            };

            WithModernScrollableViewer(scrollViewer =>
            {
                comboBox.IsDropDownOpen = true;
                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);

                Assert.True(comboBox.IsDropDownOpen);
                Assert.True(WheelScrollEventGuard.ShouldSkipSmoothScroll(scrollViewer, args, comboBox));
                Assert.False(ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    wheelScrollMultiplier: 1,
                    scrollAnimationDuration: 0,
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: comboBox));
                Assert.False(args.Handled);
                Assert.Equal(0, scrollViewer.VerticalOffset);
                comboBox.IsDropDownOpen = false;
            }, content);
        });
    }

    [Fact]
    public void SmoothScrollBehaviorDoesNotHandleWheelWhenSourceIsInsideOpenedComboBox()
    {
        RunSta(() =>
        {
            var comboBox = new ComboBox
            {
                Width = 80,
                ItemsSource = Enumerable.Range(0, 20).Select(index => $"Item {index}").ToArray()
            };
            var content = new StackPanel
            {
                Children =
                {
                    comboBox,
                    new Border { Height = 500, Width = 100 }
                }
            };
            var scrollViewer = new ScrollViewer
            {
                Width = 100,
                Height = 100,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = content
            };
            var window = CreateHiddenWindow(scrollViewer);

            try
            {
                SmoothScrollBehavior.SetDuration(scrollViewer, 0);
                SmoothScrollBehavior.SetIsEnabled(scrollViewer, true);
                window.Show();
                window.UpdateLayout();
                scrollViewer.UpdateLayout();

                comboBox.IsDropDownOpen = true;
                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);

                Assert.True(comboBox.IsDropDownOpen);
                Assert.True(WheelScrollEventGuard.ShouldSkipSmoothScroll(scrollViewer, args, comboBox));
                Assert.False(ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    SmoothScrollBehavior.GetWheelMultiplier(scrollViewer),
                    SmoothScrollBehavior.GetDuration(scrollViewer),
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: comboBox));
                Assert.False(args.Handled);
                Assert.Equal(0, scrollViewer.VerticalOffset);
            }
            finally
            {
                SmoothScrollBehavior.SetIsEnabled(scrollViewer, false);
                comboBox.IsDropDownOpen = false;
                window.Close();
            }
        });
    }

    [Fact]
    public void ModernScrollViewerDoesNotHandleWheelWhenSourceIsInsideNestedListBox()
    {
        RunSta(() =>
        {
            var listBox = new ListBox
            {
                Height = 80,
                ItemsSource = Enumerable.Range(0, 20).Select(index => $"Item {index}").ToArray()
            };
            var content = new StackPanel
            {
                Children =
                {
                    listBox,
                    new Border { Height = 500, Width = 100 }
                }
            };

            WithModernScrollableViewer(scrollViewer =>
            {
                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);
                Assert.True(WheelScrollEventGuard.ShouldSkipSmoothScroll(scrollViewer, args, listBox));
                Assert.False(ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    wheelScrollMultiplier: 1,
                    scrollAnimationDuration: 0,
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: listBox));
                Assert.False(args.Handled);
                Assert.Equal(0, scrollViewer.VerticalOffset);
            }, content);
        });
    }

    [Fact]
    public void ModernScrollViewerDoesNotHandleWheelWhenSourceIsInsideNestedListView()
    {
        RunSta(() =>
        {
            var listView = new ListView
            {
                Height = 80,
                ItemsSource = Enumerable.Range(0, 20).Select(index => $"Item {index}").ToArray()
            };
            var content = new StackPanel
            {
                Children =
                {
                    listView,
                    new Border { Height = 500, Width = 100 }
                }
            };

            WithModernScrollableViewer(scrollViewer =>
            {
                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);
                Assert.True(WheelScrollEventGuard.ShouldSkipSmoothScroll(scrollViewer, args, listView));
                Assert.False(ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    wheelScrollMultiplier: 1,
                    scrollAnimationDuration: 0,
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: listView));
                Assert.False(args.Handled);
                Assert.Equal(0, scrollViewer.VerticalOffset);
            }, content);
        });
    }

    [Fact]
    public void SmoothScrollBehaviorHandlesNormalMouseWheelOnPlainContent()
    {
        RunSta(() =>
        {
            var source = new Border { Height = 500, Width = 100 };
            var scrollViewer = new ScrollViewer
            {
                Width = 100,
                Height = 100,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = source
            };
            var window = CreateHiddenWindow(scrollViewer);

            try
            {
                SmoothScrollBehavior.SetDuration(scrollViewer, 0);
                SmoothScrollBehavior.SetIsEnabled(scrollViewer, true);
                window.Show();
                window.UpdateLayout();
                scrollViewer.UpdateLayout();

                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);
                var handled = ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    SmoothScrollBehavior.GetWheelMultiplier(scrollViewer),
                    SmoothScrollBehavior.GetDuration(scrollViewer),
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: source);

                Assert.True(handled);
                Assert.True(args.Handled);
                scrollViewer.UpdateLayout();
                Assert.True(scrollViewer.VerticalOffset > 0);
            }
            finally
            {
                SmoothScrollBehavior.SetIsEnabled(scrollViewer, false);
                window.Close();
            }
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
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content ?? new Border { Height = 500, Width = 100 }
        };

        var window = CreateHiddenWindow(scrollViewer);

        try
        {
            window.Show();
            scrollViewer.ApplyTemplate();
            window.UpdateLayout();
            scrollViewer.UpdateLayout();
            action(scrollViewer);
        }
        finally
        {
            window.Close();
        }
    }

    private static void WithModernScrollableViewer(Action<ModernScrollViewer> action, UIElement? content = null)
    {
        var scrollViewer = new ModernScrollViewer
        {
            Width = 100,
            Height = 100,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ScrollAnimationDuration = 0,
            Content = content ?? new Border { Height = 500, Width = 100 }
        };

        var window = CreateHiddenWindow(scrollViewer);

        try
        {
            window.Show();
            scrollViewer.ApplyTemplate();
            window.UpdateLayout();
            scrollViewer.UpdateLayout();
            action(scrollViewer);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateHiddenWindow(UIElement content)
    {
        return new Window
        {
            Width = 120,
            Height = 120,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Content = content
        };
    }

    private static MouseWheelEventArgs CreateWheelArgs(int delta)
    {
        return new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
        {
            RoutedEvent = Mouse.MouseWheelEvent
        };
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
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("STA test thread did not finish within 10 seconds.");
        }

        exception?.Throw();
    }
}
