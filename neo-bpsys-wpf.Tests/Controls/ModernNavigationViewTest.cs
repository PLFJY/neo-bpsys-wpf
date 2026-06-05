#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Controls.Modern.Navigation;
using neo_bpsys_wpf.Controls.Modern.Scrolling;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

public class ModernNavigationViewTest
{
    [Fact]
    public void AcceptsWpfUiNavigationViewItemAndMapsCoreFields()
    {
        RunSta(() =>
        {
            var item = new NavigationViewItem("HomePage", SymbolRegular.Home24, typeof(TestPage))
            {
                TargetPageTag = "home"
            };
            var navigationView = new ModernNavigationView
            {
                MenuItemsSource = new ObservableCollection<NavigationViewItem> { item }
            };

            var entry = Assert.Single(navigationView.MenuEntries);
            Assert.Same(item, entry.SourceItem);
            Assert.Equal("HomePage", entry.LocalizationKey);
            Assert.IsType<SymbolIcon>(entry.Icon);
            Assert.Equal(typeof(TestPage), entry.TargetPageType);
            Assert.Equal("home", entry.TargetPageTag);
        });
    }

    [Fact]
    public void LocalizesStringContentKeys()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                MenuItemsSource = new[] { new NavigationViewItem("HomePage", SymbolRegular.Home24, typeof(TestPage)) }
            };

            var entry = Assert.Single(navigationView.MenuEntries);

            Assert.NotEqual("HomePage", entry.DisplayText);
            Assert.False(string.IsNullOrWhiteSpace(entry.DisplayText));
        });
    }

    [Fact]
    public void MapsSymbolRegularIconsToSymbolIconPresenter()
    {
        RunSta(() =>
        {
            var icon = ModernNavigationIconConverter.CreateIcon(SymbolRegular.Home24);

            var symbolIcon = Assert.IsType<SymbolIcon>(icon);
            Assert.Equal(SymbolRegular.Home24, symbolIcon.Symbol);
        });
    }

    [Fact]
    public void ClonedSymbolIconDoesNotKeepSourceForeground()
    {
        RunSta(() =>
        {
            var source = new SymbolIcon(SymbolRegular.Home24)
            {
                Foreground = Brushes.Black
            };

            var icon = Assert.IsType<SymbolIcon>(ModernNavigationIconConverter.CreateIcon(source));

            Assert.Equal(DependencyProperty.UnsetValue, icon.ReadLocalValue(IconElement.ForegroundProperty));
        });
    }

    [Fact]
    public void ItemButtonForegroundUsesDynamicNavigationViewResource()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView();
            var style = Assert.IsType<Style>(navigationView.Resources["ModernNavigationItemButtonStyle"]);
            var foregroundSetter = style.Setters
                .OfType<Setter>()
                .FirstOrDefault(x => x.Property == Control.ForegroundProperty);

            Assert.NotNull(foregroundSetter);
            var dynamicResource = Assert.IsType<DynamicResourceExtension>(foregroundSetter.Value);
            Assert.Equal("NavigationViewItemForeground", dynamicResource.ResourceKey);
        });
    }

    [Fact]
    public void PaneToggleStyleUsesDynamicNavigationViewResources()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView();
            var style = Assert.IsType<Style>(navigationView.Resources["ModernPaneToggleButtonStyle"]);

            var backgroundSetter = style.Setters
                .OfType<Setter>()
                .FirstOrDefault(x => x.Property == Control.BackgroundProperty);
            var foregroundSetter = style.Setters
                .OfType<Setter>()
                .FirstOrDefault(x => x.Property == Control.ForegroundProperty);

            Assert.NotNull(backgroundSetter);
            Assert.NotNull(foregroundSetter);
            Assert.Equal(
                "NavigationViewItemBackground",
                Assert.IsType<DynamicResourceExtension>(backgroundSetter.Value).ResourceKey);
            Assert.Equal(
                "NavigationViewItemForeground",
                Assert.IsType<DynamicResourceExtension>(foregroundSetter.Value).ResourceKey);
        });
    }

    [Fact]
    public void PaneToggleUsesCompactPaneWidthHitArea()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                CompactPaneLength = 56
            };

            var window = CreateHiddenWindow(navigationView);
            try
            {
                window.Show();
                window.UpdateLayout();

                var toggleButton = FindVisualDescendants<System.Windows.Controls.Button>(navigationView)
                    .FirstOrDefault(button => ReferenceEquals(button.Command, navigationView.TogglePaneCommand));

                Assert.NotNull(toggleButton);
                Assert.Equal(56D, toggleButton.Width);
                Assert.Equal(40D, toggleButton.Height);
                Assert.Same(navigationView.Resources["ModernPaneToggleButtonStyle"], toggleButton.Style);
                Assert.Equal(HorizontalAlignment.Left, toggleButton.HorizontalAlignment);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MenuScrollViewerUsesThinVisibleVerticalScrollbarWhenPaneCollapsed()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                IsPaneOpen = false,
                MenuItemsSource = Enumerable.Range(0, 12)
                    .Select(index => new NavigationViewItem($"HomePage{index}", SymbolRegular.Home24, typeof(TestPage)))
                    .ToArray()
            };

            var window = CreateHiddenWindow(navigationView);
            try
            {
                window.Show();
                window.UpdateLayout();

                var menuScrollViewer = FindVisualDescendants<ModernScrollViewer>(navigationView)
                    .FirstOrDefault(scrollViewer => scrollViewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled);

                Assert.NotNull(menuScrollViewer);
                Assert.Equal(ScrollBarVisibility.Auto, menuScrollViewer.VerticalScrollBarVisibility);

                var verticalScrollBar = FindVisualDescendants<System.Windows.Controls.Primitives.ScrollBar>(menuScrollViewer)
                    .FirstOrDefault(scrollBar => scrollBar.Orientation == Orientation.Vertical);

                Assert.NotNull(verticalScrollBar);
                Assert.Equal(4D, verticalScrollBar.Width);
                Assert.Equal(4D, verticalScrollBar.MinWidth);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MenuScrollViewerUsesAutoVerticalScrollbarWhenPaneOpen()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                IsPaneOpen = true,
                MenuItemsSource = Enumerable.Range(0, 12)
                    .Select(index => new NavigationViewItem($"HomePage{index}", SymbolRegular.Home24, typeof(TestPage)))
                    .ToArray()
            };

            var window = CreateHiddenWindow(navigationView);
            try
            {
                window.Show();
                window.UpdateLayout();

                var menuScrollViewer = FindVisualDescendants<ModernScrollViewer>(navigationView)
                    .FirstOrDefault(scrollViewer => scrollViewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled);

                Assert.NotNull(menuScrollViewer);
                Assert.Equal(ScrollBarVisibility.Auto, menuScrollViewer.VerticalScrollBarVisibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void InvokeItemNavigatesToTargetPageType()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();
            var item = new NavigationViewItem("HomePage", SymbolRegular.Home24, typeof(TestPage));
            navigationView.MenuItemsSource = new[] { item };

            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[0]);

            Assert.IsType<TestPage>(navigationView.CurrentContent);
            Assert.Same(item, navigationView.SelectedItem);
        });
    }

    [Fact]
    public void ClickingCurrentSelectedEntryDoesNotNavigateOrRaiseEvents()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();
            var item = new NavigationViewItem("HomePage", SymbolRegular.Home24, typeof(TestPage));
            navigationView.MenuItemsSource = new[] { item };
            var invokedCount = 0;
            var navigatingCount = 0;
            var navigatedCount = 0;
            navigationView.ItemInvoked += (_, _) => invokedCount++;
            navigationView.Navigating += (_, _) => navigatingCount++;
            navigationView.Navigated += (_, _) => navigatedCount++;

            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[0]);
            var currentContent = navigationView.CurrentContent;
            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[0]);

            Assert.Same(currentContent, navigationView.CurrentContent);
            Assert.Equal(1, invokedCount);
            Assert.Equal(1, navigatingCount);
            Assert.Equal(1, navigatedCount);
            Assert.False(navigationView.CanGoBack);
        });
    }

    [Fact]
    public void ClickingDifferentEntryStillNavigates()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();
            var first = new NavigationViewItem("HomePage", SymbolRegular.Home24, typeof(TestPage));
            var second = new NavigationViewItem("SettingPage", SymbolRegular.Settings24, typeof(SecondTestPage));
            navigationView.MenuItemsSource = new[] { first, second };

            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[0]);
            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[1]);

            Assert.IsType<SecondTestPage>(navigationView.CurrentContent);
            Assert.Same(second, navigationView.SelectedItem);
            Assert.True(navigationView.CanGoBack);
        });
    }

    [Fact]
    public void ExternalNavigateSelectsMatchingItem()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();
            var item = new NavigationViewItem("HomePage", SymbolRegular.Home24, typeof(TestPage));
            navigationView.MenuItemsSource = new[] { item };

            Assert.True(navigationView.Navigate(typeof(TestPage)));

            Assert.Same(item, navigationView.SelectedItem);
            Assert.True(navigationView.MenuEntries[0].IsSelected);
        });
    }

    [Fact]
    public void ExternalNavigateWorksWithoutMatchingItem()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();

            Assert.True(navigationView.Navigate(typeof(TestPage)));

            Assert.IsType<TestPage>(navigationView.CurrentContent);
            Assert.Null(navigationView.SelectedItem);
        });
    }

    [Fact]
    public void NavigateTypeDisplaysPageContentVisibly()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();

            Assert.True(navigationView.Navigate(typeof(ScrollableTestPage)));

            var window = CreateHiddenWindow(navigationView);
            try
            {
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();

                var target = ((ScrollableTestPage)navigationView.CurrentContent!).Target;
                Assert.True(target.IsLoaded);
                Assert.Equal(Visibility.Visible, navigationView.ContentScrollHost.Visibility);
                Assert.Equal(1D, navigationView.ContentScrollHost.Opacity);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FooterItemNavigationUsesSharedSelectionModel()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();
            var footerItem = new NavigationViewItem("SettingPage", SymbolRegular.Settings24, typeof(SecondTestPage));
            navigationView.FooterMenuItemsSource = new[] { footerItem };

            Assert.True(navigationView.Navigate(typeof(SecondTestPage)));

            Assert.Same(footerItem, navigationView.SelectedItem);
            Assert.True(navigationView.FooterMenuEntries[0].IsSelected);
        });
    }

    [Fact]
    public void GoBackDelegatesToModernFrame()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();
            navigationView.Navigate(typeof(TestPage));
            navigationView.Navigate(typeof(SecondTestPage));

            Assert.True(navigationView.GoBack());

            Assert.IsType<TestPage>(navigationView.CurrentContent);
        });
    }

    [Fact]
    public void NavigateWithHierarchyDoesNotThrow()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();

            Assert.True(navigationView.NavigateWithHierarchy(typeof(TestPage)));
            Assert.IsType<TestPage>(navigationView.CurrentContent);
        });
    }

    [Fact]
    public void NavigationServiceCanSetAndUseModernNavigationView()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();
            var settingsHost = new Mock<ISettingsHostService>();
            settingsHost.SetupProperty(x => x.Settings, new Settings());
            var service = new NavigationService(
                new TestPageProvider(),
                settingsHost.Object,
                NullLogger<NavigationService>.Instance);

            service.SetNavigationControl(navigationView);

            Assert.True(service.Navigate(typeof(TestPage)));
            Assert.IsType<TestPage>(navigationView.CurrentContent);
        });
    }

    [Fact]
    public void GameGuidanceStyleNavigateTypeWorksThroughNavigationService()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();
            var settingsHost = new Mock<ISettingsHostService>();
            settingsHost.SetupProperty(x => x.Settings, new Settings());
            var service = new NavigationService(
                new TestPageProvider(),
                settingsHost.Object,
                NullLogger<NavigationService>.Instance);
            service.SetNavigationControl(navigationView);

            Assert.True(service.Navigate(typeof(TestPage)));

            Assert.IsType<TestPage>(navigationView.CurrentContent);
        });
    }

    [Fact]
    public void ModernFrameScrollHostIsDiscoverableAfterNavigation()
    {
        RunSta(() =>
        {
            var navigationView = CreateNavigationViewWithProvider();

            Assert.True(navigationView.Navigate(typeof(ScrollableTestPage)));

            var window = CreateHiddenWindow(navigationView);
            try
            {
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();

                var target = ((ScrollableTestPage)navigationView.CurrentContent!).Target;
                Assert.True(target.IsLoaded);
                Assert.True(navigationView.ContentScrollHost.ScrollableHeight > 0);
                Assert.Same(
                    navigationView.ContentScrollHost,
                    ScrollViewerSearchHelper.FindNearestScrollableAncestor(target));
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static ModernNavigationView CreateNavigationViewWithProvider()
    {
        var navigationView = new ModernNavigationView
        {
            TransitionDuration = 0
        };
        navigationView.SetPageProviderService(new TestPageProvider());
        return navigationView;
    }

    private static Window CreateHiddenWindow(UIElement content)
    {
        return new Window
        {
            Width = 420,
            Height = 160,
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
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
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

    private sealed class TestPageProvider : INavigationViewPageProvider
    {
        public object? GetPage(Type pageType)
        {
            if (pageType == typeof(TestPage))
            {
                return new TestPage();
            }

            if (pageType == typeof(SecondTestPage))
            {
                return new SecondTestPage();
            }

            if (pageType == typeof(ScrollableTestPage))
            {
                return new ScrollableTestPage();
            }

            return Activator.CreateInstance(pageType);
        }
    }

    public sealed class TestPage : Page
    {
    }

    public sealed class SecondTestPage : Page
    {
    }

    public sealed class ScrollableTestPage : Page
    {
        public ScrollableTestPage()
        {
            Target = new System.Windows.Controls.Button();
            Content = new StackPanel
            {
                Children =
                {
                    Target,
                    new Border { Height = 600 }
                }
            };
        }

        public System.Windows.Controls.Button Target { get; }
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
