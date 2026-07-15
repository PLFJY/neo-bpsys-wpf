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

/// <summary>
/// 现代导航视图控件，提供侧边栏导航面板和页面框架，支持页面导航和本地标签两种行为模式。
/// </summary>
public partial class ModernNavigationView : UserControl, INavigationView
{
    /// <summary>
    /// <see cref="Header"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null));

    /// <summary>
    /// <see cref="HeaderVisibility"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty HeaderVisibilityProperty =
        DependencyProperty.Register(nameof(HeaderVisibility), typeof(Visibility), typeof(ModernNavigationView), new PropertyMetadata(Visibility.Visible));

    /// <summary>
    /// <see cref="AlwaysShowHeader"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty AlwaysShowHeaderProperty =
        DependencyProperty.Register(nameof(AlwaysShowHeader), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(false));

    /// <summary>
    /// <see cref="MenuItemsSource"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty MenuItemsSourceProperty =
        DependencyProperty.Register(nameof(MenuItemsSource), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null, OnMenuItemsSourceChanged));

    /// <summary>
    /// <see cref="FooterMenuItemsSource"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty FooterMenuItemsSourceProperty =
        DependencyProperty.Register(nameof(FooterMenuItemsSource), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null, OnFooterMenuItemsSourceChanged));

    /// <summary>
    /// <see cref="IsTopSeparatorVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsTopSeparatorVisibleProperty =
        DependencyProperty.Register(nameof(IsTopSeparatorVisible), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true));

    /// <summary>
    /// <see cref="IsFooterSeparatorVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsFooterSeparatorVisibleProperty =
        DependencyProperty.Register(nameof(IsFooterSeparatorVisible), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true));

    /// <summary>
    /// <see cref="ContentOverlay"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ContentOverlayProperty =
        DependencyProperty.Register(nameof(ContentOverlay), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null));

    private static readonly DependencyPropertyKey IsBackEnabledPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsBackEnabled), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(false));

    /// <summary>
    /// <see cref="IsBackEnabled"/> 依赖属性的标识符（只读）。
    /// </summary>
    public static readonly DependencyProperty IsBackEnabledProperty = IsBackEnabledPropertyKey.DependencyProperty;

    /// <summary>
    /// <see cref="IsBackButtonVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsBackButtonVisibleProperty =
        DependencyProperty.Register(nameof(IsBackButtonVisible), typeof(NavigationViewBackButtonVisible), typeof(ModernNavigationView), new PropertyMetadata(NavigationViewBackButtonVisible.Auto));

    /// <summary>
    /// <see cref="IsPaneToggleVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsPaneToggleVisibleProperty =
        DependencyProperty.Register(nameof(IsPaneToggleVisible), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true));

    /// <summary>
    /// <see cref="IsPaneOpen"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsPaneOpenProperty =
        DependencyProperty.Register(nameof(IsPaneOpen), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true, OnIsPaneOpenChanged));

    /// <summary>
    /// <see cref="IsPaneVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsPaneVisibleProperty =
        DependencyProperty.Register(nameof(IsPaneVisible), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true));

    /// <summary>
    /// <see cref="OpenPaneLength"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty OpenPaneLengthProperty =
        DependencyProperty.Register(nameof(OpenPaneLength), typeof(double), typeof(ModernNavigationView), new PropertyMetadata(200D, OnPaneLengthChanged));

    /// <summary>
    /// <see cref="CompactPaneLength"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CompactPaneLengthProperty =
        DependencyProperty.Register(nameof(CompactPaneLength), typeof(double), typeof(ModernNavigationView), new PropertyMetadata(48D, OnPaneLengthChanged));

    /// <summary>
    /// <see cref="ActualPaneLength"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ActualPaneLengthProperty =
        DependencyProperty.Register(nameof(ActualPaneLength), typeof(double), typeof(ModernNavigationView), new PropertyMetadata(200D));

    /// <summary>
    /// <see cref="PaneHeader"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty PaneHeaderProperty =
        DependencyProperty.Register(nameof(PaneHeader), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null));

    /// <summary>
    /// <see cref="PaneTitle"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty PaneTitleProperty =
        DependencyProperty.Register(nameof(PaneTitle), typeof(string), typeof(ModernNavigationView), new PropertyMetadata(null));

    /// <summary>
    /// <see cref="PaneFooter"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty PaneFooterProperty =
        DependencyProperty.Register(nameof(PaneFooter), typeof(object), typeof(ModernNavigationView), new PropertyMetadata(null));

    /// <summary>
    /// <see cref="PaneDisplayMode"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty PaneDisplayModeProperty =
        DependencyProperty.Register(nameof(PaneDisplayMode), typeof(NavigationViewPaneDisplayMode), typeof(ModernNavigationView), new PropertyMetadata(NavigationViewPaneDisplayMode.Left));

    /// <summary>
    /// <see cref="NavigationBehavior"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty NavigationBehaviorProperty =
        DependencyProperty.Register(nameof(NavigationBehavior), typeof(ModernNavigationBehavior), typeof(ModernNavigationView), new PropertyMetadata(ModernNavigationBehavior.PageNavigation, OnNavigationBehaviorChanged));

    /// <summary>
    /// <see cref="SelectedEntry"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty SelectedEntryProperty =
        DependencyProperty.Register(nameof(SelectedEntry), typeof(ModernNavigationEntry), typeof(ModernNavigationView), new PropertyMetadata(null, OnSelectedEntryChanged));

    /// <summary>
    /// <see cref="TitleBar"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TitleBarProperty =
        DependencyProperty.Register(nameof(TitleBar), typeof(TitleBar), typeof(ModernNavigationView), new PropertyMetadata(null));

    /// <summary>
    /// <see cref="AutoSuggestBox"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty AutoSuggestBoxProperty =
        DependencyProperty.Register(nameof(AutoSuggestBox), typeof(AutoSuggestBox), typeof(ModernNavigationView), new PropertyMetadata(null));

    /// <summary>
    /// <see cref="BreadcrumbBar"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty BreadcrumbBarProperty =
        DependencyProperty.Register(nameof(BreadcrumbBar), typeof(BreadcrumbBar), typeof(ModernNavigationView), new PropertyMetadata(null));

    /// <summary>
    /// <see cref="ItemTemplate"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(ControlTemplate), typeof(ModernNavigationView), new PropertyMetadata(null));

    /// <summary>
    /// <see cref="TransitionDuration"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TransitionDurationProperty =
        DependencyProperty.Register(nameof(TransitionDuration), typeof(int), typeof(ModernNavigationView), new PropertyMetadata(240, OnTransitionDurationChanged));

    /// <summary>
    /// <see cref="IsAnimationEnabled"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsAnimationEnabledProperty =
        DependencyProperty.Register(nameof(IsAnimationEnabled), typeof(bool), typeof(ModernNavigationView), new PropertyMetadata(true, OnIsAnimationEnabledChanged));

    /// <summary>
    /// <see cref="Transition"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TransitionProperty =
        DependencyProperty.Register(nameof(Transition), typeof(Transition), typeof(ModernNavigationView), new PropertyMetadata(Transition.FadeInWithSlide));

    /// <summary>
    /// <see cref="FrameMargin"/> 依赖属性的标识符。
    /// </summary>
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

    /// <summary>
    /// 初始化 <see cref="ModernNavigationView"/> 的新实例。
    /// </summary>
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
        PART_Frame.IsAnimationEnabled = IsAnimationEnabled;
        UpdateFrameScrollHostMode();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _menuItems.CollectionChanged += (_, _) => RebuildMenuEntries();
        _footerMenuItems.CollectionChanged += (_, _) => RebuildFooterMenuEntries();
    }

    /// <summary>
    /// 获取菜单条目集合。
    /// </summary>
    public ObservableCollection<ModernNavigationEntry> MenuEntries { get; } = [];

    /// <summary>
    /// 获取底部菜单条目集合。
    /// </summary>
    public ObservableCollection<ModernNavigationEntry> FooterMenuEntries { get; } = [];

    /// <summary>
    /// 获取导航条目命令。
    /// </summary>
    public ICommand NavigateEntryCommand { get; }

    /// <summary>
    /// 获取切换面板命令。
    /// </summary>
    public ICommand TogglePaneCommand { get; }

    /// <summary>
    /// 获取或设置页眉内容。
    /// </summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// 获取或设置页眉的可见性。
    /// </summary>
    public Visibility HeaderVisibility
    {
        get => (Visibility)GetValue(HeaderVisibilityProperty);
        set => SetValue(HeaderVisibilityProperty, value);
    }

    /// <summary>
    /// 获取或设置一个值，指示是否始终显示页眉。
    /// </summary>
    public bool AlwaysShowHeader
    {
        get => (bool)GetValue(AlwaysShowHeaderProperty);
        set => SetValue(AlwaysShowHeaderProperty, value);
    }

    /// <summary>
    /// 获取菜单项列表。
    /// </summary>
    public IList MenuItems => _menuItems;

    /// <summary>
    /// 获取或设置菜单项数据源。
    /// </summary>
    public object? MenuItemsSource
    {
        get => GetValue(MenuItemsSourceProperty);
        set => SetValue(MenuItemsSourceProperty, value);
    }

    /// <summary>
    /// 获取底部菜单项列表。
    /// </summary>
    public IList FooterMenuItems => _footerMenuItems;

    /// <summary>
    /// 获取或设置底部菜单项数据源。
    /// </summary>
    public object? FooterMenuItemsSource
    {
        get => GetValue(FooterMenuItemsSourceProperty);
        set => SetValue(FooterMenuItemsSourceProperty, value);
    }

    /// <summary>
    /// 获取或设置一个值，指示是否显示顶部分隔线。
    /// </summary>
    public bool IsTopSeparatorVisible
    {
        get => (bool)GetValue(IsTopSeparatorVisibleProperty);
        set => SetValue(IsTopSeparatorVisibleProperty, value);
    }

    /// <summary>
    /// 获取或设置一个值，指示是否显示底部分隔线。
    /// </summary>
    public bool IsFooterSeparatorVisible
    {
        get => (bool)GetValue(IsFooterSeparatorVisibleProperty);
        set => SetValue(IsFooterSeparatorVisibleProperty, value);
    }

    /// <summary>
    /// 获取当前选中的导航项。
    /// </summary>
    public INavigationViewItem? SelectedItem => _selectedEntry?.SourceNavigationViewItem;

    /// <summary>
    /// 获取或设置内容覆盖层。
    /// </summary>
    public object? ContentOverlay
    {
        get => GetValue(ContentOverlayProperty);
        set => SetValue(ContentOverlayProperty, value);
    }

    /// <summary>
    /// 获取一个值，指示是否启用后退按钮。
    /// </summary>
    public bool IsBackEnabled => (bool)GetValue(IsBackEnabledProperty);

    /// <summary>
    /// 获取或设置后退按钮的可见性。
    /// </summary>
    public NavigationViewBackButtonVisible IsBackButtonVisible
    {
        get => (NavigationViewBackButtonVisible)GetValue(IsBackButtonVisibleProperty);
        set => SetValue(IsBackButtonVisibleProperty, value);
    }

    /// <summary>
    /// 获取或设置一个值，指示是否显示面板切换按钮。
    /// </summary>
    public bool IsPaneToggleVisible
    {
        get => (bool)GetValue(IsPaneToggleVisibleProperty);
        set => SetValue(IsPaneToggleVisibleProperty, value);
    }

    /// <summary>
    /// 获取或设置一个值，指示面板是否打开。
    /// </summary>
    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    /// <summary>
    /// 获取或设置一个值，指示面板是否可见。
    /// </summary>
    public bool IsPaneVisible
    {
        get => (bool)GetValue(IsPaneVisibleProperty);
        set => SetValue(IsPaneVisibleProperty, value);
    }

    /// <summary>
    /// 获取或设置面板打开时的宽度。
    /// </summary>
    public double OpenPaneLength
    {
        get => (double)GetValue(OpenPaneLengthProperty);
        set => SetValue(OpenPaneLengthProperty, value);
    }

    /// <summary>
    /// 获取或设置面板收起时的宽度。
    /// </summary>
    public double CompactPaneLength
    {
        get => (double)GetValue(CompactPaneLengthProperty);
        set => SetValue(CompactPaneLengthProperty, value);
    }

    /// <summary>
    /// 获取面板的实际宽度。
    /// </summary>
    public double ActualPaneLength
    {
        get => (double)GetValue(ActualPaneLengthProperty);
        private set => SetValue(ActualPaneLengthProperty, value);
    }

    /// <summary>
    /// 获取或设置面板页眉内容。
    /// </summary>
    public object? PaneHeader
    {
        get => GetValue(PaneHeaderProperty);
        set => SetValue(PaneHeaderProperty, value);
    }

    /// <summary>
    /// 获取或设置面板标题。
    /// </summary>
    public string? PaneTitle
    {
        get => (string?)GetValue(PaneTitleProperty);
        set => SetValue(PaneTitleProperty, value);
    }

    /// <summary>
    /// 获取或设置面板底部内容。
    /// </summary>
    public object? PaneFooter
    {
        get => GetValue(PaneFooterProperty);
        set => SetValue(PaneFooterProperty, value);
    }

    /// <summary>
    /// 获取或设置面板显示模式。
    /// </summary>
    public NavigationViewPaneDisplayMode PaneDisplayMode
    {
        get => (NavigationViewPaneDisplayMode)GetValue(PaneDisplayModeProperty);
        set => SetValue(PaneDisplayModeProperty, value);
    }

    /// <summary>
    /// 获取或设置导航行为模式。
    /// </summary>
    public ModernNavigationBehavior NavigationBehavior
    {
        get => (ModernNavigationBehavior)GetValue(NavigationBehaviorProperty);
        set => SetValue(NavigationBehaviorProperty, value);
    }

    /// <summary>
    /// 获取或设置当前选中的导航条目。
    /// </summary>
    public ModernNavigationEntry? SelectedEntry
    {
        get => (ModernNavigationEntry?)GetValue(SelectedEntryProperty);
        set => SetValue(SelectedEntryProperty, value);
    }

    /// <summary>
    /// 获取或设置标题栏控件。
    /// </summary>
    public TitleBar? TitleBar
    {
        get => (TitleBar?)GetValue(TitleBarProperty);
        set => SetValue(TitleBarProperty, value);
    }

    /// <summary>
    /// 获取或设置自动建议框控件。
    /// </summary>
    public AutoSuggestBox? AutoSuggestBox
    {
        get => (AutoSuggestBox?)GetValue(AutoSuggestBoxProperty);
        set => SetValue(AutoSuggestBoxProperty, value);
    }

    /// <summary>
    /// 获取或设置面包屑导航控件。
    /// </summary>
    public BreadcrumbBar? BreadcrumbBar
    {
        get => (BreadcrumbBar?)GetValue(BreadcrumbBarProperty);
        set => SetValue(BreadcrumbBarProperty, value);
    }

    /// <summary>
    /// 获取或设置菜单项的控件模板。
    /// </summary>
    public ControlTemplate? ItemTemplate
    {
        get => (ControlTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>
    /// 获取或设置过渡动画持续时间（毫秒）。
    /// </summary>
    public int TransitionDuration
    {
        get => (int)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    /// <summary>
    /// 获取或设置一个值，指示是否启用页面切换过渡动画。关闭后页面切换将立即完成。
    /// </summary>
    public bool IsAnimationEnabled
    {
        get => (bool)GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    /// <summary>
    /// 获取或设置过渡动画类型。
    /// </summary>
    public Transition Transition
    {
        get => (Transition)GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>
    /// 获取或设置框架内边距。
    /// </summary>
    public Thickness FrameMargin
    {
        get => (Thickness)GetValue(FrameMarginProperty);
        set => SetValue(FrameMarginProperty, value);
    }

    /// <summary>
    /// 获取一个值，指示是否可以执行后退导航。
    /// </summary>
    public bool CanGoBack => PART_Frame.CanGoBack;

    /// <summary>
    /// 获取当前显示的内容元素。
    /// </summary>
    public FrameworkElement? CurrentContent => PART_Frame.CurrentContent;

    /// <summary>
    /// 获取内部的内容滚动宿主控件。
    /// </summary>
    public neo_bpsys_wpf.Controls.Modern.Scrolling.ModernScrollViewer ContentScrollHost => PART_Frame.ContentScrollHost;

    /// <summary>
    /// 当面板打开时引发。
    /// </summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs>? PaneOpened;

    /// <summary>
    /// 当面板关闭时引发。
    /// </summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs>? PaneClosed;

    /// <summary>
    /// 当选中项更改时引发。
    /// </summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs>? SelectionChanged;

    /// <summary>
    /// 当菜单项被调用时引发。
    /// </summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs>? ItemInvoked;

    /// <summary>
    /// 当请求后退导航时引发。
    /// </summary>
    public event TypedEventHandler<NavigationView, RoutedEventArgs>? BackRequested;

    /// <summary>
    /// 在导航即将发生时引发。
    /// </summary>
    public event TypedEventHandler<NavigationView, NavigatingCancelEventArgs>? Navigating;

    /// <summary>
    /// 在导航完成后引发。
    /// </summary>
    public event TypedEventHandler<NavigationView, NavigatedEventArgs>? Navigated;

    /// <summary>
    /// 导航到指定页面类型，可传递数据上下文。
    /// </summary>
    /// <param name="pageType">目标页面类型。</param>
    /// <param name="dataContext">页面数据上下文。</param>
    /// <returns>如果导航成功则为 <c>true</c>。</returns>
    public bool Navigate(Type pageType, object? dataContext = null)
    {
        ArgumentNullException.ThrowIfNull(pageType);

        var entry = FindEntryByPageType(pageType);
        return NavigateEntryOrPageType(entry, pageType, dataContext);
    }

    /// <summary>
    /// 通过页面 ID 或目标标签导航到指定页面。
    /// </summary>
    /// <param name="pageIdOrTargetTag">页面 ID 或目标标签。</param>
    /// <param name="dataContext">页面数据上下文。</param>
    /// <returns>如果导航成功则为 <c>true</c>。</returns>
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

    /// <summary>
    /// 带层级结构导航到指定页面类型。
    /// </summary>
    /// <param name="pageType">目标页面类型。</param>
    /// <param name="dataContext">页面数据上下文。</param>
    /// <returns>如果导航成功则为 <c>true</c>。</returns>
    public bool NavigateWithHierarchy(Type pageType, object? dataContext = null) =>
        Navigate(pageType, dataContext);

    /// <summary>
    /// 替换当前内容为指定页面类型。
    /// </summary>
    /// <param name="pageTypeToEmbed">要嵌入的页面类型。</param>
    /// <returns>如果替换成功则为 <c>true</c>。</returns>
    public bool ReplaceContent(Type pageTypeToEmbed)
    {
        ArgumentNullException.ThrowIfNull(pageTypeToEmbed);
        return PART_Frame.Navigate(CreatePageElement(pageTypeToEmbed, null));
    }

    /// <summary>
    /// 替换当前内容为指定的 UI 元素实例。
    /// </summary>
    /// <param name="pageInstanceToEmbed">要嵌入的 UI 元素实例。</param>
    /// <param name="dataContext">页面数据上下文。</param>
    /// <returns>如果替换成功则为 <c>true</c>。</returns>
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

    /// <summary>
    /// 前进导航。当前不支持。
    /// </summary>
    /// <returns>始终返回 <c>false</c>。</returns>
    public bool GoForward() => false;

    /// <summary>
    /// 返回到上一个页面。
    /// </summary>
    /// <returns>如果成功返回则为 <c>true</c>。</returns>
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

    /// <summary>
    /// 清除导航历史记录。
    /// </summary>
    public void ClearJournal()
    {
        PART_Frame.ClearJournal();
        _selectedEntryBackStack.Clear();
        SetValue(IsBackEnabledPropertyKey, false);
    }

    /// <summary>
    /// 设置页面Provider服务。
    /// </summary>
    /// <param name="navigationViewPageProvider">页面Provider。</param>
    public void SetPageProviderService(INavigationViewPageProvider navigationViewPageProvider)
    {
        _pageProvider = navigationViewPageProvider;
    }

    /// <summary>
    /// 设置依赖注入服务Provider。
    /// </summary>
    /// <param name="serviceProvider">服务Provider。</param>
    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        PART_Frame.ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// 刷新所有菜单项的本地化显示文本。
    /// </summary>
    public void RefreshLocalizedMenuText()
    {
        foreach (var entry in AllEntries())
        {
            entry.RefreshDisplayText();
        }
    }

    /// <summary>
    /// 如果当前没有选中项，则选中第一个可用的菜单项。
    /// </summary>
    /// <returns>如果执行了选择则为 <c>true</c>。</returns>
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

    private static void OnIsAnimationEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var navigationView = (ModernNavigationView)d;
        if (navigationView.PART_Frame is not null)
        {
            navigationView.PART_Frame.IsAnimationEnabled = (bool)e.NewValue;
        }
    }

    private static void OnNavigationBehaviorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ModernNavigationView)d).UpdateFrameScrollHostMode();
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
        UpdateFrameScrollHostMode();

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

    private void UpdateFrameScrollHostMode()
    {
        if (PART_Frame is null)
        {
            return;
        }

        PART_Frame.ContentScrollHostMode = NavigationBehavior == ModernNavigationBehavior.LocalTabs
            ? ModernFrameContentScrollHostMode.Auto
            : ModernFrameContentScrollHostMode.Enabled;
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
