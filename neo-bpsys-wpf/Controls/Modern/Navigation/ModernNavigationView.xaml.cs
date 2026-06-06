#nullable enable

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using neo_bpsys_wpf.Controls.Modern.Frame;
using WPFLocalizeExtension.Engine;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Animations;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Controls.Modern.Navigation;

public partial class ModernNavigationView : UserControl, INavigationView
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderVisibilityProperty =
        DependencyProperty.Register(nameof(HeaderVisibility), typeof(Visibility), typeof(ModernNavigationView), new PropertyMetadata(Visibility.Visible));

    public static readonly DependencyProperty AlwaysShowHeaderProperty =
        DependencyProperty.Register(nameof(AlwaysShowHeader), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(false));

    public static readonly DependencyProperty MenuItemsSourceProperty =
        DependencyProperty.Register(nameof(MenuItemsSource), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null, OnMenuItemsSourceChanged));

    public static readonly DependencyProperty FooterMenuItemsSourceProperty =
        DependencyProperty.Register(nameof(FooterMenuItemsSource), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null, OnFooterMenuItemsSourceChanged));

    public static readonly DependencyProperty IsTopSeparatorVisibleProperty =
        DependencyProperty.Register(nameof(IsTopSeparatorVisible), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true));

    public static readonly DependencyProperty IsFooterSeparatorVisibleProperty =
        DependencyProperty.Register(nameof(IsFooterSeparatorVisible), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true));

    public static readonly DependencyProperty ContentOverlayProperty =
        DependencyProperty.Register(nameof(ContentOverlay), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null));

    private static readonly DependencyPropertyKey IsBackEnabledPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsBackEnabled), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(false));

    public static readonly DependencyProperty IsBackEnabledProperty = IsBackEnabledPropertyKey.DependencyProperty;

    public static readonly DependencyProperty IsBackButtonVisibleProperty =
        DependencyProperty.Register(nameof(IsBackButtonVisible), typeof(NavigationViewBackButtonVisible), typeof(ModernNavigationView), new PropertyMetadata(NavigationViewBackButtonVisible.Auto));

    public static readonly DependencyProperty IsPaneToggleVisibleProperty =
        DependencyProperty.Register(nameof(IsPaneToggleVisible), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true));

    public static readonly DependencyProperty IsPaneOpenProperty =
        DependencyProperty.Register(nameof(IsPaneOpen), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true, OnIsPaneOpenChanged));

    public static readonly DependencyProperty IsPaneVisibleProperty =
        DependencyProperty.Register(nameof(IsPaneVisible), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true));

    public static readonly DependencyProperty OpenPaneLengthProperty =
        DependencyProperty.Register(nameof(OpenPaneLength), typeof(double), typeof(ModernNavigationView), new PropertyMetadata(200D, OnPaneLengthChanged));

    public static readonly DependencyProperty CompactPaneLengthProperty =
        DependencyProperty.Register(nameof(CompactPaneLength), typeof(double), typeof(ModernNavigationView), new PropertyMetadata(48D, OnPaneLengthChanged));

    public static readonly DependencyProperty ActualPaneLengthProperty =
        DependencyProperty.Register(nameof(ActualPaneLength), typeof(double), typeof(ModernNavigationView), new PropertyMetadata(200D));

    public static readonly DependencyProperty PaneHeaderProperty =
        DependencyProperty.Register(nameof(PaneHeader), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null));

    public static readonly DependencyProperty PaneTitleProperty =
        DependencyProperty.Register(nameof(PaneTitle), typeof(string), typeof(ModernNavigationView), new PropertyMetadata(null));

    public static readonly DependencyProperty PaneFooterProperty =
        DependencyProperty.Register(nameof(PaneFooter), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null));

    public static readonly DependencyProperty PaneDisplayModeProperty =
        DependencyProperty.Register(nameof(PaneDisplayMode), typeof(NavigationViewPaneDisplayMode), typeof(ModernNavigationView), new PropertyMetadata(NavigationViewPaneDisplayMode.Left));

    public static readonly DependencyProperty NavigationBehaviorProperty =
        DependencyProperty.Register(nameof(NavigationBehavior), typeof(ModernNavigationBehavior), typeof(ModernNavigationView), new PropertyMetadata(ModernNavigationBehavior.PageNavigation));

    public static readonly DependencyProperty SelectedEntryProperty =
        DependencyProperty.Register(nameof(SelectedEntry), typeof(ModernNavigationEntry), typeof(ModernNavigationView), new PropertyMetadata(null, OnSelectedEntryChanged));

    public static readonly DependencyProperty TitleBarProperty =
        DependencyProperty.Register(nameof(TitleBar), typeof(TitleBar), typeof(ModernNavigationView), new PropertyMetadata(null));

    public static readonly DependencyProperty AutoSuggestBoxProperty =
        DependencyProperty.Register(nameof(AutoSuggestBox), typeof(AutoSuggestBox), typeof(ModernNavigationView), new PropertyMetadata(null));

    public static readonly DependencyProperty BreadcrumbBarProperty =
        DependencyProperty.Register(nameof(BreadcrumbBar), typeof(BreadcrumbBar), typeof(ModernNavigationView), new PropertyMetadata(null));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(ControlTemplate), typeof(ModernNavigationView), new PropertyMetadata(null));

    public static readonly DependencyProperty TransitionDurationProperty =
        DependencyProperty.Register(nameof(TransitionDuration), typeof(int), typeof(ModernNavigationView), new PropertyMetadata(240, OnTransitionDurationChanged));

    public static readonly DependencyProperty TransitionProperty =
        DependencyProperty.Register(nameof(Transition), typeof(Transition), typeof(ModernNavigationView), new PropertyMetadata(Transition.FadeInWithSlide));

    public static readonly DependencyProperty FrameMarginProperty =
        DependencyProperty.Register(nameof(FrameMargin), typeof(Thickness), typeof(ModernNavigationView), new PropertyMetadata(default(Thickness)));

    private readonly ObservableCollection<object> _menuItems = [];
    private readonly ObservableCollection<object> _footerMenuItems = [];
    private readonly Stack<ModernNavigationEntry?> _selectedEntryBackStack = new();
    private INavigationViewPageProvider? _pageProvider;
    private IServiceProvider? _serviceProvider;
    private INotifyCollectionChanged? _menuItemsSourceCollection;
    private INotifyCollectionChanged? _footerMenuItemsSourceCollection;
    private ModernNavigationEntry? _selectedEntry;
    private bool _isUpdatingSelectedEntryProperty;
    private bool _isGoingBack;

    public ModernNavigationView()
    {
        NavigateEntryCommand = new ModernNavigationCommand(parameter =>
        {
            if (parameter is ModernNavigationEntry entry)
            {
                NavigateEntry(entry, null, suppressIfCurrent: true);
            }
        });

        TogglePaneCommand = new ModernNavigationCommand(_ => IsPaneOpen = !IsPaneOpen);

        InitializeComponent();

        PART_Frame.Navigated += (_, _) =>
        {
            SetValue(IsBackEnabledPropertyKey, PART_Frame.CanGoBack);
        };

        SetCurrentValue(ActualPaneLengthProperty, GetTargetPaneLength());
        PART_Frame.TransitionDuration = TimeSpan.FromMilliseconds(Math.Max(0, TransitionDuration));

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _menuItems.CollectionChanged += (_, _) => RebuildMenuEntries();
        _footerMenuItems.CollectionChanged += (_, _) => RebuildFooterMenuEntries();
    }

    public ObservableCollection<ModernNavigationEntry> MenuEntries { get; } = [];

    public ObservableCollection<ModernNavigationEntry> FooterMenuEntries { get; } = [];

    public ICommand NavigateEntryCommand { get; }

    public ICommand TogglePaneCommand { get; }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public Visibility HeaderVisibility
    {
        get => (Visibility)GetValue(HeaderVisibilityProperty);
        set => SetValue(HeaderVisibilityProperty, value);
    }

    public bool AlwaysShowHeader
    {
        get => (bool)GetValue(AlwaysShowHeaderProperty);
        set => SetValue(AlwaysShowHeaderProperty, value);
    }

    public IList MenuItems => _menuItems;

    public object? MenuItemsSource
    {
        get => GetValue(MenuItemsSourceProperty);
        set => SetValue(MenuItemsSourceProperty, value);
    }

    public IList FooterMenuItems => _footerMenuItems;

    public object? FooterMenuItemsSource
    {
        get => GetValue(FooterMenuItemsSourceProperty);
        set => SetValue(FooterMenuItemsSourceProperty, value);
    }

    public bool IsTopSeparatorVisible
    {
        get => (bool)GetValue(IsTopSeparatorVisibleProperty);
        set => SetValue(IsTopSeparatorVisibleProperty, value);
    }

    public bool IsFooterSeparatorVisible
    {
        get => (bool)GetValue(IsFooterSeparatorVisibleProperty);
        set => SetValue(IsFooterSeparatorVisibleProperty, value);
    }

    public INavigationViewItem? SelectedItem => _selectedEntry?.SourceNavigationViewItem;

    public object? ContentOverlay
    {
        get => GetValue(ContentOverlayProperty);
        set => SetValue(ContentOverlayProperty, value);
    }

    public bool IsBackEnabled => (bool)GetValue(IsBackEnabledProperty);

    public NavigationViewBackButtonVisible IsBackButtonVisible
    {
        get => (NavigationViewBackButtonVisible)GetValue(IsBackButtonVisibleProperty);
        set => SetValue(IsBackButtonVisibleProperty, value);
    }

    public bool IsPaneToggleVisible
    {
        get => (bool)GetValue(IsPaneToggleVisibleProperty);
        set => SetValue(IsPaneToggleVisibleProperty, value);
    }

    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    public bool IsPaneVisible
    {
        get => (bool)GetValue(IsPaneVisibleProperty);
        set => SetValue(IsPaneVisibleProperty, value);
    }

    public double OpenPaneLength
    {
        get => (double)GetValue(OpenPaneLengthProperty);
        set => SetValue(OpenPaneLengthProperty, value);
    }

    public double CompactPaneLength
    {
        get => (double)GetValue(CompactPaneLengthProperty);
        set => SetValue(CompactPaneLengthProperty, value);
    }

    public double ActualPaneLength
    {
        get => (double)GetValue(ActualPaneLengthProperty);
        private set => SetValue(ActualPaneLengthProperty, value);
    }

    public object? PaneHeader
    {
        get => GetValue(PaneHeaderProperty);
        set => SetValue(PaneHeaderProperty, value);
    }

    public string? PaneTitle
    {
        get => (string?)GetValue(PaneTitleProperty);
        set => SetValue(PaneTitleProperty, value);
    }

    public object? PaneFooter
    {
        get => GetValue(PaneFooterProperty);
        set => SetValue(PaneFooterProperty, value);
    }

    public NavigationViewPaneDisplayMode PaneDisplayMode
    {
        get => (NavigationViewPaneDisplayMode)GetValue(PaneDisplayModeProperty);
        set => SetValue(PaneDisplayModeProperty, value);
    }

    public ModernNavigationBehavior NavigationBehavior
    {
        get => (ModernNavigationBehavior)GetValue(NavigationBehaviorProperty);
        set => SetValue(NavigationBehaviorProperty, value);
    }

    public ModernNavigationEntry? SelectedEntry
    {
        get => (ModernNavigationEntry?)GetValue(SelectedEntryProperty);
        set => SetValue(SelectedEntryProperty, value);
    }

    public TitleBar? TitleBar
    {
        get => (TitleBar?)GetValue(TitleBarProperty);
        set => SetValue(TitleBarProperty, value);
    }

    public AutoSuggestBox? AutoSuggestBox
    {
        get => (AutoSuggestBox?)GetValue(AutoSuggestBoxProperty);
        set => SetValue(AutoSuggestBoxProperty, value);
    }

    public BreadcrumbBar? BreadcrumbBar
    {
        get => (BreadcrumbBar?)GetValue(BreadcrumbBarProperty);
        set => SetValue(BreadcrumbBarProperty, value);
    }

    public ControlTemplate? ItemTemplate
    {
        get => (ControlTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public int TransitionDuration
    {
        get => (int)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    public Transition Transition
    {
        get => (Transition)GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    public Thickness FrameMargin
    {
        get => (Thickness)GetValue(FrameMarginProperty);
        set => SetValue(FrameMarginProperty, value);
    }

    public bool CanGoBack => PART_Frame.CanGoBack;

    public FrameworkElement? CurrentContent => PART_Frame.CurrentContent;

    public neo_bpsys_wpf.Controls.Modern.Scrolling.ModernScrollViewer ContentScrollHost => PART_Frame.ContentScrollHost;

    public event TypedEventHandler<NavigationView, RoutedEventArgs>? PaneOpened;

    public event TypedEventHandler<NavigationView, RoutedEventArgs>? PaneClosed;

    public event TypedEventHandler<NavigationView, RoutedEventArgs>? SelectionChanged;

    public event TypedEventHandler<NavigationView, RoutedEventArgs>? ItemInvoked;

    public event TypedEventHandler<NavigationView, RoutedEventArgs>? BackRequested;

    public event TypedEventHandler<NavigationView, NavigatingCancelEventArgs>? Navigating;

    public event TypedEventHandler<NavigationView, NavigatedEventArgs>? Navigated;

    public bool Navigate(Type pageType, object? dataContext = null)
    {
        ArgumentNullException.ThrowIfNull(pageType);

        var entry = FindEntryByPageType(pageType);
        return NavigateEntryOrPageType(entry, pageType, dataContext);
    }

    public bool Navigate(string pageIdOrTargetTag, object? dataContext = null)
    {
        if (string.IsNullOrWhiteSpace(pageIdOrTargetTag))
        {
            return false;
        }

        var entry = AllEntries().FirstOrDefault(x => IsTagMatch(x, pageIdOrTargetTag));
        if (entry?.TargetPageType is null)
        {
            return false;
        }

        return NavigateEntry(entry, dataContext, suppressIfCurrent: false);
    }

    public bool NavigateWithHierarchy(Type pageType, object? dataContext = null) =>
        Navigate(pageType, dataContext);

    public bool ReplaceContent(Type pageTypeToEmbed)
    {
        ArgumentNullException.ThrowIfNull(pageTypeToEmbed);
        return PART_Frame.Navigate(CreatePageElement(pageTypeToEmbed, null));
    }

    public bool ReplaceContent(UIElement pageInstanceToEmbed, object? dataContext = null)
    {
        ArgumentNullException.ThrowIfNull(pageInstanceToEmbed);

        if (pageInstanceToEmbed is not FrameworkElement frameworkElement)
        {
            return false;
        }

        if (dataContext is not null)
        {
            frameworkElement.DataContext = dataContext;
        }

        return PART_Frame.Navigate(frameworkElement);
    }

    public bool GoForward() => false;

    public bool GoBack()
    {
        _isGoingBack = true;
        try
        {
            if (!PART_Frame.GoBack())
            {
                return false;
            }

            var previous = _selectedEntryBackStack.Count > 0 ? _selectedEntryBackStack.Pop() : null;
            SetSelectedEntry(previous, raiseSelectionChanged: true, addCurrentToBackStack: false);
            BackRequested?.Invoke(null!, new RoutedEventArgs());
            SetValue(IsBackEnabledPropertyKey, PART_Frame.CanGoBack);
            return true;
        }
        finally
        {
            _isGoingBack = false;
        }
    }

    public void ClearJournal()
    {
        PART_Frame.ClearJournal();
        _selectedEntryBackStack.Clear();
        SetValue(IsBackEnabledPropertyKey, false);
    }

    public void SetPageProviderService(INavigationViewPageProvider navigationViewPageProvider)
    {
        _pageProvider = navigationViewPageProvider;
    }

    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        PART_Frame.ServiceProvider = serviceProvider;
    }

    public void RefreshLocalizedMenuText()
    {
        foreach (var entry in AllEntries())
        {
            entry.RefreshDisplayText();
        }
    }

    public bool SelectFirstItemIfNoneSelected()
    {
        if (_selectedEntry is not null || CurrentContent is not null)
        {
            return false;
        }

        var entry = MenuEntries.FirstOrDefault(x => x is { IsEnabled: true, TargetPageType: not null });
        return entry is not null && NavigateEntry(entry, null, suppressIfCurrent: true);
    }

    private static void OnMenuItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ModernNavigationView)d).SetItemsSource(e.OldValue, e.NewValue, isFooter: false);
    }

    private static void OnFooterMenuItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ModernNavigationView)d).SetItemsSource(e.OldValue, e.NewValue, isFooter: true);
    }

    private static void OnIsPaneOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var navigationView = (ModernNavigationView)d;
        navigationView.AnimatePaneLength();

        if ((bool)e.NewValue)
        {
            navigationView.PaneOpened?.Invoke(null!, new RoutedEventArgs());
        }
        else
        {
            navigationView.PaneClosed?.Invoke(null!, new RoutedEventArgs());
        }
    }

    private static void OnPaneLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var navigationView = (ModernNavigationView)d;
        navigationView.SetCurrentValue(ActualPaneLengthProperty, navigationView.GetTargetPaneLength());
    }

    private static void OnTransitionDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var navigationView = (ModernNavigationView)d;
        if (navigationView.PART_Frame is not null)
        {
            navigationView.PART_Frame.TransitionDuration = TimeSpan.FromMilliseconds(Math.Max(0, (int)e.NewValue));
        }
    }

    private static void OnSelectedEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var navigationView = (ModernNavigationView)d;
        if (navigationView._isUpdatingSelectedEntryProperty)
        {
            return;
        }

        var selectedEntry = (ModernNavigationEntry?)e.NewValue;
        if (selectedEntry is null)
        {
            navigationView.SetSelectedEntry(null, raiseSelectionChanged: true, addCurrentToBackStack: false);
            return;
        }

        if (!navigationView.NavigateEntry(selectedEntry, null, suppressIfCurrent: true))
        {
            navigationView.SyncSelectedEntryProperty(navigationView._selectedEntry);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LocalizeDictionary.Instance.PropertyChanged += OnLocalizeDictionaryPropertyChanged;
        RefreshLocalizedMenuText();

        if (PaneDisplayMode == NavigationViewPaneDisplayMode.Top
            && NavigationBehavior == ModernNavigationBehavior.LocalTabs)
        {
            Dispatcher.BeginInvoke(SelectFirstItemIfNoneSelected, DispatcherPriority.Loaded);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        LocalizeDictionary.Instance.PropertyChanged -= OnLocalizeDictionaryPropertyChanged;
    }

    private void OnLocalizeDictionaryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName == nameof(LocalizeDictionary.Culture)
            || e.PropertyName == nameof(LocalizeDictionary.CurrentCulture))
        {
            RefreshLocalizedMenuText();
        }
    }

    private void SetItemsSource(object? oldSource, object? newSource, bool isFooter)
    {
        if (oldSource is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= isFooter ? OnFooterItemsSourceCollectionChanged : OnMenuItemsSourceCollectionChanged;
        }

        if (isFooter)
        {
            _footerMenuItemsSourceCollection = newSource as INotifyCollectionChanged;
            _footerMenuItems.Clear();
        }
        else
        {
            _menuItemsSourceCollection = newSource as INotifyCollectionChanged;
            _menuItems.Clear();
        }

        foreach (var item in EnumerateItems(newSource))
        {
            if (isFooter)
            {
                _footerMenuItems.Add(item);
            }
            else
            {
                _menuItems.Add(item);
            }
        }

        if (newSource is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += isFooter ? OnFooterItemsSourceCollectionChanged : OnMenuItemsSourceCollectionChanged;
        }
    }

    private void OnMenuItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncObservableSourceToItems(_menuItemsSourceCollection, _menuItems);
    }

    private void OnFooterItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncObservableSourceToItems(_footerMenuItemsSourceCollection, _footerMenuItems);
    }

    private static void SyncObservableSourceToItems(INotifyCollectionChanged? source, ObservableCollection<object> target)
    {
        target.Clear();

        if (source is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                target.Add(item);
            }
        }
    }

    private void RebuildMenuEntries()
    {
        RebuildEntries(_menuItems, MenuEntries, isFooter: false);
    }

    private void RebuildFooterMenuEntries()
    {
        RebuildEntries(_footerMenuItems, FooterMenuEntries, isFooter: true);
    }

    private void RebuildEntries(IEnumerable source, ObservableCollection<ModernNavigationEntry> target, bool isFooter)
    {
        target.Clear();

        foreach (var item in source)
        {
            target.Add(ModernNavigationEntry.FromSource(item, isFooter));
        }

        if (_selectedEntry is not null)
        {
            var selected = AllEntries().FirstOrDefault(entry => IsSameEntry(entry, _selectedEntry));
            SetSelectedEntry(selected, raiseSelectionChanged: false, addCurrentToBackStack: false);
        }
    }

    private bool NavigateEntryOrPageType(ModernNavigationEntry? entry, Type pageType, object? dataContext)
    {
        if (entry is not null)
        {
            return NavigateEntry(entry, dataContext, suppressIfCurrent: false);
        }

        return NavigatePageType(pageType, dataContext, entry: null);
    }

    private bool NavigateEntry(ModernNavigationEntry entry, object? dataContext, bool suppressIfCurrent)
    {
        if (entry.TargetPageType is null || !entry.IsEnabled)
        {
            return false;
        }

        if (suppressIfCurrent && IsCurrentEntry(entry))
        {
            return true;
        }

        ItemInvoked?.Invoke(null!, new RoutedEventArgs());
        return NavigatePageType(entry.TargetPageType, dataContext, entry);
    }

    private bool IsCurrentEntry(ModernNavigationEntry entry)
    {
        if (_selectedEntry is null || entry.TargetPageType is null)
        {
            return false;
        }

        if (!IsSameEntry(entry, _selectedEntry))
        {
            return false;
        }

        return CurrentContent is not null && CurrentContent.GetType() == entry.TargetPageType;
    }

    private bool NavigatePageType(Type pageType, object? dataContext, ModernNavigationEntry? entry)
    {
        var page = CreateNavigationElement(pageType, dataContext);
        var navigatingArgs = new NavigatingCancelEventArgs(System.Windows.Controls.Button.ClickEvent, this)
        {
            Page = page
        };

        Navigating?.Invoke(null!, navigatingArgs);
        if (navigatingArgs.Cancel)
        {
            return false;
        }

        var transitionInfo = GetNavigationTransitionInfo(entry);
        if (!PART_Frame.Navigate(page, transitionInfo))
        {
            return false;
        }

        SetSelectedEntry(entry, raiseSelectionChanged: true, addCurrentToBackStack: !_isGoingBack);
        if (NavigationBehavior == ModernNavigationBehavior.LocalTabs)
        {
            ClearJournal();
        }

        SetValue(IsBackEnabledPropertyKey, PART_Frame.CanGoBack);
        Navigated?.Invoke(null!, new NavigatedEventArgs(System.Windows.Controls.Button.ClickEvent, this) { Page = page });
        return true;
    }

    private FrameworkElement CreateNavigationElement(Type pageType, object? dataContext)
    {
        return NavigationBehavior == ModernNavigationBehavior.LocalTabs
            ? CreateLocalTabElement(pageType)
            : CreatePageElement(pageType, dataContext);
    }

    private FrameworkElement CreatePageElement(Type pageType, object? dataContext)
    {
        object? page = null;

        if (_serviceProvider is not null)
        {
            page = _serviceProvider.GetService(pageType);
        }

        page ??= _pageProvider?.GetPage(pageType);
        page ??= Activator.CreateInstance(pageType);

        if (page is not FrameworkElement frameworkElement)
        {
            throw new InvalidOperationException($"Navigation page type '{pageType.FullName}' must create a FrameworkElement.");
        }

        if (dataContext is not null)
        {
            frameworkElement.DataContext = dataContext;
        }

        return frameworkElement;
    }

    private FrameworkElement CreateLocalTabElement(Type pageType)
    {
        var view = Activator.CreateInstance(pageType) as FrameworkElement
            ?? throw new InvalidOperationException($"Local tab target type '{pageType.FullName}' must create a FrameworkElement.");

        if (view.DataContext is null)
        {
            view.DataContext = DataContext;
        }

        return view;
    }

    private ModernNavigationTransitionInfo? GetNavigationTransitionInfo(ModernNavigationEntry? entry)
    {
        if (NavigationBehavior != ModernNavigationBehavior.LocalTabs || PaneDisplayMode != NavigationViewPaneDisplayMode.Top)
        {
            return null;
        }

        if (_selectedEntry is null || entry is null)
        {
            return new SuppressNavigationTransitionInfo();
        }

        var oldIndex = MenuEntries.IndexOf(_selectedEntry);
        var newIndex = MenuEntries.IndexOf(entry);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
        {
            return new SuppressNavigationTransitionInfo();
        }

        return new SlideNavigationTransitionInfo
        {
            Effect = newIndex > oldIndex
                ? SlideNavigationTransitionEffect.FromRight
                : SlideNavigationTransitionEffect.FromLeft
        };
    }

    private void SetSelectedEntry(ModernNavigationEntry? entry, bool raiseSelectionChanged, bool addCurrentToBackStack)
    {
        if (ReferenceEquals(_selectedEntry, entry))
        {
            return;
        }

        if (addCurrentToBackStack && _selectedEntry is not null)
        {
            _selectedEntryBackStack.Push(_selectedEntry);
        }

        if (_selectedEntry is not null)
        {
            _selectedEntry.IsSelected = false;
            _selectedEntry.SourceNavigationViewItem?.SetCurrentValue(NavigationViewItem.IsActiveProperty, false);
        }

        _selectedEntry = entry;
        SyncSelectedEntryProperty(_selectedEntry);

        if (_selectedEntry is not null)
        {
            _selectedEntry.IsSelected = true;
            _selectedEntry.SourceNavigationViewItem?.SetCurrentValue(NavigationViewItem.IsActiveProperty, true);
        }

        if (raiseSelectionChanged)
        {
            SelectionChanged?.Invoke(null!, new RoutedEventArgs());
        }
    }

    private void SyncSelectedEntryProperty(ModernNavigationEntry? entry)
    {
        if (ReferenceEquals(SelectedEntry, entry))
        {
            return;
        }

        _isUpdatingSelectedEntryProperty = true;
        try
        {
            SetCurrentValue(SelectedEntryProperty, entry);
        }
        finally
        {
            _isUpdatingSelectedEntryProperty = false;
        }
    }

    private void TopItemsSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PaneDisplayMode != NavigationViewPaneDisplayMode.Top)
        {
            return;
        }

        if (PART_TopItemsSelector.SelectedItem is not ModernNavigationEntry entry
            || ReferenceEquals(entry, _selectedEntry))
        {
            return;
        }

        if (!NavigateEntry(entry, null, suppressIfCurrent: true))
        {
            PART_TopItemsSelector.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, _selectedEntry);
        }
    }

    private ModernNavigationEntry? FindEntryByPageType(Type pageType) =>
        AllEntries().FirstOrDefault(entry => entry.TargetPageType == pageType);

    private IEnumerable<ModernNavigationEntry> AllEntries() => MenuEntries.Concat(FooterMenuEntries);

    private static bool IsTagMatch(ModernNavigationEntry entry, string pageIdOrTargetTag)
    {
        if (string.Equals(entry.TargetPageTag, pageIdOrTargetTag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (entry.SourceNavigationViewItem is not null
            && string.Equals(entry.SourceNavigationViewItem.Id, pageIdOrTargetTag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return entry.SourceItem is FrameworkElement element
            && string.Equals(element.Tag?.ToString(), pageIdOrTargetTag, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameEntry(ModernNavigationEntry left, ModernNavigationEntry right)
    {
        if (ReferenceEquals(left.SourceItem, right.SourceItem))
        {
            return true;
        }

        return left.TargetPageType is not null && left.TargetPageType == right.TargetPageType;
    }

    private static IEnumerable<object> EnumerateItems(object? source)
    {
        if (source is null)
        {
            yield break;
        }

        if (source is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }

            yield break;
        }

        yield return source;
    }

    private void AnimatePaneLength()
    {
        var targetLength = GetTargetPaneLength();
        var duration = TimeSpan.FromMilliseconds(180);

        if (!IsLoaded || !SystemParameters.ClientAreaAnimation)
        {
            SetCurrentValue(ActualPaneLengthProperty, targetLength);
            return;
        }

        BeginAnimation(
            ActualPaneLengthProperty,
            new DoubleAnimation(targetLength, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private double GetTargetPaneLength()
    {
        var compact = Math.Max(40D, CompactPaneLength);
        var open = Math.Max(compact, OpenPaneLength);
        return IsPaneOpen ? open : compact;
    }
}
