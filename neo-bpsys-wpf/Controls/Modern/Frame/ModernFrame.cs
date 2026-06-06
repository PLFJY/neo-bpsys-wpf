#nullable enable

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using neo_bpsys_wpf.Controls.Modern.Scrolling;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

[TemplatePart(Name = RootPartName, Type = typeof(Grid))]
[TemplatePart(Name = OldContentPresenterPartName, Type = typeof(ContentPresenter))]
[TemplatePart(Name = NewContentPresenterPartName, Type = typeof(ContentPresenter))]
[TemplatePart(Name = DirectContentPresenterPartName, Type = typeof(ContentPresenter))]
[TemplatePart(Name = ContentScrollHostPartName, Type = typeof(ModernScrollViewer))]
public class ModernFrame : Control
{
    private const string RootPartName = "PART_Root";
    private const string OldContentPresenterPartName = "PART_OldContentPresenter";
    private const string NewContentPresenterPartName = "PART_NewContentPresenter";
    private const string DirectContentPresenterPartName = "PART_DirectContentPresenter";
    private const string ContentScrollHostPartName = "PART_ContentScrollHost";

    public static readonly DependencyProperty DefaultTransitionInfoProperty =
        DependencyProperty.Register(
            nameof(DefaultTransitionInfo),
            typeof(ModernNavigationTransitionInfo),
            typeof(ModernFrame),
            new PropertyMetadata(null));

    public static readonly DependencyProperty TransitionDurationProperty =
        DependencyProperty.Register(
            nameof(TransitionDuration),
            typeof(TimeSpan),
            typeof(ModernFrame),
            new PropertyMetadata(TimeSpan.FromMilliseconds(240)));

    public static readonly DependencyProperty IsAnimationEnabledProperty =
        DependencyProperty.Register(
            nameof(IsAnimationEnabled),
            typeof(bool),
            typeof(ModernFrame),
            new PropertyMetadata(true));

    public static readonly DependencyProperty IsContentScrollHostEnabledProperty =
        DependencyProperty.Register(
            nameof(IsContentScrollHostEnabled),
            typeof(bool),
            typeof(ModernFrame),
            new PropertyMetadata(true, OnIsContentScrollHostEnabledChanged));

    public static readonly DependencyProperty ContentScrollHostModeProperty =
        DependencyProperty.Register(
            nameof(ContentScrollHostMode),
            typeof(ModernFrameContentScrollHostMode),
            typeof(ModernFrame),
            new PropertyMetadata(ModernFrameContentScrollHostMode.Enabled, OnContentScrollHostModeChanged));

    public static readonly DependencyProperty ResetScrollOnNavigationProperty =
        DependencyProperty.Register(
            nameof(ResetScrollOnNavigation),
            typeof(bool),
            typeof(ModernFrame),
            new PropertyMetadata(true));

    public static readonly DependencyProperty CurrentContentProperty =
        DependencyProperty.Register(
            nameof(CurrentContent),
            typeof(FrameworkElement),
            typeof(ModernFrame),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CanGoBackProperty =
        DependencyProperty.Register(
            nameof(CanGoBack),
            typeof(bool),
            typeof(ModernFrame),
            new PropertyMetadata(false));

    private readonly List<ModernFrameJournalEntry> _backStack = new();
    private Storyboard? _activeExitStoryboard;
    private Storyboard? _activeEnterStoryboard;
    private DispatcherOperation? _pendingTransitionOperation;
    private FrameworkElement? _activeContent;
    private Grid? _root;
    private ContentPresenter? _oldContentPresenter;
    private ContentPresenter? _newContentPresenter;
    private ContentPresenter? _directContentPresenter;
    private ModernScrollViewer? _contentScrollHost;
    private FrameworkElement? _activeHostedContent;
    private bool _isUsingContentScrollHostForActiveContent = true;

    public ModernFrame()
    {
        Focusable = false;
        IsTabStop = false;
        ClipToBounds = true;
        SetCurrentValue(DefaultTransitionInfoProperty, new EntranceNavigationTransitionInfo());
        Template = CreateDefaultTemplate();
    }

    public ModernNavigationTransitionInfo DefaultTransitionInfo
    {
        get => (ModernNavigationTransitionInfo)GetValue(DefaultTransitionInfoProperty);
        set => SetValue(DefaultTransitionInfoProperty, value);
    }

    public TimeSpan TransitionDuration
    {
        get => (TimeSpan)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    public bool IsAnimationEnabled
    {
        get => (bool)GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    public bool IsContentScrollHostEnabled
    {
        get => (bool)GetValue(IsContentScrollHostEnabledProperty);
        set => SetValue(IsContentScrollHostEnabledProperty, value);
    }

    public ModernFrameContentScrollHostMode ContentScrollHostMode
    {
        get => (ModernFrameContentScrollHostMode)GetValue(ContentScrollHostModeProperty);
        set => SetValue(ContentScrollHostModeProperty, value);
    }

    public bool ResetScrollOnNavigation
    {
        get => (bool)GetValue(ResetScrollOnNavigationProperty);
        set => SetValue(ResetScrollOnNavigationProperty, value);
    }

    public FrameworkElement? CurrentContent
    {
        get => (FrameworkElement?)GetValue(CurrentContentProperty);
        private set => SetValue(CurrentContentProperty, value);
    }

    public bool CanGoBack
    {
        get => (bool)GetValue(CanGoBackProperty);
        private set => SetValue(CanGoBackProperty, value);
    }

    public IServiceProvider? ServiceProvider { get; set; }

    public ModernScrollViewer ContentScrollHost
    {
        get
        {
            EnsureTemplateParts();
            return _contentScrollHost!;
        }
    }

    public event EventHandler<ModernFrameNavigatingEventArgs>? Navigating;

    public event EventHandler<ModernFrameNavigationEventArgs>? Navigated;

    public bool Navigate(Type pageType)
    {
        return Navigate(pageType, null, null);
    }

    public bool Navigate(Type pageType, object? parameter)
    {
        return Navigate(pageType, parameter, null);
    }

    public bool Navigate(Type pageType, object? parameter, ModernNavigationTransitionInfo? transitionInfo)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        EnsureTemplateParts();
        return NavigateCore(() => CreateContentFromType(pageType), parameter, transitionInfo, addCurrentToBackStack: true, ModernFrameNavigationMode.New);
    }

    public bool Navigate(FrameworkElement content)
    {
        return Navigate(content, null);
    }

    public bool Navigate(FrameworkElement content, ModernNavigationTransitionInfo? transitionInfo)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureTemplateParts();
        return NavigateCore(content, null, transitionInfo, addCurrentToBackStack: true, ModernFrameNavigationMode.New);
    }

    public bool Navigate(Func<FrameworkElement> contentFactory)
    {
        return Navigate(contentFactory, null);
    }

    public bool Navigate(Func<FrameworkElement> contentFactory, ModernNavigationTransitionInfo? transitionInfo)
    {
        ArgumentNullException.ThrowIfNull(contentFactory);
        EnsureTemplateParts();
        return NavigateCore(contentFactory, null, transitionInfo, addCurrentToBackStack: true, ModernFrameNavigationMode.New);
    }

    public bool Navigate(object content)
    {
        return Navigate(content, null, null);
    }

    public bool Navigate(object content, object? parameter, ModernNavigationTransitionInfo? transitionInfo = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureTemplateParts();

        return content switch
        {
            Type pageType => Navigate(pageType, parameter, transitionInfo),
            FrameworkElement element => NavigateCore(element, parameter, transitionInfo, addCurrentToBackStack: true, ModernFrameNavigationMode.New),
            Func<FrameworkElement> factory => NavigateCore(factory, parameter, transitionInfo, addCurrentToBackStack: true, ModernFrameNavigationMode.New),
            _ => throw new ArgumentException("ModernFrame content must be a Type, FrameworkElement, or Func<FrameworkElement>.", nameof(content))
        };
    }

    public bool GoBack()
    {
        EnsureTemplateParts();

        if (_backStack.Count == 0)
        {
            return false;
        }

        var entryIndex = _backStack.Count - 1;
        var entry = _backStack[entryIndex];
        _backStack.RemoveAt(entryIndex);
        UpdateCanGoBack();

        return NavigateCore(entry.CreateContent(), entry.Parameter, entry.TransitionInfo, addCurrentToBackStack: false, ModernFrameNavigationMode.Back);
    }

    public void ClearJournal()
    {
        _backStack.Clear();
        UpdateCanGoBack();
    }

    public override void OnApplyTemplate()
    {
        var oldHostedContent = _activeHostedContent;
        StopTransition();
        DetachFromActiveHost();
        ReleaseHostedContent(oldHostedContent);

        base.OnApplyTemplate();

        _root = GetTemplateChild(RootPartName) as Grid
            ?? throw new InvalidOperationException($"ModernFrame template must contain a {nameof(Grid)} named {RootPartName}.");
        _oldContentPresenter = GetTemplateChild(OldContentPresenterPartName) as ContentPresenter
            ?? throw new InvalidOperationException($"ModernFrame template must contain a {nameof(ContentPresenter)} named {OldContentPresenterPartName}.");
        _newContentPresenter = GetTemplateChild(NewContentPresenterPartName) as ContentPresenter
            ?? throw new InvalidOperationException($"ModernFrame template must contain a {nameof(ContentPresenter)} named {NewContentPresenterPartName}.");
        _directContentPresenter = GetTemplateChild(DirectContentPresenterPartName) as ContentPresenter
            ?? throw new InvalidOperationException($"ModernFrame template must contain a {nameof(ContentPresenter)} named {DirectContentPresenterPartName}.");
        _contentScrollHost = GetTemplateChild(ContentScrollHostPartName) as ModernScrollViewer
            ?? throw new InvalidOperationException($"ModernFrame template must contain a {nameof(ModernScrollViewer)} named {ContentScrollHostPartName}.");

        if (!ReferenceEquals(_contentScrollHost.Content, _newContentPresenter))
        {
            _contentScrollHost.Content = _newContentPresenter;
        }

        if (_activeContent is not null)
        {
            AttachToActiveHost(_activeContent);
        }
    }

    private static void OnIsContentScrollHostEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ModernFrame)d).UpdateActiveHost();
    }

    private static void OnContentScrollHostModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ModernFrame)d).UpdateActiveHost();
    }

    private bool NavigateCore(
        Func<FrameworkElement> contentFactory,
        object? parameter,
        ModernNavigationTransitionInfo? transitionInfo,
        bool addCurrentToBackStack,
        ModernFrameNavigationMode navigationMode)
    {
        return NavigateCore(contentFactory(), parameter, transitionInfo, addCurrentToBackStack, navigationMode);
    }

    private bool NavigateCore(
        FrameworkElement newContent,
        object? parameter,
        ModernNavigationTransitionInfo? transitionInfo,
        bool addCurrentToBackStack,
        ModernFrameNavigationMode navigationMode)
    {
        EnsureTemplateParts();

        var effectiveTransitionInfo = transitionInfo ?? DefaultTransitionInfo;
        var navigatingArgs = new ModernFrameNavigatingEventArgs(newContent, parameter, navigationMode, effectiveTransitionInfo);
        Navigating?.Invoke(this, navigatingArgs);

        if (navigatingArgs.Cancel)
        {
            return false;
        }

        StopTransition();

        var oldContent = _activeContent;
        var oldHostedContent = _activeHostedContent;
        if (addCurrentToBackStack && oldContent is not null)
        {
            _backStack.Add(new ModernFrameJournalEntry(oldContent, null, effectiveTransitionInfo));
            UpdateCanGoBack();
        }

        _activeContent = newContent;
        CurrentContent = newContent;

        BeginContentSwap(
            oldContent,
            oldHostedContent,
            newContent,
            effectiveTransitionInfo,
            navigationMode == ModernFrameNavigationMode.Back,
            ResetScrollOnNavigation && navigationMode == ModernFrameNavigationMode.New);
        Navigated?.Invoke(this, new ModernFrameNavigationEventArgs(newContent, parameter, navigationMode, effectiveTransitionInfo));
        return true;
    }

    private FrameworkElement CreateContentFromType(Type pageType)
    {
        object? instance = null;

        if (ServiceProvider is not null)
        {
            instance = ServiceProvider.GetService(pageType);
        }

        instance ??= Activator.CreateInstance(pageType);

        return instance as FrameworkElement
            ?? throw new InvalidOperationException($"ModernFrame page type '{pageType.FullName}' must create a FrameworkElement.");
    }

    private static ControlTemplate CreateDefaultTemplate()
    {
        var root = new FrameworkElementFactory(typeof(Grid), RootPartName);

        var oldContentPresenter = new FrameworkElementFactory(typeof(ContentPresenter), OldContentPresenterPartName);
        oldContentPresenter.SetValue(VisibilityProperty, Visibility.Collapsed);
        oldContentPresenter.SetValue(IsHitTestVisibleProperty, false);
        root.AppendChild(oldContentPresenter);

        var contentScrollHost = new FrameworkElementFactory(typeof(ModernScrollViewer), ContentScrollHostPartName);
        contentScrollHost.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        contentScrollHost.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        contentScrollHost.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter), NewContentPresenterPartName));
        root.AppendChild(contentScrollHost);

        var directContentPresenter = new FrameworkElementFactory(typeof(ContentPresenter), DirectContentPresenterPartName);
        directContentPresenter.SetValue(VisibilityProperty, Visibility.Collapsed);
        root.AppendChild(directContentPresenter);

        return new ControlTemplate(typeof(ModernFrame))
        {
            VisualTree = root
        };
    }

    private void BeginContentSwap(
        FrameworkElement? oldContent,
        FrameworkElement? oldHostedContent,
        FrameworkElement newContent,
        ModernNavigationTransitionInfo? transitionInfo,
        bool movingBackwards,
        bool resetScroll)
    {
        DetachFromActiveHost();

        if (oldContent is null || !ShouldAnimate(transitionInfo))
        {
            ReleaseHostedContent(oldHostedContent);
            _oldContentPresenter.Content = null;
            _oldContentPresenter.Visibility = Visibility.Collapsed;
            _oldContentPresenter.IsHitTestVisible = false;

            AttachToActiveHost(newContent, resetScroll);
            ClearAnimatedState(newContent);
            RestoreActiveHostState();
            return;
        }

        _oldContentPresenter.Content = oldHostedContent ?? oldContent;
        _oldContentPresenter.Visibility = Visibility.Visible;
        _oldContentPresenter.Opacity = 1;
        _oldContentPresenter.IsHitTestVisible = false;

        AttachToActiveHost(newContent, resetScroll);
        _contentScrollHost.Opacity = 0;
        _contentScrollHost.IsHitTestVisible = false;
        _directContentPresenter.Opacity = 0;
        _directContentPresenter.IsHitTestVisible = false;

        var activeHost = GetActiveTransitionElement();

        _activeExitStoryboard = transitionInfo?.CreateExitStoryboard(_oldContentPresenter, movingBackwards, TransitionDuration);
        _activeEnterStoryboard = transitionInfo?.CreateEnterStoryboard(activeHost, movingBackwards, TransitionDuration);

        if (_activeExitStoryboard is null)
        {
            ClearOldTransitionPresenter();
            BeginEnterTransition();
            return;
        }

        _activeExitStoryboard.Completed += OnExitStoryboardCompleted;

        _pendingTransitionOperation = Dispatcher.BeginInvoke(() =>
        {
            _pendingTransitionOperation = null;
            try
            {
                _activeExitStoryboard?.Begin(_oldContentPresenter, true);
            }
            catch (Exception)
            {
                _activeExitStoryboard = null;
                ClearOldTransitionPresenter();
                BeginEnterTransition();
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    private bool ShouldAnimate(ModernNavigationTransitionInfo? transitionInfo)
    {
        return IsAnimationEnabled
            && SystemParameters.ClientAreaAnimation
            && RenderCapability.Tier > 0
            && TransitionDuration > TimeSpan.Zero
            && transitionInfo is not null
            && transitionInfo is not SuppressNavigationTransitionInfo;
    }

    private void OnExitStoryboardCompleted(object? sender, EventArgs e)
    {
        if (!HasTemplateParts)
        {
            return;
        }

        _activeExitStoryboard?.Remove(_oldContentPresenter);
        _activeExitStoryboard = null;
        ClearOldTransitionPresenter();
        BeginEnterTransition();
    }

    private void BeginEnterTransition()
    {
        if (!HasTemplateParts)
        {
            return;
        }

        var activeHost = GetActiveTransitionElement();

        if (_activeEnterStoryboard is null)
        {
            CompleteTransition(_activeContent);
            return;
        }

        _activeEnterStoryboard.Completed += OnEnterStoryboardCompleted;
        _pendingTransitionOperation = Dispatcher.BeginInvoke(() =>
        {
            _pendingTransitionOperation = null;
            try
            {
                RestoreActiveHostVisibilityForEnter(activeHost);
                _activeEnterStoryboard?.Begin(activeHost, true);
            }
            catch (Exception)
            {
                CompleteTransition(_activeContent);
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    private void OnEnterStoryboardCompleted(object? sender, EventArgs e)
    {
        CompleteTransition(_activeContent);
    }

    private void CompleteTransition(FrameworkElement? activeContent)
    {
        if (!HasTemplateParts)
        {
            return;
        }

        var activeHost = GetActiveTransitionElement();

        _activeExitStoryboard?.Remove(_oldContentPresenter);
        _activeEnterStoryboard?.Remove(activeHost);
        _activeExitStoryboard = null;
        _activeEnterStoryboard = null;

        ClearOldTransitionPresenter();

        _contentScrollHost.IsHitTestVisible = true;
        NormalizeActiveTransitionElement(_contentScrollHost);

        _directContentPresenter.IsHitTestVisible = true;
        NormalizeActiveTransitionElement(_directContentPresenter);
        RestoreActiveHostState();

        if (activeContent is not null)
        {
            ClearAnimatedState(activeContent);
        }
    }

    private void StopTransition()
    {
        if (!HasTemplateParts)
        {
            return;
        }

        if (_pendingTransitionOperation is not null)
        {
            _pendingTransitionOperation.Abort();
            _pendingTransitionOperation = null;
        }

        _activeExitStoryboard?.Remove(_oldContentPresenter);
        _activeEnterStoryboard?.Remove(GetActiveTransitionElement());
        _activeExitStoryboard = null;
        _activeEnterStoryboard = null;

        ClearOldTransitionPresenter();

        _contentScrollHost.IsHitTestVisible = true;
        _contentScrollHost.ClearValue(OpacityProperty);
        _contentScrollHost.ClearValue(RenderTransformProperty);

        _directContentPresenter.IsHitTestVisible = true;
        _directContentPresenter.ClearValue(OpacityProperty);
        _directContentPresenter.ClearValue(RenderTransformProperty);
        RestoreActiveHostState();
    }

    private void ClearOldTransitionPresenter()
    {
        if (!HasTemplateParts)
        {
            return;
        }

        ReleaseHostedContent(_oldContentPresenter.Content as FrameworkElement);
        _oldContentPresenter.Content = null;
        _oldContentPresenter.Visibility = Visibility.Collapsed;
        _oldContentPresenter.IsHitTestVisible = false;
        _oldContentPresenter.ClearValue(OpacityProperty);
        _oldContentPresenter.ClearValue(RenderTransformProperty);
    }

    private void RestoreActiveHostVisibilityForEnter(FrameworkElement activeHost)
    {
        activeHost.Visibility = Visibility.Visible;
        activeHost.IsHitTestVisible = false;
    }

    private void UpdateActiveHost()
    {
        EnsureTemplateParts();

        if (_activeContent is null)
        {
            return;
        }

        DetachFromActiveHost();
        AttachToActiveHost(_activeContent);
    }

    private void AttachToActiveHost(FrameworkElement content, bool resetScroll = false)
    {
        EnsureTemplateParts();

        var hostedContent = CreateHostedContent(content);
        _activeHostedContent = hostedContent;
        _isUsingContentScrollHostForActiveContent = ShouldUseContentScrollHost(content, hostedContent);

        if (_isUsingContentScrollHostForActiveContent)
        {
            _directContentPresenter.Content = null;
            _directContentPresenter.Visibility = Visibility.Collapsed;
            _contentScrollHost.Visibility = Visibility.Visible;
            _newContentPresenter.Content = hostedContent;
            _contentScrollHost.Content = _newContentPresenter;
        }
        else
        {
            _newContentPresenter.Content = null;
            _contentScrollHost.Visibility = Visibility.Collapsed;
            _directContentPresenter.Visibility = Visibility.Visible;
            _directContentPresenter.Content = hostedContent;
        }

        RestoreActiveHostState();

        if (resetScroll)
        {
            ResetActiveScrollHostToTop();
        }
    }

    private void DetachFromActiveHost()
    {
        if (!HasTemplateParts)
        {
            return;
        }

        _newContentPresenter.Content = null;
        _directContentPresenter.Content = null;
        _activeHostedContent = null;

        if (!ReferenceEquals(_contentScrollHost.Content, _newContentPresenter))
        {
            _contentScrollHost.Content = _newContentPresenter;
        }
    }

    private bool ShouldUseContentScrollHost(FrameworkElement content, FrameworkElement hostedContent)
    {
        if (!IsContentScrollHostEnabled)
        {
            return false;
        }

        return ContentScrollHostMode switch
        {
            ModernFrameContentScrollHostMode.Enabled => true,
            ModernFrameContentScrollHostMode.Disabled => false,
            ModernFrameContentScrollHostMode.Auto => !ContentRequestsDirectScrollOwnership(content, hostedContent),
            _ => true
        };
    }

    private static bool ContentRequestsDirectScrollOwnership(FrameworkElement content, FrameworkElement hostedContent)
    {
        return ModernScroll.GetOwnership(content) == ModernScrollOwnership.Self
            || ModernScroll.GetOwnership(hostedContent) == ModernScrollOwnership.Self;
    }

    private static FrameworkElement CreateHostedContent(FrameworkElement content)
    {
        if (content is not Page page)
        {
            return content;
        }

        return new ModernFramePageHost(page);
    }

    private static void ReleaseHostedContent(FrameworkElement? hostedContent)
    {
        if (hostedContent is ModernFramePageHost frame)
        {
            frame.ClearPage();
        }
    }

    private FrameworkElement GetActiveTransitionElement()
    {
        EnsureTemplateParts();
        return _isUsingContentScrollHostForActiveContent ? _contentScrollHost : _directContentPresenter;
    }

    private void ResetActiveScrollHostToTop()
    {
        if (!HasTemplateParts || !_isUsingContentScrollHostForActiveContent)
        {
            return;
        }

        ScrollAnimationHelper.CancelVerticalAnimation(_contentScrollHost!);
        _contentScrollHost!.ScrollToVerticalOffset(0);
        _contentScrollHost.UpdateLayout();
    }

    private void RestoreActiveHostState()
    {
        if (!HasTemplateParts)
        {
            return;
        }

        if (_isUsingContentScrollHostForActiveContent)
        {
            _contentScrollHost.Visibility = Visibility.Visible;
            _contentScrollHost.IsHitTestVisible = true;
            NormalizeActiveTransitionElement(_contentScrollHost);

            _directContentPresenter.Visibility = Visibility.Collapsed;
        }
        else
        {
            _directContentPresenter.Visibility = Visibility.Visible;
            _directContentPresenter.IsHitTestVisible = true;
            NormalizeActiveTransitionElement(_directContentPresenter);

            _contentScrollHost.Visibility = Visibility.Collapsed;
        }
    }

    private static void ClearAnimatedState(FrameworkElement element)
    {
        element.ClearValue(OpacityProperty);
    }

    private static void NormalizeActiveTransitionElement(FrameworkElement element)
    {
        element.Opacity = 1;

        if (element.RenderTransform is TranslateTransform translateTransform)
        {
            translateTransform.X = 0;
            translateTransform.Y = 0;
        }
    }

    private void UpdateCanGoBack()
    {
        CanGoBack = _backStack.Count > 0;
    }

    private bool HasTemplateParts =>
        _root is not null
        && _oldContentPresenter is not null
        && _newContentPresenter is not null
        && _directContentPresenter is not null
        && _contentScrollHost is not null;

    private void EnsureTemplateParts()
    {
        if (HasTemplateParts)
        {
            return;
        }

        ApplyTemplate();

        if (!HasTemplateParts)
        {
            throw new InvalidOperationException("ModernFrame template parts are unavailable.");
        }
    }

    private sealed class ModernFramePageHost : System.Windows.Controls.Frame
    {
        private readonly Page _page;
        private bool _isPageNavigated;
        private bool _isCleared;

        public ModernFramePageHost(Page page)
        {
            _page = page;
            Focusable = false;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isCleared || _isPageNavigated)
            {
                return;
            }

            Dispatcher.BeginInvoke(NavigateToPage, DispatcherPriority.Loaded);
        }

        private void NavigateToPage()
        {
            if (_isCleared || _isPageNavigated)
            {
                return;
            }

            _isPageNavigated = true;
            Navigate(_page);
        }

        public void ClearPage()
        {
            Loaded -= OnLoaded;
            _isCleared = true;
            _isPageNavigated = false;

            while (CanGoBack)
            {
                RemoveBackEntry();
            }

            Content = null;
        }
    }
}
