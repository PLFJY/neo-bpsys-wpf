#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using neo_bpsys_wpf.Controls.Modern.Scrolling;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

[Collection(WpfUiCollectionDefinition.Name)]
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
    public void ModernScrollViewerHoverWheelUsesPlainContentWithoutFocus()
    {
        RunSta(() =>
        {
            var source = new Border { Height = 500, Width = 100 };

            WithModernScrollableViewer(scrollViewer =>
            {
                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);

                Assert.NotSame(scrollViewer, Keyboard.FocusedElement);
                Assert.True(WheelScrollEventGuard.ShouldOwnerHandleHoverWheel(scrollViewer, args, source));
                Assert.True(ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    wheelScrollMultiplier: 1,
                    scrollAnimationDuration: 0,
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: source));
                scrollViewer.UpdateLayout();
                Assert.True(scrollViewer.VerticalOffset > 0);
            }, source);
        });
    }

    [Fact]
    public void ModernScrollViewerHoverWheelDoesNotUseHandledEvent()
    {
        RunSta(() =>
        {
            var source = new Border { Height = 500, Width = 100 };

            WithModernScrollableViewer(scrollViewer =>
            {
                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);
                args.Handled = true;

                Assert.False(WheelScrollEventGuard.ShouldOwnerHandleHoverWheel(scrollViewer, args, source));
                Assert.False(ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    wheelScrollMultiplier: 1,
                    scrollAnimationDuration: 0,
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: source));
                Assert.Equal(0, scrollViewer.VerticalOffset);
            }, source);
        });
    }

    [Fact]
    public void ModernScrollViewerHoverWheelRequiresMouseOverOwner()
    {
        RunSta(() =>
        {
            var ownerContent = new Border { Height = 500, Width = 100 };
            var outside = new Border { Height = 20, Width = 100 };

            WithModernScrollableViewer(scrollViewer =>
            {
                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);

                Assert.False(WheelScrollEventGuard.ShouldOwnerHandleHoverWheel(scrollViewer, args, outside));
            }, ownerContent);
        });
    }

    [Fact]
    public void ModernScrollViewerHoverWheelIgnoresUnrelatedNestedListSource()
    {
        RunSta(() =>
        {
            var source = new Border { Height = 500, Width = 100 };
            var unrelatedListBox = new ListBox
            {
                ItemsSource = Enumerable.Range(0, 20).Select(index => $"Item {index}").ToArray()
            };

            WithModernScrollableViewer(scrollViewer =>
            {
                var args = CreateWheelArgs(-Mouse.MouseWheelDeltaForOneLine);
                args.Source = unrelatedListBox;

                Assert.True(WheelScrollEventGuard.ShouldOwnerHandleHoverWheel(scrollViewer, args, source));
            }, source);
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
                Assert.False(WheelScrollEventGuard.ShouldOwnerHandleHoverWheel(scrollViewer, args, comboBox));
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
    public void ModernScrollViewerHandlesWheelWhenSourceIsInsideUnconstrainedListBox()
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
                Assert.True(WheelScrollEventGuard.ShouldOwnerHandleHoverWheel(scrollViewer, args, listBox));
                Assert.False(WheelScrollEventGuard.ShouldSkipSmoothScroll(scrollViewer, args, listBox));
                Assert.True(ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    wheelScrollMultiplier: 1,
                    scrollAnimationDuration: 0,
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: listBox));
                Assert.True(args.Handled);
                scrollViewer.UpdateLayout();
                Assert.True(scrollViewer.VerticalOffset > 0);
            }, content);
        });
    }

    [Fact]
    public void ModernScrollViewerHandlesWheelWhenSourceIsInsideUnconstrainedListView()
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
                Assert.True(WheelScrollEventGuard.ShouldOwnerHandleHoverWheel(scrollViewer, args, listView));
                Assert.False(WheelScrollEventGuard.ShouldSkipSmoothScroll(scrollViewer, args, listView));
                Assert.True(ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    wheelScrollMultiplier: 1,
                    scrollAnimationDuration: 0,
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: listView));
                Assert.True(args.Handled);
                scrollViewer.UpdateLayout();
                Assert.True(scrollViewer.VerticalOffset > 0);
            }, content);
        });
    }

    [Fact]
    public void ModernScrollViewerDoesNotPreviewHandleExplicitSelfRegion()
    {
        RunSta(() =>
        {
            var listBox = new ListBox
            {
                Height = 80,
                ItemsSource = Enumerable.Range(0, 20).Select(index => $"Item {index}").ToArray()
            };
            ModernScroll.SetOwnership(listBox, ModernScrollOwnership.Self);
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

                Assert.False(WheelScrollEventGuard.ShouldOwnerHandleHoverWheel(scrollViewer, args, listBox));
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
    public void NestedSmoothScrollBehaviorScrollsExplicitSelfListBox()
    {
        RunSta(() =>
        {
            var listBox = new ListBox
            {
                Width = 100,
                Height = 80,
                ItemsSource = Enumerable.Range(0, 40).Select(index => $"Item {index}").ToArray()
            };
            ModernScroll.SetOwnership(listBox, ModernScrollOwnership.Self);
            NestedSmoothScrollBehavior.SetDuration(listBox, 0);
            NestedSmoothScrollBehavior.SetIsEnabled(listBox, true);

            var window = CreateHiddenWindow(listBox);
            try
            {
                window.Show();
                window.UpdateLayout();
                listBox.UpdateLayout();

                var internalScrollViewer = Assert.Single(FindVisualDescendants<ScrollViewer>(listBox));
                var args = CreatePreviewWheelArgs(-Mouse.MouseWheelDeltaForOneLine);
                listBox.RaiseEvent(args);

                Assert.True(args.Handled);
                internalScrollViewer.UpdateLayout();
                Assert.True(internalScrollViewer.VerticalOffset > 0);
            }
            finally
            {
                NestedSmoothScrollBehavior.SetIsEnabled(listBox, false);
                window.Close();
            }
        });
    }

    [Fact]
    public void NestedSmoothScrollBehaviorDoesNotTrapWhenInternalScrollViewerCannotScrollFurther()
    {
        RunSta(() =>
        {
            var listBox = new ListBox
            {
                Width = 100,
                Height = 80,
                ItemsSource = Enumerable.Range(0, 2).Select(index => $"Item {index}").ToArray()
            };
            ModernScroll.SetOwnership(listBox, ModernScrollOwnership.Self);
            NestedSmoothScrollBehavior.SetDuration(listBox, 0);
            NestedSmoothScrollBehavior.SetIsEnabled(listBox, true);

            var window = CreateHiddenWindow(listBox);
            try
            {
                window.Show();
                window.UpdateLayout();
                listBox.UpdateLayout();

                var args = CreatePreviewWheelArgs(-Mouse.MouseWheelDeltaForOneLine);
                listBox.RaiseEvent(args);

                Assert.False(args.Handled);
            }
            finally
            {
                NestedSmoothScrollBehavior.SetIsEnabled(listBox, false);
                window.Close();
            }
        });
    }

    [Fact]
    public void ComboBoxDropdownSmoothScrollScrollsDropdownWithoutScrollingOwner()
    {
        RunSta(() =>
        {
            var comboBox = new ComboBox
            {
                Width = 100,
                ItemsSource = Enumerable.Range(0, 80).Select(index => $"Item {index}").ToArray()
            };
            ComboBoxDropdownSmoothScrollBehavior.SetDuration(comboBox, 0);
            ComboBoxDropdownSmoothScrollBehavior.SetIsEnabled(comboBox, true);

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
                FlushDispatcher(comboBox.Dispatcher);

                var popup = Assert.IsType<System.Windows.Controls.Primitives.Popup>(comboBox.Template.FindName("PART_Popup", comboBox));
                var dropdownScrollViewer = Assert.Single(FindVisualDescendants<ScrollViewer>(popup.Child));
                var args = CreatePreviewWheelArgs(-Mouse.MouseWheelDeltaForOneLine);
                dropdownScrollViewer.RaiseEvent(args);

                Assert.True(args.Handled);
                dropdownScrollViewer.UpdateLayout();
                Assert.True(dropdownScrollViewer.VerticalOffset > 0);
                Assert.Equal(0, scrollViewer.VerticalOffset);

                comboBox.IsDropDownOpen = false;
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
    public void SmoothScrollBehaviorHoverWheelUsesSameGuardWithoutFocus()
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

                Assert.NotSame(scrollViewer, Keyboard.FocusedElement);
                Assert.True(WheelScrollEventGuard.ShouldOwnerHandleHoverWheel(scrollViewer, args, source));
                Assert.True(ModernScrollViewer.TryHandleSmoothVerticalWheelScroll(
                    scrollViewer,
                    args,
                    SmoothScrollBehavior.GetWheelMultiplier(scrollViewer),
                    SmoothScrollBehavior.GetDuration(scrollViewer),
                    isSmoothScrollingEnabled: true,
                    easingFunction: null,
                    explicitSource: source));
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

    private static MouseWheelEventArgs CreatePreviewWheelArgs(int delta)
    {
        return new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
        {
            RoutedEvent = Mouse.PreviewMouseWheelEvent
        };
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void FlushDispatcher(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(() => frame.Continue = false, DispatcherPriority.ApplicationIdle);
        Dispatcher.PushFrame(frame);
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
        WpfTestThread.Run(action);
    }
}
