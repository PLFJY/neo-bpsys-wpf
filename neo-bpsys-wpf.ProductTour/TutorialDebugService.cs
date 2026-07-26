using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using UiFluentWindow = Wpf.Ui.Controls.FluentWindow;
using UiListView = Wpf.Ui.Controls.ListView;
using UiTitleBar = Wpf.Ui.Controls.TitleBar;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>产品导览调试队列服务。</summary>
public interface ITutorialDebugService : ITutorialRunObserver
{
    /// <summary>获取调试窗口是否启用。</summary>
    bool IsEnabled { get; }

    /// <summary>获取当前调试队列的页面键。</summary>
    string? CurrentPageKey { get; }

    /// <summary>获取当前调试队列项。</summary>
    ReadOnlyObservableCollection<TutorialDebugQueueItem> QueueItems { get; }

    /// <summary>当队列跳转请求已应用时发生。</summary>
    event EventHandler<TutorialDebugJumpRequestedEventArgs>? JumpRequested;

    /// <summary>更新当前正在运行的页面教程队列。</summary>
    /// <param name="owner">教程所有者。</param>
    /// <param name="pageKey">教程页面键。</param>
    void SetCurrentQueue(FrameworkElement owner, string pageKey);

    /// <summary>使用显式包顺序更新当前教程队列。</summary>
    /// <param name="owner">教程所有者。</param>
    /// <param name="queueKey">队列或流程的稳定键。</param>
    /// <param name="packageIds">按运行顺序排列的包 id。</param>
    void SetCurrentQueue(FrameworkElement owner, string queueKey, IReadOnlyList<string> packageIds);

    /// <summary>请求从指定包重新开始当前队列。</summary>
    /// <param name="packageId">新的队列起点。</param>
    /// <param name="persist">是否将跳转状态写入持久化状态。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>跳转状态是否已应用。</returns>
    Task<bool> RequestJumpAsync(string packageId, bool persist, CancellationToken cancellationToken = default);

    /// <summary>判断包在持久化状态与当前调试会话覆盖层下是否已完成。</summary>
    /// <param name="state">已加载的持久化状态。</param>
    /// <param name="package">包定义。</param>
    /// <returns>若包应被跳过则为 <see langword="true"/>。</returns>
    bool IsPackageCompleted(TutorialState state, TutorialPackageDefinition package);

    /// <summary>消费指定队列的一次调试重启请求。</summary>
    /// <param name="owner">教程所有者。</param>
    /// <param name="pageKey">教程页面键。</param>
    /// <returns>若应立即重新解析队列则为 <see langword="true"/>。</returns>
    bool ConsumeRestart(FrameworkElement owner, string pageKey);
}

/// <summary>调试窗口中显示的教程包。</summary>
public sealed class TutorialDebugQueueItem : INotifyPropertyChanged
{
    private bool _isCompleted;
    private bool _isCurrent;

    /// <summary>获取初始化 <see cref="TutorialDebugQueueItem"/> 类的新实例。</summary>
    /// <param name="packageId">稳定的教程包 id。</param>
    public TutorialDebugQueueItem(string packageId) => PackageId = packageId;

    /// <summary>获取教程包 id。</summary>
    public string PackageId { get; }

    /// <summary>获取或设置包是否已完成。</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }

    /// <summary>获取或设置包是否正在执行。</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    /// <summary>当属性值变化时发生。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>调试器请求重启教程队列时提供的上下文。</summary>
public sealed class TutorialDebugJumpRequestedEventArgs(FrameworkElement owner, string pageKey) : EventArgs
{
    /// <summary>获取教程所有者。</summary>
    public FrameworkElement Owner { get; } = owner;

    /// <summary>获取教程页面键。</summary>
    public string PageKey { get; } = pageKey;
}

/// <summary>默认产品导览调试服务。</summary>
public sealed class TutorialDebugService : ITutorialDebugService, INotifyPropertyChanged
{
    private readonly ProductTourOptions _options;
    private readonly ITutorialSequenceRegistry _sequenceRegistry;
    private readonly ITutorialPackageRegistry _packageRegistry;
    private readonly ITutorialStateStore _stateStore;
    private readonly ObservableCollection<TutorialDebugQueueItem> _items = [];
    private readonly Dictionary<string, bool> _sessionOverrides = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();
    private TutorialDebugWindow? _window;
    private FrameworkElement? _owner;
    private string? _currentPageKey;
    private string? _currentPackageId;
    private IReadOnlyList<string> _currentPackageIds = [];
    private bool _restartRequested;

    /// <summary>初始化 <see cref="TutorialDebugService"/> 类的新实例。</summary>
    /// <param name="options">产品导览选项。</param>
    /// <param name="sequenceRegistry">教程队列注册表。</param>
    /// <param name="packageRegistry">教程包注册表。</param>
    /// <param name="stateStore">教程状态存储。</param>
    public TutorialDebugService(
        ProductTourOptions options,
        ITutorialSequenceRegistry sequenceRegistry,
        ITutorialPackageRegistry packageRegistry,
        ITutorialStateStore stateStore)
    {
        _options = options;
        _sequenceRegistry = sequenceRegistry;
        _packageRegistry = packageRegistry;
        _stateStore = stateStore;
        QueueItems = new ReadOnlyObservableCollection<TutorialDebugQueueItem>(_items);
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsDebugWindowEnabled;

    /// <inheritdoc />
    public string? CurrentPageKey
    {
        get => _currentPageKey;
        private set
        {
            if (string.Equals(_currentPageKey, value, StringComparison.Ordinal))
            {
                return;
            }

            _currentPageKey = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPageKey)));
        }
    }

    /// <inheritdoc />
    public ReadOnlyObservableCollection<TutorialDebugQueueItem> QueueItems { get; }

    /// <inheritdoc />
    public event EventHandler<TutorialDebugJumpRequestedEventArgs>? JumpRequested;

    /// <summary>当调试状态属性变化时发生。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public void SetCurrentQueue(FrameworkElement owner, string pageKey)
        => SetCurrentQueue(owner, pageKey, _sequenceRegistry.GetSequenceDefinition(pageKey).PackageIds);

    /// <inheritdoc />
    public void SetCurrentQueue(FrameworkElement owner, string queueKey, IReadOnlyList<string> packageIds)
    {
        if (!IsEnabled)
        {
            return;
        }

        _owner = owner;
        CurrentPageKey = queueKey;
        _currentPackageIds = packageIds.ToArray();
        RunOnOwnerDispatcher(() =>
        {
            _items.Clear();
            foreach (var packageId in packageIds)
            {
                _items.Add(new TutorialDebugQueueItem(packageId));
            }

            ShowWindow(owner);
        });
        _ = RefreshAsync();
    }

    /// <inheritdoc />
    public async Task<bool> RequestJumpAsync(string packageId, bool persist, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || _owner is null || string.IsNullOrWhiteSpace(CurrentPageKey))
        {
            return false;
        }

        var packageIds = _currentPackageIds;
        var selectedIndex = Array.IndexOf(packageIds.ToArray(), packageId);
        if (selectedIndex < 0)
        {
            return false;
        }

        var nextStates = packageIds.Select((id, index) => new KeyValuePair<string, bool>(id, index < selectedIndex)).ToArray();
        lock (_syncRoot)
        {
            foreach (var pair in nextStates)
            {
                _sessionOverrides[pair.Key] = pair.Value;
            }
            _restartRequested = true;
        }

        if (persist)
        {
            var state = await _stateStore.LoadAsync(cancellationToken);
            foreach (var pair in nextStates)
            {
                var package = _packageRegistry.GetPackage(pair.Key);
                if (package == null)
                {
                    continue;
                }

                if (pair.Value)
                {
                    state.CompletedPackages[pair.Key] = new TutorialCompletionRecord
                    {
                        Version = package.Version,
                        CompletionKind = TutorialCompletionKind.Completed
                    };
                }
                else
                {
                    state.CompletedPackages.Remove(pair.Key);
                }
            }

            await _stateStore.SaveAsync(state, cancellationToken);
        }

        await RefreshAsync(cancellationToken);
        JumpRequested?.Invoke(this, new TutorialDebugJumpRequestedEventArgs(_owner, CurrentPageKey));
        return true;
    }

    /// <inheritdoc />
    public bool IsPackageCompleted(TutorialState state, TutorialPackageDefinition package)
    {
        lock (_syncRoot)
        {
            if (_sessionOverrides.TryGetValue(package.PackageId, out var overridden))
            {
                return overridden;
            }
        }

        return state.CompletedPackages.TryGetValue(package.PackageId, out var record)
            && record.Version >= package.Version;
    }

    /// <inheritdoc />
    public bool ConsumeRestart(FrameworkElement owner, string pageKey)
    {
        lock (_syncRoot)
        {
            if (!_restartRequested || !ReferenceEquals(_owner, owner) || !string.Equals(CurrentPageKey, pageKey, StringComparison.Ordinal))
            {
                return false;
            }

            _restartRequested = false;
            return true;
        }
    }

    /// <inheritdoc />
    public void OnAutoRunRequested(string ownerType, string pageKey, string reason) { }

    /// <inheritdoc />
    public void OnAutoRunCompleted(string ownerType, string pageKey, TutorialRunResult result) => _ = RefreshAsync();

    /// <inheritdoc />
    public void OnPackageRunRequested(string packageId, string pageKey, TutorialTriggerMode triggerMode) { }

    /// <inheritdoc />
    public void OnPackageStarted(string packageId, string pageKey, TutorialTriggerMode triggerMode)
    {
        _currentPackageId = packageId;
        _ = RefreshAsync();
    }

    /// <inheritdoc />
    public void OnStepShown(string packageId, string? targetName, string title) { }

    /// <inheritdoc />
    public void OnPackageCompleted(string packageId, TutorialRunResult result)
    {
        if (result == TutorialRunResult.Completed)
        {
            lock (_syncRoot)
            {
                _sessionOverrides[packageId] = true;
            }
        }

        _currentPackageId = null;
        _ = RefreshAsync();
    }

    /// <inheritdoc />
    public void OnPackageNotPending(string pageKey) => _ = RefreshAsync();

    /// <inheritdoc />
    public void OnPackageSkippedByState(string packageId, TutorialCompletionKind completionKind, int recordedVersion, int currentVersion) { }

    /// <inheritdoc />
    public void OnPackageNotReady(string packageId, string pageKey) => _ = RefreshAsync();

    /// <inheritdoc />
    public void OnSequenceResolved(string pageKey, IReadOnlyList<string> packageIds) { }

    /// <inheritdoc />
    public void OnPackageTargetMissing(string packageId) => _ = RefreshAsync();

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || CurrentPageKey is null)
        {
            return;
        }

        var state = await _stateStore.LoadAsync(cancellationToken);
        RunOnOwnerDispatcher(() =>
        {
            foreach (var item in _items)
            {
                var package = _packageRegistry.GetPackage(item.PackageId);
                item.IsCompleted = package != null && IsPackageCompleted(state, package);
                item.IsCurrent = string.Equals(item.PackageId, _currentPackageId, StringComparison.Ordinal);
            }
        });
    }

    private void ShowWindow(FrameworkElement owner)
    {
        if (_window is { IsVisible: true })
        {
            return;
        }

        _window = new TutorialDebugWindow(this);
        _window.Closed += (_, _) => _window = null;
        var ownerWindow = owner as Window ?? Window.GetWindow(owner);
        if (ownerWindow != null)
        {
            _window.Owner = ownerWindow;
            _window.Left = ownerWindow.Left + ownerWindow.ActualWidth + 12;
            _window.Top = ownerWindow.Top;
        }

        _window.Show();
    }

    private void RunOnOwnerDispatcher(Action action)
    {
        if (_owner == null)
        {
            return;
        }

        if (_owner.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _owner.Dispatcher.BeginInvoke(action);
        }
    }
}

/// <summary>未启用调试功能时使用的空实现。</summary>
internal sealed class NoOpTutorialDebugService : ITutorialDebugService
{
    /// <summary>获取共享空实例。</summary>
    public static NoOpTutorialDebugService Instance { get; } = new();
    public bool IsEnabled => false;
    public string? CurrentPageKey => null;
    public ReadOnlyObservableCollection<TutorialDebugQueueItem> QueueItems { get; } = new(new ObservableCollection<TutorialDebugQueueItem>());
    public event EventHandler<TutorialDebugJumpRequestedEventArgs>? JumpRequested { add { } remove { } }
    public void SetCurrentQueue(FrameworkElement owner, string pageKey) { }
    public void SetCurrentQueue(FrameworkElement owner, string queueKey, IReadOnlyList<string> packageIds) { }
    public Task<bool> RequestJumpAsync(string packageId, bool persist, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public bool IsPackageCompleted(TutorialState state, TutorialPackageDefinition package) => state.CompletedPackages.TryGetValue(package.PackageId, out var record) && record.Version >= package.Version;
    public bool ConsumeRestart(FrameworkElement owner, string pageKey) => false;
    public void OnAutoRunRequested(string ownerType, string pageKey, string reason) { }
    public void OnAutoRunCompleted(string ownerType, string pageKey, TutorialRunResult result) { }
    public void OnPackageRunRequested(string packageId, string pageKey, TutorialTriggerMode triggerMode) { }
    public void OnPackageStarted(string packageId, string pageKey, TutorialTriggerMode triggerMode) { }
    public void OnStepShown(string packageId, string? targetName, string title) { }
    public void OnPackageCompleted(string packageId, TutorialRunResult result) { }
    public void OnPackageNotPending(string pageKey) { }
    public void OnPackageSkippedByState(string packageId, TutorialCompletionKind completionKind, int recordedVersion, int currentVersion) { }
    public void OnPackageNotReady(string packageId, string pageKey) { }
    public void OnSequenceResolved(string pageKey, IReadOnlyList<string> packageIds) { }
    public void OnPackageTargetMissing(string packageId) { }
}

/// <summary>产品导览调试窗口。</summary>
internal sealed class TutorialDebugWindow : UiFluentWindow
{
    private readonly ITutorialDebugService _service;
    private readonly CheckBox _persistCheckBox;

    public TutorialDebugWindow(ITutorialDebugService service)
    {
        _service = service;
        Title = "Product Tour Debug";
        Width = 380;
        Height = 440;
        MinWidth = 300;
        MinHeight = 260;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var titleBar = new UiTitleBar
        {
            Title = Title,
            ShowMaximize = false,
            ShowMinimize = false,
            ShowHelp = false
        };

        var pageKey = new TextBlock { Margin = new Thickness(12, 8, 12, 6), TextWrapping = TextWrapping.Wrap };
        pageKey.SetBinding(TextBlock.TextProperty, new Binding(nameof(ITutorialDebugService.CurrentPageKey)) { Source = service, StringFormat = "Queue: {0}" });

        _persistCheckBox = new CheckBox
        {
            Margin = new Thickness(12, 0, 12, 12),
            Content = "Persist jump state"
        };

        var listView = new UiListView { Margin = new Thickness(12, 0, 12, 8), ItemsSource = service.QueueItems };
        listView.ItemTemplate = CreateItemTemplate();
        listView.MouseDoubleClick += async (_, _) =>
        {
            if (listView.SelectedItem is TutorialDebugQueueItem item)
            {
                await _service.RequestJumpAsync(item.PackageId, _persistCheckBox.IsChecked == true);
            }
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Insert(1, new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(titleBar, 0);
        Grid.SetRow(pageKey, 1);
        Grid.SetRow(listView, 2);
        Grid.SetRow(_persistCheckBox, 3);
        root.Children.Add(titleBar);
        root.Children.Add(pageKey);
        root.Children.Add(listView);
        root.Children.Add(_persistCheckBox);
        Content = root;
    }

    private static DataTemplate CreateItemTemplate()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(TutorialDebugQueueItem.PackageId)));
        text.SetBinding(TextBlock.FontWeightProperty, new Binding(nameof(TutorialDebugQueueItem.IsCurrent))
        {
            Converter = new BooleanToFontWeightConverter()
        });
        text.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(TutorialDebugQueueItem.IsCompleted))
        {
            Converter = new BooleanToBrushConverter()
        });
        return new DataTemplate { VisualTree = text };
    }
}

internal sealed class BooleanToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is true ? FontWeights.Bold : FontWeights.Normal;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
}

internal sealed class BooleanToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is true ? Brushes.ForestGreen : Brushes.Gray;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => Binding.DoNothing;
}
