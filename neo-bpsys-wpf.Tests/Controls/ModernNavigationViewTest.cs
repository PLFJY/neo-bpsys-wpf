#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Controls.Modern.Frame;
using neo_bpsys_wpf.Controls.Modern.Navigation;
using neo_bpsys_wpf.Controls.Modern.Scrolling;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using neo_bpsys_wpf.Views.Pages.Plugin;
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
    public void TopModeUsesSingleInternalFrameAndNoTopFrame()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                NavigationBehavior = ModernNavigationBehavior.LocalTabs,
                TransitionDuration = 0
            };

            Assert.NotNull(navigationView.FindName("PART_Frame"));
            Assert.Null(navigationView.FindName("PART_TopFrame"));
        });
    }

    [Fact]
    public void PageNavigationSetsFrameScrollHostModeToEnabled()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                NavigationBehavior = ModernNavigationBehavior.PageNavigation
            };

            Assert.Equal(ModernFrameContentScrollHostMode.Enabled, GetFrame(navigationView).ContentScrollHostMode);
        });
    }

    [Fact]
    public void LocalTabsSetsFrameScrollHostModeToAuto()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                NavigationBehavior = ModernNavigationBehavior.LocalTabs
            };

            Assert.Equal(ModernFrameContentScrollHostMode.Auto, GetFrame(navigationView).ContentScrollHostMode);
        });
    }

    [Fact]
    public void TopModeUsesListBoxSelectorNotButtonCommandTabs()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                NavigationBehavior = ModernNavigationBehavior.LocalTabs,
                MenuItemsSource = new[]
                {
                    new NavigationViewItem("Installed", SymbolRegular.AppsList24, typeof(TestUserControl)),
                    new NavigationViewItem("PluginMarket", SymbolRegular.AppsAddIn24, typeof(SecondTestUserControl))
                }
            };

            var window = CreateHiddenWindow(navigationView);
            try
            {
                window.Show();
                window.UpdateLayout();

                var selector = FindVisualDescendants<ListBox>(navigationView)
                    .FirstOrDefault(listBox => Equals(listBox.Name, "PART_TopItemsSelector"));
                Assert.NotNull(selector);

                var topButtons = FindVisualDescendants<System.Windows.Controls.Button>(selector)
                    .Where(button => ReferenceEquals(button.Command, navigationView.NavigateEntryCommand))
                    .ToArray();
                Assert.Empty(topButtons);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TopModeSelectingSecondEntryNavigatesSharedFrame()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                NavigationBehavior = ModernNavigationBehavior.LocalTabs,
                TransitionDuration = 0,
                MenuItemsSource = new[]
                {
                    new NavigationViewItem("Installed", SymbolRegular.AppsList24, typeof(TestUserControl)),
                    new NavigationViewItem("PluginMarket", SymbolRegular.AppsAddIn24, typeof(SecondTestUserControl))
                }
            };

            var window = CreateHiddenWindow(navigationView);
            try
            {
                window.Show();
                window.UpdateLayout();

                var selector = FindVisualDescendants<ListBox>(navigationView)
                    .First(listBox => Equals(listBox.Name, "PART_TopItemsSelector"));

                selector.SelectedIndex = 1;
                FlushDispatcher(window.Dispatcher);

                Assert.IsType<SecondTestUserControl>(navigationView.CurrentContent);
                Assert.Same(navigationView.MenuEntries[1], navigationView.SelectedEntry);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TopModeSelectingCurrentEntryNoOps()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                NavigationBehavior = ModernNavigationBehavior.LocalTabs,
                TransitionDuration = 0,
                MenuItemsSource = new[]
                {
                    new NavigationViewItem("Installed", SymbolRegular.AppsList24, typeof(TestUserControl))
                }
            };
            var navigatedCount = 0;
            navigationView.Navigated += (_, _) => navigatedCount++;

            Assert.True(navigationView.SelectFirstItemIfNoneSelected());
            Assert.False(navigationView.SelectFirstItemIfNoneSelected());
            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[0]);

            Assert.Equal(1, navigatedCount);
            Assert.IsType<TestUserControl>(navigationView.CurrentContent);
        });
    }

    [Fact]
    public void LocalTabsClearsJournalAfterNavigation()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                NavigationBehavior = ModernNavigationBehavior.LocalTabs,
                TransitionDuration = 0,
                MenuItemsSource = new[]
                {
                    new NavigationViewItem("Installed", SymbolRegular.AppsList24, typeof(TestUserControl)),
                    new NavigationViewItem("PluginMarket", SymbolRegular.AppsAddIn24, typeof(SecondTestUserControl))
                }
            };

            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[0]);
            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[1]);

            Assert.False(navigationView.CanGoBack);
            Assert.IsType<SecondTestUserControl>(navigationView.CurrentContent);
        });
    }

    [Fact]
    public void LocalTabsChildViewInheritsNavigationViewDataContext()
    {
        RunSta(() =>
        {
            var dataContext = new object();
            var navigationView = new ModernNavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                NavigationBehavior = ModernNavigationBehavior.LocalTabs,
                TransitionDuration = 0,
                DataContext = dataContext,
                MenuItemsSource = new[]
                {
                    new NavigationViewItem("Installed", SymbolRegular.AppsList24, typeof(TestUserControl))
                }
            };

            Assert.True(navigationView.SelectFirstItemIfNoneSelected());

            Assert.Same(dataContext, navigationView.CurrentContent?.DataContext);
        });
    }

    [Fact]
    public void PluginMarketDisplayTextIsNonEmptyAndIconIsPreserved()
    {
        RunSta(() =>
        {
            var item = new NavigationViewItem("PluginMarket", SymbolRegular.AppsAddIn24, typeof(SecondTestUserControl));
            var navigationView = new ModernNavigationView
            {
                MenuItemsSource = new[] { item }
            };

            var entry = Assert.Single(navigationView.MenuEntries);
            Assert.False(string.IsNullOrWhiteSpace(entry.DisplayText));
            var icon = Assert.IsType<SymbolIcon>(ModernNavigationIconConverter.CreateIcon(entry.Icon));
            Assert.Equal(SymbolRegular.AppsAddIn24, icon.Symbol);
            Assert.NotEqual(SymbolRegular.Document24, icon.Symbol);
        });
    }

    [Fact]
    public void MenuItemsCollectionStillCreatesEntries()
    {
        RunSta(() =>
        {
            var navigationView = new ModernNavigationView();
            var item = new NavigationViewItem("Installed", SymbolRegular.AppsList24, typeof(TestUserControl));

            navigationView.MenuItems.Add(item);

            var entry = Assert.Single(navigationView.MenuEntries);
            Assert.Same(item, entry.SourceItem);
            Assert.Equal(typeof(TestUserControl), entry.TargetPageType);
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
                FlushDispatcher(window.Dispatcher);
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
    public void PluginPageUsesTopLocalTabsAndCreatesTwoMenuItems()
    {
        RunSta(() =>
        {
            var page = new PluginPage();
            var navigationView = Assert.IsType<ModernNavigationView>(page.FindName("PluginTabs"));

            Assert.Equal(NavigationViewPaneDisplayMode.Top, navigationView.PaneDisplayMode);
            Assert.Equal(ModernNavigationBehavior.LocalTabs, navigationView.NavigationBehavior);
            Assert.Equal(2, navigationView.MenuItems.Count);
            Assert.Equal(typeof(PluginInstalledView), navigationView.MenuEntries[0].TargetPageType);
            Assert.Equal(typeof(PluginMarketView), navigationView.MenuEntries[1].TargetPageType);
            Assert.False(string.IsNullOrWhiteSpace(navigationView.MenuEntries[0].DisplayText));
            Assert.False(string.IsNullOrWhiteSpace(navigationView.MenuEntries[1].DisplayText));
        });
    }

    [Fact]
    public void PluginPageLoadedInitializesInstalledView()
    {
        RunSta(() =>
        {
            var dataContext = new object();
            var page = new PluginPage
            {
                DataContext = dataContext
            };
            var navigationView = Assert.IsType<ModernNavigationView>(page.FindName("PluginTabs"));

            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.IsType<PluginInstalledView>(navigationView.CurrentContent);
            Assert.Same(dataContext, navigationView.CurrentContent?.DataContext);
        });
    }

    [Fact]
    public void PluginInstalledLocalTabUsesDirectFramePresenter()
    {
        RunSta(() =>
        {
            var page = new PluginPage();
            var navigationView = Assert.IsType<ModernNavigationView>(page.FindName("PluginTabs"));

            Assert.True(navigationView.SelectFirstItemIfNoneSelected());

            var content = Assert.IsType<PluginInstalledView>(navigationView.CurrentContent);
            Assert.False(IsUsingFrameScrollHost(GetFrame(navigationView), content));
        });
    }

    [Fact]
    public void PluginPageCanSwitchLocalTabsWithoutSeparateFrame()
    {
        RunSta(() =>
        {
            var dataContext = new object();
            var page = new PluginPage
            {
                DataContext = dataContext
            };
            var navigationView = Assert.IsType<ModernNavigationView>(page.FindName("PluginTabs"));

            Assert.True(navigationView.SelectFirstItemIfNoneSelected());
            Assert.IsType<PluginInstalledView>(navigationView.CurrentContent);
            Assert.Same(dataContext, navigationView.CurrentContent?.DataContext);

            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[1]);
            Assert.IsType<PluginMarketView>(navigationView.CurrentContent);
            Assert.Same(dataContext, navigationView.CurrentContent?.DataContext);

            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[0]);
            Assert.IsType<PluginInstalledView>(navigationView.CurrentContent);
            Assert.DoesNotContain(
                FindVisualDescendants<ModernFrame>(navigationView),
                frame => frame.Name == "PART_TopFrame");
        });
    }

    [Fact]
    public void PluginChildViewsAreUserControlsWithoutBackendPageInfo()
    {
        Assert.True(typeof(UserControl).IsAssignableFrom(typeof(PluginInstalledView)));
        Assert.True(typeof(UserControl).IsAssignableFrom(typeof(PluginMarketView)));
        Assert.False(typeof(Page).IsAssignableFrom(typeof(PluginInstalledView)));
        Assert.False(typeof(Page).IsAssignableFrom(typeof(PluginMarketView)));
        Assert.Empty(typeof(PluginInstalledView).GetCustomAttributes(typeof(BackendPageInfo), inherit: false));
        Assert.Empty(typeof(PluginMarketView).GetCustomAttributes(typeof(BackendPageInfo), inherit: false));
    }

    [Fact]
    public void PluginMarketViewContainsOverlayPanelsAndMarkdownViewer()
    {
        RunSta(() =>
        {
            var view = new PluginMarketView();

            Assert.NotNull(view.FindName("DownloadQueuePanel"));
            Assert.NotNull(view.FindName("PluginDetailsPanel"));
            Assert.NotNull(view.FindName("PluginMarketSettingsPanel"));
            Assert.NotNull(view.FindName("PluginReadmeMarkdownViewer"));
        });
    }

    [Fact]
    public void PluginMarketViewLocalWpfUiStylesKeepBasedOnDefaultStyles()
    {
        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "Plugin",
            "PluginMarketView.xaml"));

        Assert.Contains(
            "BasedOn=\"{StaticResource {x:Type ui:HyperlinkButton}}\" TargetType=\"ui:HyperlinkButton\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "BasedOn=\"{StaticResource {x:Type ui:Button}}\" TargetType=\"ui:Button\"",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FrontManagePageUsesTopLocalTabsAndCreatesTwoMenuItems()
    {
        RunSta(() =>
        {
            var page = new FrontManagePage();
            var navigationView = Assert.IsType<ModernNavigationView>(page.FindName("FrontManageTabs"));

            Assert.Equal(NavigationViewPaneDisplayMode.Top, navigationView.PaneDisplayMode);
            Assert.Equal(ModernNavigationBehavior.LocalTabs, navigationView.NavigationBehavior);
            Assert.Equal(2, navigationView.MenuItems.Count);
            Assert.Equal(typeof(FrontedWindowsView), navigationView.MenuEntries[0].TargetPageType);
            Assert.Equal(typeof(FrontedLayoutPackagesView), navigationView.MenuEntries[1].TargetPageType);
            Assert.False(string.IsNullOrWhiteSpace(navigationView.MenuEntries[0].DisplayText));
            Assert.False(string.IsNullOrWhiteSpace(navigationView.MenuEntries[1].DisplayText));
        });
    }

    [Fact]
    public void FrontManagePageLoadedInitializesWindowsView()
    {
        RunSta(() =>
        {
            var dataContext = new object();
            var page = new FrontManagePage
            {
                DataContext = dataContext
            };
            var navigationView = Assert.IsType<ModernNavigationView>(page.FindName("FrontManageTabs"));

            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.IsType<FrontedWindowsView>(navigationView.CurrentContent);
            Assert.Same(dataContext, navigationView.CurrentContent?.DataContext);
        });
    }

    [Fact]
    public void FrontManageLayoutPackagesLocalTabUsesDirectFramePresenter()
    {
        RunSta(() =>
        {
            var page = new FrontManagePage();
            var navigationView = Assert.IsType<ModernNavigationView>(page.FindName("FrontManageTabs"));

            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[1]);

            var content = Assert.IsType<FrontedLayoutPackagesView>(navigationView.CurrentContent);
            Assert.False(IsUsingFrameScrollHost(GetFrame(navigationView), content));
        });
    }

    [Fact]
    public void FrontManageWindowsLocalTabCanUseFrameScrollHost()
    {
        RunSta(() =>
        {
            var page = new FrontManagePage();
            var navigationView = Assert.IsType<ModernNavigationView>(page.FindName("FrontManageTabs"));

            Assert.True(navigationView.SelectFirstItemIfNoneSelected());

            var content = Assert.IsType<FrontedWindowsView>(navigationView.CurrentContent);
            Assert.True(IsUsingFrameScrollHost(GetFrame(navigationView), content));
        });
    }

    [Fact]
    public void FrontManagePageCanSwitchLocalTabsWithoutSeparateFrame()
    {
        RunSta(() =>
        {
            var dataContext = new object();
            var page = new FrontManagePage
            {
                DataContext = dataContext
            };
            var navigationView = Assert.IsType<ModernNavigationView>(page.FindName("FrontManageTabs"));

            Assert.True(navigationView.SelectFirstItemIfNoneSelected());
            Assert.IsType<FrontedWindowsView>(navigationView.CurrentContent);
            Assert.Same(dataContext, navigationView.CurrentContent?.DataContext);

            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[1]);
            Assert.IsType<FrontedLayoutPackagesView>(navigationView.CurrentContent);
            Assert.Same(dataContext, navigationView.CurrentContent?.DataContext);

            navigationView.NavigateEntryCommand.Execute(navigationView.MenuEntries[0]);
            Assert.IsType<FrontedWindowsView>(navigationView.CurrentContent);
            Assert.DoesNotContain(
                FindVisualDescendants<ModernFrame>(navigationView),
                frame => frame.Name == "PART_TopFrame");
        });
    }

    [Fact]
    public void FrontManageChildViewsAreUserControlsWithoutBackendPageInfo()
    {
        Assert.True(typeof(UserControl).IsAssignableFrom(typeof(FrontedWindowsView)));
        Assert.True(typeof(UserControl).IsAssignableFrom(typeof(FrontedLayoutPackagesView)));
        Assert.False(typeof(Page).IsAssignableFrom(typeof(FrontedWindowsView)));
        Assert.False(typeof(Page).IsAssignableFrom(typeof(FrontedLayoutPackagesView)));
        Assert.Empty(typeof(FrontedWindowsView).GetCustomAttributes(typeof(BackendPageInfo), inherit: false));
        Assert.Empty(typeof(FrontedLayoutPackagesView).GetCustomAttributes(typeof(BackendPageInfo), inherit: false));
    }

    [Fact]
    public void FrontedLayoutPackagesViewContainsPackageListAndKeepsBasedOnStyles()
    {
        RunSta(() =>
        {
            var view = new FrontedLayoutPackagesView();

            Assert.NotNull(view.FindName("PackageListBox"));
        });

        var xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "FrontManage",
            "FrontedLayoutPackagesView.xaml"));

        Assert.Contains(
            "BasedOn=\"{StaticResource {x:Type ListBoxItem}}\" TargetType=\"ListBoxItem\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "BasedOn=\"{StaticResource {x:Type ui:Button}}\" TargetType=\"ui:Button\"",
            xaml,
            StringComparison.Ordinal);
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
                FlushDispatcher(window.Dispatcher);
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

    private static ModernFrame GetFrame(ModernNavigationView navigationView)
    {
        return Assert.IsType<ModernFrame>(navigationView.FindName("PART_Frame"));
    }

    private static bool IsUsingFrameScrollHost(ModernFrame frame, FrameworkElement content)
    {
        frame.ApplyTemplate();

        if (frame.ContentScrollHost.Visibility == Visibility.Visible)
        {
            return true;
        }

        var directPresenter = FindVisualDescendants<ContentPresenter>(frame)
            .FirstOrDefault(presenter =>
                ReferenceEquals(presenter.Content, content)
                && !IsVisualDescendantOf(presenter, frame.ContentScrollHost));

        Assert.NotNull(directPresenter);
        Assert.Equal(Visibility.Visible, directPresenter.Visibility);
        return false;
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

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
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

    public sealed class TestUserControl : UserControl
    {
    }

    public sealed class SecondTestUserControl : UserControl
    {
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
