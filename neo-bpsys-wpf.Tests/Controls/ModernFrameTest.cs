#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using neo_bpsys_wpf.Controls.Modern.Frame;
using neo_bpsys_wpf.Controls.Modern.Scrolling;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

[Collection(WpfUiCollectionDefinition.Name)]
public class ModernFrameTest
{
    [Fact]
    public void NavigateToFrameworkElementUpdatesCurrentContent()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame();
            var content = new UserControl();

            Assert.True(frame.Navigate(content, new SuppressNavigationTransitionInfo()));

            Assert.Same(content, frame.CurrentContent);
        });
    }

    [Fact]
    public void NavigateToPageTypeUsesActivatorFallback()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame();

            Assert.True(frame.Navigate(typeof(TestPage), null, new SuppressNavigationTransitionInfo()));

            Assert.IsType<TestPage>(frame.CurrentContent);
        });
    }

    [Fact]
    public void NavigateToPageTypeUsesServiceProviderWhenAvailable()
    {
        RunSta(() =>
        {
            var expected = new TestPage();
            var frame = new ModernFrame
            {
                ServiceProvider = new TestServiceProvider(new Dictionary<Type, object>
                {
                    [typeof(TestPage)] = expected
                })
            };

            Assert.True(frame.Navigate(typeof(TestPage), null, new SuppressNavigationTransitionInfo()));

            Assert.Same(expected, frame.CurrentContent);
        });
    }

    [Fact]
    public void GoBackReturnsFalseWhenJournalIsEmpty()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame();

            Assert.False(frame.GoBack());
            Assert.False(frame.CanGoBack);
        });
    }

    [Fact]
    public void GoBackNavigatesToPreviousContent()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame();
            var first = new Border();
            var second = new Button();

            frame.Navigate(first, new SuppressNavigationTransitionInfo());
            frame.Navigate(second, new SuppressNavigationTransitionInfo());

            Assert.True(frame.CanGoBack);
            Assert.True(frame.GoBack());

            Assert.Same(first, frame.CurrentContent);
            Assert.False(frame.CanGoBack);
        });
    }

    [Fact]
    public void ClearJournalClearsBackHistory()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame();
            frame.Navigate(new Border(), new SuppressNavigationTransitionInfo());
            frame.Navigate(new Button(), new SuppressNavigationTransitionInfo());

            frame.ClearJournal();

            Assert.False(frame.CanGoBack);
            Assert.False(frame.GoBack());
        });
    }

    [Fact]
    public void SuppressTransitionSwapsContentImmediately()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame();
            var first = new Border();
            var second = new Button();

            frame.Navigate(first, new SuppressNavigationTransitionInfo());
            frame.Navigate(second, new SuppressNavigationTransitionInfo());

            Assert.Same(second, frame.CurrentContent);
            Assert.Null(first.Parent);
        });
    }

    [Fact]
    public void RapidNavigationDoesNotThrow()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                TransitionDuration = TimeSpan.FromMilliseconds(500)
            };

            frame.Navigate(new Border(), new EntranceNavigationTransitionInfo());
            frame.Navigate(new Button(), new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            frame.Navigate(new TextBlock(), new SuppressNavigationTransitionInfo());

            Assert.IsType<TextBlock>(frame.CurrentContent);
        });
    }

    [Fact]
    public void DefaultScrollHostIsModernScrollViewer()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame();

            Assert.IsType<ModernScrollViewer>(frame.ContentScrollHost);
            Assert.True(frame.IsContentScrollHostEnabled);
            Assert.Equal(ModernFrameContentScrollHostMode.Enabled, frame.ContentScrollHostMode);
        });
    }

    [Fact]
    public void EnabledContentScrollHostModeUsesScrollHost()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Enabled
            };
            var content = new UserControl
            {
                Content = new ListView()
            };

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            Assert.True(IsUsingFrameScrollHost(frame, content));
        });
    }

    [Fact]
    public void DisabledContentScrollHostModeUsesDirectPresenter()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Disabled
            };
            var content = new WrapPanel();

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            Assert.False(IsUsingFrameScrollHost(frame, content));
        });
    }

    [Fact]
    public void IsContentScrollHostEnabledFalseStillUsesDirectPresenter()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                IsContentScrollHostEnabled = false,
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Enabled
            };
            var content = new WrapPanel();

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            Assert.False(IsUsingFrameScrollHost(frame, content));
        });
    }

    [Fact]
    public void AutoContentScrollHostModeWithListViewUsesFrameScrollHost()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Auto
            };
            var content = new UserControl
            {
                Content = new Grid
                {
                    Children = { new ListView() }
                }
            };

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            Assert.True(IsUsingFrameScrollHost(frame, content));
        });
    }

    [Fact]
    public void AutoContentScrollHostModeWithListBoxUsesFrameScrollHost()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Auto
            };
            var content = new UserControl
            {
                Content = new Border
                {
                    Child = new ListBox()
                }
            };

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            Assert.True(IsUsingFrameScrollHost(frame, content));
        });
    }

    [Fact]
    public void AutoContentScrollHostModeWithScrollViewerUsesFrameScrollHost()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Auto
            };
            var content = new UserControl
            {
                Content = new ScrollViewer()
            };

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            Assert.True(IsUsingFrameScrollHost(frame, content));
        });
    }

    [Fact]
    public void AutoContentScrollHostModeWithDynamicScrollViewerUsesFrameScrollHost()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Auto
            };
            var content = new UserControl
            {
                Content = new Wpf.Ui.Controls.DynamicScrollViewer()
            };

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            Assert.True(IsUsingFrameScrollHost(frame, content));
        });
    }

    [Fact]
    public void AutoContentScrollHostModeWithExplicitSelfOwnershipUsesDirectPresenter()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Auto
            };
            var content = new UserControl
            {
                Content = new Grid()
            };
            ModernScroll.SetOwnership(content, ModernScrollOwnership.Self);

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            Assert.False(IsUsingFrameScrollHost(frame, content));
        });
    }

    [Fact]
    public void AutoContentScrollHostModeWithExplicitFrameOwnershipUsesFrameScrollHost()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Auto
            };
            var content = new UserControl
            {
                Content = new ScrollViewer()
            };
            ModernScroll.SetOwnership(content, ModernScrollOwnership.Frame);

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            Assert.True(IsUsingFrameScrollHost(frame, content));
        });
    }

    [Fact]
    public void AutoContentScrollHostModeWithSimpleWrapPanelUsesScrollHost()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Auto
            };
            var content = new WrapPanel
            {
                Children =
                {
                    new Border { Height = 120 },
                    new Border { Height = 120 }
                }
            };

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            Assert.True(IsUsingFrameScrollHost(frame, content));
        });
    }

    [Fact]
    public void SwitchingAutoContentScrollHostModesUpdatesActiveHost()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Auto
            };
            var simpleContent = new WrapPanel();
            var selfScrollContent = new UserControl
            {
                Content = new ListBox()
            };
            ModernScroll.SetOwnership(selfScrollContent, ModernScrollOwnership.Self);

            frame.Navigate(simpleContent, new SuppressNavigationTransitionInfo());
            Assert.True(IsUsingFrameScrollHost(frame, simpleContent));

            frame.Navigate(selfScrollContent, new SuppressNavigationTransitionInfo());
            Assert.False(IsUsingFrameScrollHost(frame, selfScrollContent));

            frame.Navigate(new WrapPanel(), new SuppressNavigationTransitionInfo());
            Assert.Equal(Visibility.Visible, frame.ContentScrollHost.Visibility);
        });
    }

    [Fact]
    public void ScrollViewerSearchFindsModernFrameScrollHostFromHostedContent()
    {
        RunSta(() =>
        {
            var target = new Button();
            var content = new StackPanel
            {
                Children =
                {
                    target,
                    new Border { Height = 600 }
                }
            };

            var frame = new ModernFrame
            {
                Width = 100,
                Height = 100
            };
            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            var window = new Window
            {
                Width = 120,
                Height = 120,
                Left = -10000,
                Top = -10000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Content = frame
            };

            try
            {
                window.Show();
                window.UpdateLayout();
                frame.UpdateLayout();

                Assert.Same(frame.ContentScrollHost, ScrollViewerSearchHelper.FindNearestScrollableAncestor(target));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void NavigateResetsContentScrollHostVerticalOffset()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                Width = 120,
                Height = 120,
                TransitionDuration = TimeSpan.Zero
            };
            var window = CreateHiddenWindow(frame);

            try
            {
                window.Show();
                frame.Navigate(new TallContent(), new SuppressNavigationTransitionInfo());
                window.UpdateLayout();
                frame.ContentScrollHost.ApplyTemplate();
                frame.ContentScrollHost.UpdateLayout();
                Assert.True(frame.ContentScrollHost.ScrollableHeight > 0);
                frame.ContentScrollHost.ScrollToVerticalOffset(160);
                frame.ContentScrollHost.UpdateLayout();
                Assert.True(frame.ContentScrollHost.VerticalOffset > 0);

                frame.Navigate(new TallContent(), new SuppressNavigationTransitionInfo());
                window.UpdateLayout();

                Assert.Equal(0D, frame.ContentScrollHost.VerticalOffset);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void NavigateCancelsPendingContentScrollHostVerticalAnimation()
    {
        RunSta(() =>
        {
            if (RenderCapability.Tier == 0 || !SystemParameters.ClientAreaAnimation)
            {
                return;
            }

            var frame = new ModernFrame
            {
                Width = 120,
                Height = 120,
                TransitionDuration = TimeSpan.Zero
            };
            var window = CreateHiddenWindow(frame);

            try
            {
                window.Show();
                frame.Navigate(new TallContent(), new SuppressNavigationTransitionInfo());
                window.UpdateLayout();
                frame.ContentScrollHost.ApplyTemplate();
                frame.ContentScrollHost.UpdateLayout();
                Assert.True(frame.ContentScrollHost.ScrollableHeight > 0);

                ScrollAnimationHelper.SmoothScrollToVerticalOffset(
                    frame.ContentScrollHost,
                    260,
                    TimeSpan.FromSeconds(10));
                Assert.True(ScrollAnimationHelper.IsVerticalAnimationActive(frame.ContentScrollHost));

                frame.Navigate(new TallContent(), new SuppressNavigationTransitionInfo());
                window.UpdateLayout();

                Assert.False(ScrollAnimationHelper.IsVerticalAnimationActive(frame.ContentScrollHost));
                Assert.Equal(0D, frame.ContentScrollHost.VerticalOffset);
            }
            finally
            {
                ScrollAnimationHelper.CancelVerticalAnimation(frame.ContentScrollHost);
                window.Close();
            }
        });
    }

    [Fact]
    public void NavigateWithDirectPresenterDoesNotResetChildSelfScrollRegion()
    {
        RunSta(() =>
        {
            var content = new SelfScrollContent();
            var stagingWindow = CreateHiddenWindow(content);

            try
            {
                stagingWindow.Show();
                content.InnerScrollViewer.ApplyTemplate();
                stagingWindow.UpdateLayout();
                content.InnerScrollViewer.UpdateLayout();
                Assert.True(content.InnerScrollViewer.ScrollableHeight > 0);
                content.InnerScrollViewer.ScrollToVerticalOffset(180);
                content.InnerScrollViewer.UpdateLayout();
                Assert.True(content.InnerScrollViewer.VerticalOffset > 0);
                stagingWindow.Content = null;
            }
            finally
            {
                stagingWindow.Close();
            }

            var frame = new ModernFrame
            {
                Width = 120,
                Height = 120,
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Auto,
                TransitionDuration = TimeSpan.Zero
            };
            var window = CreateHiddenWindow(frame);

            try
            {
                window.Show();
                frame.Navigate(content, new SuppressNavigationTransitionInfo());
                window.UpdateLayout();

                Assert.False(IsUsingFrameScrollHost(frame, content));
                Assert.True(content.InnerScrollViewer.VerticalOffset > 0);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GoBackDoesNotResetContentScrollHostVerticalOffset()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                Width = 120,
                Height = 120,
                TransitionDuration = TimeSpan.Zero
            };
            var window = CreateHiddenWindow(frame);

            try
            {
                window.Show();
                frame.Navigate(new TallContent(), new SuppressNavigationTransitionInfo());
                frame.Navigate(new TallContent(), new SuppressNavigationTransitionInfo());
                window.UpdateLayout();
                frame.ContentScrollHost.ApplyTemplate();
                frame.ContentScrollHost.UpdateLayout();
                Assert.True(frame.ContentScrollHost.ScrollableHeight > 0);
                frame.ContentScrollHost.ScrollToVerticalOffset(160);
                frame.ContentScrollHost.UpdateLayout();
                Assert.True(frame.ContentScrollHost.VerticalOffset > 0);

                Assert.True(frame.GoBack());
                window.UpdateLayout();

                Assert.True(frame.ContentScrollHost.VerticalOffset > 0);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void NavigateResetsContentScrollHostBeforeNavigatedEvent()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                Width = 120,
                Height = 120,
                TransitionDuration = TimeSpan.Zero
            };
            var window = CreateHiddenWindow(frame);

            try
            {
                window.Show();
                frame.Navigate(new TallContent(), new SuppressNavigationTransitionInfo());
                window.UpdateLayout();
                frame.ContentScrollHost.ApplyTemplate();
                frame.ContentScrollHost.UpdateLayout();
                Assert.True(frame.ContentScrollHost.ScrollableHeight > 0);
                frame.ContentScrollHost.ScrollToVerticalOffset(160);
                frame.ContentScrollHost.UpdateLayout();
                Assert.True(frame.ContentScrollHost.VerticalOffset > 0);

                double? offsetDuringNavigated = null;
                frame.Navigated += (_, _) => offsetDuringNavigated = frame.ContentScrollHost.VerticalOffset;

                frame.Navigate(new TallContent(), new SuppressNavigationTransitionInfo());

                Assert.Equal(0D, offsetDuringNavigated);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void GuidanceStyleScrollCanOverrideNavigationReset()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame
            {
                Width = 120,
                Height = 120,
                TransitionDuration = TimeSpan.Zero
            };
            var content = new TallContent(spacerBeforeTarget: 260);
            var window = CreateHiddenWindow(frame);

            try
            {
                window.Show();
                frame.Navigate(new TallContent(), new SuppressNavigationTransitionInfo());
                window.UpdateLayout();
                frame.ContentScrollHost.ApplyTemplate();
                frame.ContentScrollHost.UpdateLayout();
                Assert.True(frame.ContentScrollHost.ScrollableHeight > 0);
                frame.ContentScrollHost.ScrollToVerticalOffset(160);
                frame.ContentScrollHost.UpdateLayout();
                Assert.True(frame.ContentScrollHost.VerticalOffset > 0);

                frame.Navigated += (_, _) =>
                {
                    frame.Dispatcher.BeginInvoke(
                        () => GuidanceScrollHelper.ScrollElementIntoView(content.Target, topMargin: 0, animated: false),
                        DispatcherPriority.ContextIdle);
                };

                frame.Navigate(content, new SuppressNavigationTransitionInfo());
                FlushDispatcher(window.Dispatcher);
                window.UpdateLayout();

                Assert.True(frame.ContentScrollHost.VerticalOffset > 0);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PageContentIsHostedByFrame()
    {
        RunSta(() =>
        {
            var page = new TestPageWithTarget();
            var frame = new ModernFrame
            {
                Width = 120,
                Height = 120
            };
            frame.Navigate(page, new SuppressNavigationTransitionInfo());

            var window = CreateHiddenWindow(frame);

            try
            {
                window.Show();
                FlushDispatcher(window.Dispatcher);
                window.UpdateLayout();

                var pageHost = FindVisualDescendants<System.Windows.Controls.Frame>(frame)
                    .FirstOrDefault(x => ReferenceEquals(x.Content, page));

                Assert.NotNull(pageHost);
                Assert.True(page.Target.IsLoaded);
                Assert.All(
                    FindVisualDescendants<ContentPresenter>(frame)
                        .Where(presenter => ReferenceEquals(presenter.Content, page)),
                    presenter => Assert.True(IsVisualDescendantOf(presenter, pageHost)));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SuppressTransitionLeavesActiveHostVisible()
    {
        RunSta(() =>
        {
            var frame = new ModernFrame();

            frame.Navigate(new Border(), new SuppressNavigationTransitionInfo());

            Assert.Equal(Visibility.Visible, frame.ContentScrollHost.Visibility);
            Assert.True(frame.ContentScrollHost.IsHitTestVisible);
            Assert.Equal(1D, frame.ContentScrollHost.Opacity);
        });
    }

    [Fact]
    public void HostedContentInheritsFrameDataContext()
    {
        RunSta(() =>
        {
            var expectedDataContext = new object();
            var content = new Border();
            var frame = new ModernFrame
            {
                DataContext = expectedDataContext
            };

            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            var window = CreateHiddenWindow(frame);

            try
            {
                window.Show();
                window.UpdateLayout();

                Assert.Same(expectedDataContext, content.DataContext);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void HostedContentCanResolveParentResources()
    {
        RunSta(() =>
        {
            var content = new Border();
            var frame = new ModernFrame();
            frame.Resources["ModernFrameTestResource"] = "resource-value";
            frame.Navigate(content, new SuppressNavigationTransitionInfo());

            var window = CreateHiddenWindow(frame);

            try
            {
                window.Show();
                window.UpdateLayout();

                Assert.Equal("resource-value", content.FindResource("ModernFrameTestResource"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RapidAnimatedNavigationLeavesOldContentNonInteractive()
    {
        RunSta(() =>
        {
            var first = new Button();
            var second = new TextBlock();
            var frame = new ModernFrame
            {
                TransitionDuration = TimeSpan.FromMilliseconds(500)
            };

            frame.Navigate(first, new SuppressNavigationTransitionInfo());
            frame.Navigate(second, new EntranceNavigationTransitionInfo());

            var oldPresenter = FindVisualDescendants<ContentPresenter>(frame)
                .FirstOrDefault(p => ReferenceEquals(p.Content, first));

            Assert.NotNull(oldPresenter);
            Assert.False(oldPresenter.IsHitTestVisible);
            Assert.Same(second, frame.CurrentContent);
        });
    }

    [Fact]
    public void AnimatedTransitionKeepsNewHostHiddenWhileOldPresenterExits()
    {
        RunSta(() =>
        {
            if (RenderCapability.Tier == 0 || !SystemParameters.ClientAreaAnimation)
            {
                return;
            }

            var first = new Border();
            var second = new TextBlock();
            var frame = new ModernFrame
            {
                Width = 120,
                Height = 120,
                TransitionDuration = TimeSpan.FromMilliseconds(200)
            };
            frame.Navigate(first, new SuppressNavigationTransitionInfo());

            var window = CreateHiddenWindow(frame);
            try
            {
                window.Show();
                window.UpdateLayout();

                frame.Navigate(second, new EntranceNavigationTransitionInfo());
                FlushDispatcher(window.Dispatcher);

                var oldPresenter = FindVisualDescendants<ContentPresenter>(frame)
                    .FirstOrDefault(p => ReferenceEquals(p.Content, first));

                Assert.NotNull(oldPresenter);
                Assert.Equal(Visibility.Visible, oldPresenter.Visibility);
                Assert.Equal(0D, frame.ContentScrollHost.Opacity);
                Assert.False(frame.ContentScrollHost.IsHitTestVisible);
                Assert.Same(second, frame.CurrentContent);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void AnimatedTransitionTargetsDirectPresenterWhenContentDeclaresSelfOwnership()
    {
        RunSta(() =>
        {
            if (RenderCapability.Tier == 0 || !SystemParameters.ClientAreaAnimation)
            {
                return;
            }

            var first = new WrapPanel();
            var second = new UserControl
            {
                Content = new ListBox()
            };
            ModernScroll.SetOwnership(second, ModernScrollOwnership.Self);
            var frame = new ModernFrame
            {
                Width = 120,
                Height = 120,
                ContentScrollHostMode = ModernFrameContentScrollHostMode.Auto,
                TransitionDuration = TimeSpan.FromMilliseconds(200)
            };
            frame.Navigate(first, new SuppressNavigationTransitionInfo());

            var window = CreateHiddenWindow(frame);
            try
            {
                window.Show();
                window.UpdateLayout();

                frame.Navigate(second, new EntranceNavigationTransitionInfo());
                FlushDispatcher(window.Dispatcher);

                var directPresenter = FindDirectPresenter(frame, second);

                Assert.NotNull(directPresenter);
                Assert.Equal(Visibility.Visible, directPresenter.Visibility);
                Assert.Equal(0D, directPresenter.Opacity);
                Assert.False(directPresenter.IsHitTestVisible);
                Assert.Equal(Visibility.Collapsed, frame.ContentScrollHost.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void AnimatedTransitionCompletionRestoresActiveHostWithoutClearingIdentityTransform()
    {
        RunSta(() =>
        {
            if (RenderCapability.Tier == 0 || !SystemParameters.ClientAreaAnimation)
            {
                return;
            }

            var first = new Border();
            var second = new TextBlock();
            var frame = new ModernFrame
            {
                Width = 120,
                Height = 120,
                TransitionDuration = TimeSpan.FromMilliseconds(30)
            };
            frame.Navigate(first, new SuppressNavigationTransitionInfo());

            var window = CreateHiddenWindow(frame);
            try
            {
                window.Show();
                window.UpdateLayout();

                frame.Navigate(second, new EntranceNavigationTransitionInfo());
                PumpDispatcher(window.Dispatcher, TimeSpan.FromMilliseconds(160));
                window.UpdateLayout();

                Assert.Equal(Visibility.Visible, frame.ContentScrollHost.Visibility);
                Assert.Equal(1D, frame.ContentScrollHost.Opacity);
                Assert.True(frame.ContentScrollHost.IsHitTestVisible);
                Assert.DoesNotContain(
                    FindVisualDescendants<ContentPresenter>(frame),
                    p => ReferenceEquals(p.Content, first));

                if (frame.ContentScrollHost.RenderTransform is TranslateTransform translateTransform)
                {
                    Assert.Equal(0D, translateTransform.X);
                    Assert.Equal(0D, translateTransform.Y);
                }

                Assert.Same(second, frame.CurrentContent);
            }
            finally
            {
                window.Close();
            }
        });
    }

    public sealed class TestPage : Page
    {
    }

    public sealed class TestPageWithTarget : Page
    {
        public TestPageWithTarget()
        {
            Target = new Button();
            Content = new Grid
            {
                Children = { Target }
            };
        }

        public Button Target { get; }
    }

    private sealed class TallContent : Border
    {
        public TallContent(double spacerBeforeTarget = 0)
        {
            Height = 560;
            Width = 100;
            Target = new Button
            {
                Height = 32,
                Content = "Target"
            };

            Child = new StackPanel
            {
                Children =
                {
                    new Border { Height = spacerBeforeTarget },
                    Target,
                    new Border { Height = 520 }
                }
            };
        }

        public Button Target { get; }
    }

    private sealed class SelfScrollContent : StackPanel
    {
        public SelfScrollContent()
        {
            InnerScrollViewer = new ScrollViewer
            {
                Width = 100,
                Height = 80,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new Border { Height = 520, Width = 100 }
            };

            Children.Add(InnerScrollViewer);
            ModernScroll.SetOwnership(this, ModernScrollOwnership.Self);
        }

        public ScrollViewer InnerScrollViewer { get; }
    }

    private sealed class TestServiceProvider : IServiceProvider
    {
        private readonly IReadOnlyDictionary<Type, object> _services;

        public TestServiceProvider(IReadOnlyDictionary<Type, object> services)
        {
            _services = services;
        }

        public object? GetService(Type serviceType)
        {
            return _services.TryGetValue(serviceType, out var service) ? service : null;
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

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        root.Dispatcher.Invoke(() => root.GetValue(FrameworkElement.TagProperty));
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

    private static bool IsVisualDescendantOf(DependencyObject descendant, DependencyObject ancestor)
    {
        var current = descendant;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static bool IsUsingFrameScrollHost(ModernFrame frame, FrameworkElement content)
    {
        frame.ApplyTemplate();

        if (frame.ContentScrollHost.Visibility == Visibility.Visible)
        {
            return true;
        }

        var directPresenter = FindDirectPresenter(frame, content);
        Assert.NotNull(directPresenter);
        Assert.Equal(Visibility.Visible, directPresenter.Visibility);
        return false;
    }

    private static ContentPresenter? FindDirectPresenter(ModernFrame frame, FrameworkElement content)
    {
        return FindVisualDescendants<ContentPresenter>(frame)
            .FirstOrDefault(presenter =>
                ReferenceEquals(presenter.Content, content)
                && !IsVisualDescendantOf(presenter, frame.ContentScrollHost));
    }

    private static void PumpDispatcher(Dispatcher dispatcher, TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void FlushDispatcher(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        var completed = false;
        var timedOut = false;
        var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        EventHandler onTimeout = (_, _) =>
        {
            timedOut = true;
            timer.Stop();
            frame.Continue = false;
        };

        timer.Tick += onTimeout;
        timer.Start();
        dispatcher.BeginInvoke(() =>
        {
            completed = true;
            frame.Continue = false;
        }, DispatcherPriority.ApplicationIdle);

        Dispatcher.PushFrame(frame);
        timer.Tick -= onTimeout;
        timer.Stop();

        if (!completed || timedOut)
        {
            throw new TimeoutException("Dispatcher did not reach ApplicationIdle within 2 seconds.");
        }
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
        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("STA test thread did not finish within 30 seconds.");
        }

        exception?.Throw();
    }
}
