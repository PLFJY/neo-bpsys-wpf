#nullable enable

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using neo_bpsys_wpf.Controls.Modern.Scrolling;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

public class ModernFrame : Control
{
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

    private readonly Grid _root;
    private readonly ContentPresenter _oldContentPresenter;
    private readonly ContentPresenter _newContentPresenter;
    private readonly ContentPresenter _directContentPresenter;
    private readonly ModernScrollViewer _contentScrollHost;
    private readonly List<ModernFrameJournalEntry> _backStack = new();
    private Storyboard? _activeExitStoryboard;
    private Storyboard? _activeEnterStoryboard;
    private DispatcherOperation? _pendingTransitionOperation;
    private FrameworkElement? _activeContent;
    private int _remainingTransitionStoryboards;

    public ModernFrame()
    {
        Focusable = false;
        IsTabStop = false;
        ClipToBounds = true;
        SetCurrentValue(DefaultTransitionInfoProperty, new EntranceNavigationTransitionInfo());

        _oldContentPresenter = new ContentPresenter
        {
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        _newContentPresenter = new ContentPresenter();
        _directContentPresenter = new ContentPresenter
        {
            Visibility = Visibility.Collapsed
        };
        _contentScrollHost = CreateScrollHost();

        _root = new Grid
        {
            Children =
            {
                _oldContentPresenter,
                _contentScrollHost,
                _directContentPresenter
            }
        };

        AddVisualChild(_root);
        AddLogicalChild(_root);
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

    public ModernScrollViewer ContentScrollHost => _contentScrollHost;

    protected override int VisualChildrenCount => 1;

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
        return NavigateCore(() => CreateContentFromType(pageType), parameter, transitionInfo, addCurrentToBackStack: true, ModernFrameNavigationMode.New);
    }

    public bool Navigate(FrameworkElement content)
    {
        return Navigate(content, null);
    }

    public bool Navigate(FrameworkElement content, ModernNavigationTransitionInfo? transitionInfo)
    {
        ArgumentNullException.ThrowIfNull(content);
        return NavigateCore(content, null, transitionInfo, addCurrentToBackStack: true, ModernFrameNavigationMode.New);
    }

    public bool Navigate(Func<FrameworkElement> contentFactory)
    {
        return Navigate(contentFactory, null);
    }

    public bool Navigate(Func<FrameworkElement> contentFactory, ModernNavigationTransitionInfo? transitionInfo)
    {
        ArgumentNullException.ThrowIfNull(contentFactory);
        return NavigateCore(contentFactory, null, transitionInfo, addCurrentToBackStack: true, ModernFrameNavigationMode.New);
    }

    public bool Navigate(object content)
    {
        return Navigate(content, null, null);
    }

    public bool Navigate(object content, object? parameter, ModernNavigationTransitionInfo? transitionInfo = null)
    {
        ArgumentNullException.ThrowIfNull(content);

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

    protected override Visual GetVisualChild(int index)
    {
        if (index != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _root;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _root.Measure(constraint);
        return _root.DesiredSize;
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        _root.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    private static void OnIsContentScrollHostEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
        var effectiveTransitionInfo = transitionInfo ?? DefaultTransitionInfo;
        var navigatingArgs = new ModernFrameNavigatingEventArgs(newContent, parameter, navigationMode, effectiveTransitionInfo);
        Navigating?.Invoke(this, navigatingArgs);

        if (navigatingArgs.Cancel)
        {
            return false;
        }

        StopTransition();

        var oldContent = _activeContent;
        if (addCurrentToBackStack && oldContent is not null)
        {
            _backStack.Add(new ModernFrameJournalEntry(oldContent, null, effectiveTransitionInfo));
            UpdateCanGoBack();
        }

        _activeContent = newContent;
        CurrentContent = newContent;

        BeginContentSwap(oldContent, newContent, effectiveTransitionInfo, navigationMode == ModernFrameNavigationMode.Back);
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

    private ModernScrollViewer CreateScrollHost()
    {
        return new ModernScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _newContentPresenter
        };
    }

    private void BeginContentSwap(
        FrameworkElement? oldContent,
        FrameworkElement newContent,
        ModernNavigationTransitionInfo? transitionInfo,
        bool movingBackwards)
    {
        DetachFromActiveHost();

        if (oldContent is null || !ShouldAnimate(transitionInfo))
        {
            _oldContentPresenter.Content = null;
            _oldContentPresenter.Visibility = Visibility.Collapsed;
            _oldContentPresenter.IsHitTestVisible = false;

            AttachToActiveHost(newContent);
            ClearAnimatedState(newContent);
            return;
        }

        _oldContentPresenter.Content = oldContent;
        _oldContentPresenter.Visibility = Visibility.Visible;
        _oldContentPresenter.Opacity = 1;
        _oldContentPresenter.IsHitTestVisible = false;

        AttachToActiveHost(newContent);
        _contentScrollHost.Opacity = 0;
        _contentScrollHost.IsHitTestVisible = false;
        _directContentPresenter.Opacity = 0;
        _directContentPresenter.IsHitTestVisible = false;

        var activeHost = GetActiveTransitionElement();

        _activeExitStoryboard = transitionInfo?.CreateExitStoryboard(_oldContentPresenter, movingBackwards, TransitionDuration);
        _activeEnterStoryboard = transitionInfo?.CreateEnterStoryboard(activeHost, movingBackwards, TransitionDuration);

        if (_activeExitStoryboard is null && _activeEnterStoryboard is null)
        {
            CompleteTransition(newContent);
            return;
        }

        if (_activeExitStoryboard is not null)
        {
            _activeExitStoryboard.Completed += OnTransitionStoryboardCompleted;
        }

        if (_activeEnterStoryboard is not null)
        {
            _activeEnterStoryboard.Completed += OnTransitionStoryboardCompleted;
        }

        _remainingTransitionStoryboards = (_activeExitStoryboard is null ? 0 : 1) + (_activeEnterStoryboard is null ? 0 : 1);

        _pendingTransitionOperation = Dispatcher.BeginInvoke(() =>
        {
            _pendingTransitionOperation = null;
            _activeExitStoryboard?.Begin(_oldContentPresenter, true);
            _activeEnterStoryboard?.Begin(activeHost, true);
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

    private void OnTransitionStoryboardCompleted(object? sender, EventArgs e)
    {
        _remainingTransitionStoryboards = Math.Max(0, _remainingTransitionStoryboards - 1);

        if (_remainingTransitionStoryboards == 0)
        {
            CompleteTransition(_activeContent);
        }
    }

    private void CompleteTransition(FrameworkElement? activeContent)
    {
        var activeHost = GetActiveTransitionElement();

        _activeExitStoryboard?.Remove(_oldContentPresenter);
        _activeEnterStoryboard?.Remove(activeHost);
        _activeExitStoryboard = null;
        _activeEnterStoryboard = null;
        _remainingTransitionStoryboards = 0;

        _oldContentPresenter.Content = null;
        _oldContentPresenter.Visibility = Visibility.Collapsed;
        _oldContentPresenter.IsHitTestVisible = false;
        _oldContentPresenter.ClearValue(OpacityProperty);
        _oldContentPresenter.ClearValue(RenderTransformProperty);

        _contentScrollHost.IsHitTestVisible = true;
        _contentScrollHost.ClearValue(OpacityProperty);
        _contentScrollHost.ClearValue(RenderTransformProperty);

        _directContentPresenter.IsHitTestVisible = true;
        _directContentPresenter.ClearValue(OpacityProperty);
        _directContentPresenter.ClearValue(RenderTransformProperty);

        if (activeContent is not null)
        {
            ClearAnimatedState(activeContent);
        }
    }

    private void StopTransition()
    {
        if (_pendingTransitionOperation is not null)
        {
            _pendingTransitionOperation.Abort();
            _pendingTransitionOperation = null;
        }

        _activeExitStoryboard?.Remove(_oldContentPresenter);
        _activeEnterStoryboard?.Remove(GetActiveTransitionElement());
        _activeExitStoryboard = null;
        _activeEnterStoryboard = null;
        _remainingTransitionStoryboards = 0;

        _oldContentPresenter.Content = null;
        _oldContentPresenter.Visibility = Visibility.Collapsed;
        _oldContentPresenter.IsHitTestVisible = false;
        _oldContentPresenter.ClearValue(OpacityProperty);
        _oldContentPresenter.ClearValue(RenderTransformProperty);

        _contentScrollHost.IsHitTestVisible = true;
        _contentScrollHost.ClearValue(OpacityProperty);
        _contentScrollHost.ClearValue(RenderTransformProperty);

        _directContentPresenter.IsHitTestVisible = true;
        _directContentPresenter.ClearValue(OpacityProperty);
        _directContentPresenter.ClearValue(RenderTransformProperty);
    }

    private void UpdateActiveHost()
    {
        if (_activeContent is null)
        {
            return;
        }

        DetachFromActiveHost();
        AttachToActiveHost(_activeContent);
    }

    private void AttachToActiveHost(FrameworkElement content)
    {
        if (IsContentScrollHostEnabled)
        {
            _directContentPresenter.Content = null;
            _directContentPresenter.Visibility = Visibility.Collapsed;
            _contentScrollHost.Visibility = Visibility.Visible;
            _newContentPresenter.Content = content;
            _contentScrollHost.Content = _newContentPresenter;
        }
        else
        {
            _newContentPresenter.Content = null;
            _contentScrollHost.Visibility = Visibility.Collapsed;
            _directContentPresenter.Visibility = Visibility.Visible;
            _directContentPresenter.Content = content;
        }
    }

    private void DetachFromActiveHost()
    {
        _newContentPresenter.Content = null;
        _directContentPresenter.Content = null;

        if (!ReferenceEquals(_contentScrollHost.Content, _newContentPresenter))
        {
            _contentScrollHost.Content = _newContentPresenter;
        }
    }

    private FrameworkElement GetActiveTransitionElement()
    {
        return IsContentScrollHostEnabled ? _contentScrollHost : _directContentPresenter;
    }

    private static void ClearAnimatedState(FrameworkElement element)
    {
        element.ClearValue(OpacityProperty);
        element.ClearValue(RenderTransformProperty);
    }

    private void UpdateCanGoBack()
    {
        CanGoBack = _backStack.Count > 0;
    }
}
