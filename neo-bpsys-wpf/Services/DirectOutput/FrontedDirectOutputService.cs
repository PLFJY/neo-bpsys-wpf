using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Services.DirectOutput;

/// <summary>单 Sender 实验入口。WPF 留在应用 Dispatcher，上传和 Spout 同步在串行工作任务中完成。</summary>
internal sealed class FrontedDirectOutputService : ObservableObject, IDisposable, IRecipient<FrontedLayoutPackagesChangedMessage>
{
    private readonly IFrontedLayoutService _layouts;
    private readonly IFrontedRenderer _renderer;
    private readonly ISharedDataService _data;
    private readonly FrontedBehaviorRuntimeHostManager _behaviors;
    private readonly IFrontedWindowRegistry _registry;
    private readonly IFrontedEventBus _events;
    private readonly ILogger<FrontedDirectOutputService> _logger;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cancellation;
    private Task _loop = Task.CompletedTask;
    private FrontedOffscreenRenderHost? _host;
    private bool _boDirty;
    private bool _disposed;
    private bool _isOutputRunning;
    private bool _isBusy;
    private string _selectedWindowId = "BpWindow";
    private string _metrics = string.Empty;
    private string _errorMessage = string.Empty;
    private string _senderName = "neo-bpsys-wpf/BpWindow";
    private string? _outputWindowId;

    /// <summary>初始化实验输出服务，不会自动开始输出。</summary>
    /// <param name="layouts">活动布局服务。</param>
    /// <param name="renderer">v3 渲染器。</param>
    /// <param name="data">真实共享状态。</param>
    /// <param name="behaviors">行为管理器。</param>
    /// <param name="registry">窗口注册表。</param>
    /// <param name="events">现有前台语义事件总线。</param>
    /// <param name="logger">诊断日志。</param>
    public FrontedDirectOutputService(IFrontedLayoutService layouts, IFrontedRenderer renderer,
        ISharedDataService data, FrontedBehaviorRuntimeHostManager behaviors,
        IFrontedWindowRegistry registry, IFrontedEventBus events, ILogger<FrontedDirectOutputService> logger)
    {
        _layouts = layouts;
        _renderer = renderer;
        _data = data;
        _behaviors = behaviors;
        _registry = registry;
        _events = events;
        _logger = logger;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        StartCommand = new AsyncRelayCommand(() => StartAsync(SelectedWindowId), () => !IsBusy && !IsOutputRunning);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsBusy || IsOutputRunning);
        WeakReferenceMessenger.Default.Register(this);
    }

    /// <summary>获取可自然复用的 v3 窗口注册。</summary>
    public IReadOnlyList<FrontedWindowRegistration> Windows => _registry.GetManageableWindows()
        .Where(window => window.Kind == FrontedWindowRegistrationKind.V3Layout).ToArray();

    /// <summary>获取或设置下次开始输出的窗口 Canonical ID。</summary>
    public string SelectedWindowId { get => _selectedWindowId; set => SetProperty(ref _selectedWindowId, value); }

    /// <summary>获取输出生命周期状态，不依赖 Window.IsVisible。</summary>
    public bool IsOutputRunning { get => _isOutputRunning; private set { SetProperty(ref _isOutputRunning, value); NotifyCommands(); } }

    /// <summary>获取开始或停止操作是否尚未结束。</summary>
    public bool IsBusy { get => _isBusy; private set { SetProperty(ref _isBusy, value); NotifyCommands(); } }

    /// <summary>获取稳定的 Spout Sender 名称。</summary>
    public string SenderName { get => _senderName; private set => SetProperty(ref _senderName, value); }

    /// <summary>获取最近统计周期的分辨率、成功 Send 调用 FPS、平均 WPF 生成及全帧耗时；不代表 OBS 展示帧率。</summary>
    public string Metrics { get => _metrics; private set => SetProperty(ref _metrics, value); }

    /// <summary>获取启动、渲染或发送失败的具体诊断。</summary>
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    /// <summary>获取开始命令。</summary>
    public IAsyncRelayCommand StartCommand { get; }

    /// <summary>获取停止命令。</summary>
    public IAsyncRelayCommand StopCommand { get; }

    /// <summary>加载一个布局并开始 30 FPS 输出；失败信息保留供界面检查。</summary>
    /// <param name="windowId">已注册的 v3 布局 Canonical ID。</param>
    /// <returns>启动操作完成的任务。</returns>
    public async Task StartAsync(string windowId)
    {
        _dispatcher.VerifyAccess();
        await _gate.WaitAsync();
        SpoutFrameSender? sender = null;
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsOutputRunning) return;
            IsBusy = true;
            ErrorMessage = Metrics = string.Empty;
            if (!_registry.TryGet(windowId, out var registration) || registration.Kind != FrontedWindowRegistrationKind.V3Layout)
                throw new InvalidOperationException("Direct Output requires a registered v3 layout window.");
            _outputWindowId = windowId;
            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;
            var config = await _layouts.LoadWindowConfigAsync(windowId, token);
            token.ThrowIfCancellationRequested();
            _host = new FrontedOffscreenRenderHost(windowId, config, _renderer, _data, _behaviors);
            await _host.LoadAsync(config, token);
            var host = _host;
            SenderName = $"neo-bpsys-wpf/{windowId}";
            sender = await Task.Run(() => new SpoutFrameSender(SenderName, host.Width, host.Height), token);
            token.ThrowIfCancellationRequested();
            _boDirty = false;
            _data.IsBo3ModeChanged += OnBoModeChanged;
            IsOutputRunning = true;
            PublishLifecycle("WindowShown", windowId, host.InstanceId);
            var ownedSender = sender;
            _loop = Task.Run(() => RunAsync(windowId, host, ownedSender, token));
            sender = null; // The worker owns and disposes GPU resources, including on failure/shutdown.
        }
        catch (OperationCanceledException) { CleanupHost(); }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "Direct Output start failed for {WindowId}", windowId);
            CleanupHost();
        }
        finally
        {
            if (sender is not null) await Task.Run(sender.Dispose);
            IsBusy = false;
            _gate.Release();
        }
    }

    /// <summary>取消调度，等待唯一在途帧，分离行为并释放 WPF 与 GPU 资源。</summary>
    /// <returns>完整停止后的任务。</returns>
    public async Task StopAsync()
    {
        _dispatcher.VerifyAccess();
        _cancellation?.Cancel();
        await _gate.WaitAsync();
        try
        {
            IsBusy = true;
            await _loop;
            CleanupHost();
        }
        finally { IsBusy = false; _gate.Release(); }
    }

    private async Task RunAsync(string windowId, FrontedOffscreenRenderHost host, SpoutFrameSender sender, CancellationToken token)
    {
        Exception? failure = null;
        try
        {
            using (sender)
            using (var timer = new PeriodicTimer(TimeSpan.FromSeconds(1D / 30)))
            {
                var pixels = new byte[checked(host.Width * host.Height * 4)];
                var period = Stopwatch.StartNew();
                double renderMs = 0, frameMs = 0;
                var frames = 0;
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    var frame = Stopwatch.StartNew();
                    var renderTime = await _dispatcher.InvokeAsync(async () =>
                    {
                        token.ThrowIfCancellationRequested();
                        if (_boDirty)
                        {
                            _boDirty = false;
                            var config = await _layouts.LoadWindowConfigAsync(windowId, token);
                            await host.LoadAsync(config, token);
                        }
                        token.ThrowIfCancellationRequested();
                        var render = Stopwatch.StartNew();
                        host.CopyFrame(pixels);
                        return render.Elapsed.TotalMilliseconds;
                    }, DispatcherPriority.Background, token).Task.Unwrap().ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    sender.Send(pixels);
                    frames++;
                    renderMs += renderTime;
                    frameMs += frame.Elapsed.TotalMilliseconds;
                    if (period.Elapsed.TotalSeconds < 1) continue;
                    var metrics = FormattableString.Invariant($"{host.Width} × {host.Height} | {frames / period.Elapsed.TotalSeconds:F1} send FPS | WPF {renderMs / frames:F2} ms | frame {frameMs / frames:F2} ms");
                    _logger.LogInformation("Direct Output {Sender}: {Metrics}", SenderName, metrics);
                    await _dispatcher.InvokeAsync(() => Metrics = metrics, DispatcherPriority.Background, token);
                    frames = 0;
                    renderMs = frameMs = 0;
                    period.Restart();
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { failure = ex; _logger.LogError(ex, "Direct Output failed for {WindowId}", windowId); }
        finally
        {
            if (!_dispatcher.HasShutdownStarted)
                await _dispatcher.InvokeAsync(() =>
                {
                    if (failure is not null) ErrorMessage = failure.Message;
                    CleanupHost();
                });
        }
    }

    private void OnBoModeChanged(object? sender, EventArgs e) => _dispatcher.InvokeAsync(() => _boDirty = true);

    private void NotifyCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private void CleanupHost()
    {
        _data.IsBo3ModeChanged -= OnBoModeChanged;
        if (IsOutputRunning && _host is not null && _outputWindowId is not null)
            PublishLifecycle("WindowHidden", _outputWindowId, _host.InstanceId);
        _host?.Dispose();
        _host = null;
        _cancellation?.Dispose();
        _cancellation = null;
        IsOutputRunning = false;
    }

    private void PublishLifecycle(string eventType, string windowType, string instanceId) => _events.Publish(new()
    {
        EventType = eventType,
        WindowId = instanceId,
        WindowType = windowType,
        CanvasName = FrontedLayoutConstants.BaseCanvasName,
        Source = "DirectOutput",
        Timestamp = DateTimeOffset.UtcNow
    });

    /// <summary>活动包改变时完整重建输出会话，载入新尺寸、资源和行为。</summary>
    /// <param name="message">包变更消息。</param>
    public void Receive(FrontedLayoutPackagesChangedMessage message)
    {
        if (message.ActivePackageId is null) return;
        _dispatcher.InvokeAsync(async () =>
        {
            try
            {
                if (!IsOutputRunning && !IsBusy) return;
                var windowId = _outputWindowId;
                await StopAsync();
                OnPropertyChanged(nameof(Windows));
                if (!_disposed && windowId is not null && _registry.TryGet(windowId, out var registration)
                    && registration.Kind == FrontedWindowRegistrationKind.V3Layout)
                    await StartAsync(windowId);
                else
                    ErrorMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "DirectOutputPackageChanged");
            }
            catch (Exception ex) { ErrorMessage = ex.Message; _logger.LogError(ex, "Direct Output package stop failed"); }
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _cancellation?.Cancel();
        // Do not synchronously wait for a worker which may be awaiting this Dispatcher during application exit.
        CleanupHost();
    }
}
