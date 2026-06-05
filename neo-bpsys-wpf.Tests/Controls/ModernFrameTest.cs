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
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
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
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

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

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        exception?.Throw();
    }
}
