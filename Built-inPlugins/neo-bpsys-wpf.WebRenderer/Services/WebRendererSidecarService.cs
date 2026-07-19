using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.WebRenderer.Protocol;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>管理 sidecar 进程、可靠命名管道会话及已确认的 Web bootstrap。</summary>
public sealed class WebRendererSidecarService : IHostedService, IDisposable, IRecipient<FrontedLayoutPackagesChangedMessage>
{
    private static readonly TimeSpan BootstrapAckTimeout = TimeSpan.FromSeconds(30);
    private WebRendererLaunchOptions _options;
    private readonly WebRendererRuntimeDetector _runtimeDetector;
    private readonly WebRendererPlugin _plugin;
    private readonly ISnackbarService _snackbarService;
    private readonly ILogger<WebRendererSidecarService> _logger;
    private readonly WebRendererBootstrapBuilder? _bootstrapBuilder;
    private readonly WebRendererRuntimeStatePublisher? _runtimePublisher;
    private readonly IWebTransitionGateway? _transitionGateway;
    private readonly WebRendererLifecycleOperationCoordinator? _lifecycleCoordinator;
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly SemaphoreSlim _bootstrapLock = new(1, 1);
    private readonly object _gate = new();
    private Process? _process;
    private Task? _acceptLoop;
    private CancellationTokenSource? _acceptCancellation;
    private WebRendererSidecarJob? _sidecarJob;
    private Channel<WebRendererOutbound>? _outbound;
    private CancellationTokenSource? _sessionCancellation;
    private TaskCompletionSource<WebRendererBootstrapApplied>? _bootstrapAck;
    private long _sequence;
    private long _lastInboundSequence;
    private long _bootstrapGeneration;
    private WebRendererBootstrapSnapshot? _confirmedSnapshot;
    private WebRendererLifecycleState _state = WebRendererLifecycleState.Stopped;
    private string? _lastError;
    private bool _manualStopped;
    private bool _closingSession;

    /// <summary>服务权威状态改变时发生。</summary>
    public event EventHandler? StatusChanged;

    /// <summary>获取当前是否有互斥生命周期操作在运行。</summary>
    public bool IsLifecycleOperationRunning => _lifecycleCoordinator?.IsLifecycleOperationRunning ?? false;

    /// <summary>获取当前生命周期操作名称。</summary>
    public string? CurrentLifecycleOperation => _lifecycleCoordinator?.CurrentOperation;

    /// <summary>获取仅含 sidecar 已确认 bootstrap 的当前服务状态。</summary>
    public WebRendererServiceStatus Status
    {
        get
        {
            lock (_gate) return new(_process is { HasExited: false }, _process?.Id, _options.Address, _options.Port,
                _runtimePublisher?.ClientCount ?? 0, _bootstrapGeneration, _lastError, _options.LogProtocol,
                _confirmedSnapshot?.ActivePackageId, _confirmedSnapshot?.Windows.Select(item => item.FullWindowType).ToArray() ?? [], _state);
        }
    }

    /// <summary>获取已被 sidecar 应用确认的窗口摘要。</summary>
    public IReadOnlyList<WebRendererPublishedWindow> GetPublishedWindows()
    {
        lock (_gate) return _confirmedSnapshot?.Windows.Select(window => new WebRendererPublishedWindow(window.FullWindowType,
            window.Layout is not null, window.Layout?.CanvasSettings.CanvasWidth, window.Layout?.CanvasSettings.CanvasHeight,
            window.Diagnostics)).ToArray() ?? [];
    }

    /// <summary>获取当前会话是否已确认 bootstrap。</summary>
    public bool HasBootstrapSnapshot { get { lock (_gate) return _state == WebRendererLifecycleState.Ready && _confirmedSnapshot is not null; } }

    /// <summary>初始化 sidecar 服务。</summary>
    public WebRendererSidecarService(WebRendererLaunchOptions options, WebRendererRuntimeDetector runtimeDetector,
        WebRendererPlugin plugin, ISnackbarService snackbarService, ILogger<WebRendererSidecarService> logger,
        WebRendererBootstrapBuilder? bootstrapBuilder = null, WebRendererRuntimeStatePublisher? runtimePublisher = null,
        IWebTransitionGateway? transitionGateway = null, WebRendererLifecycleOperationCoordinator? lifecycleCoordinator = null)
    {
        _options = options; _runtimeDetector = runtimeDetector; _plugin = plugin; _snackbarService = snackbarService; _logger = logger;
        _bootstrapBuilder = bootstrapBuilder; _runtimePublisher = runtimePublisher; _transitionGateway = transitionGateway;
        _lifecycleCoordinator = lifecycleCoordinator;
        if (_runtimePublisher is not null) { _runtimePublisher.Updated += OnRuntimeUpdated; _runtimePublisher.BehaviorEventPublished += OnBehaviorEventPublished; }
        if (_transitionGateway is WebTransitionGateway gateway) gateway.SignalPublished += OnTransitionSignalPublished;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Register(this);
        if (_options.NoStart) { SetState(WebRendererLifecycleState.Stopped, null); return; }
        if (_options.ValidationError is not null) { SetState(WebRendererLifecycleState.Faulted, _options.ValidationError); return; }
        await StartRendererAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        await StopCoreAsync(cancellationToken);
    }

    /// <summary>启动 sidecar；可由管理页重复调用。</summary>
    public Task StartRendererAsync(CancellationToken cancellationToken = default) => RunLifecycleAsync("Start", StartCoreAsync, cancellationToken);

    /// <summary>停止 sidecar，但不停止主程序 HostedService。</summary>
    public Task StopRendererAsync(CancellationToken cancellationToken = default) => RunLifecycleAsync("Stop", StopCoreAsync, cancellationToken);

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        _manualStopped = true;
        _closingSession = true;
        SetState(WebRendererLifecycleState.Stopping, null);
        try
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
            await QueueAsync(WebRendererIpcProtocol.Shutdown, new { reason = "host-stopping" }, shutdown.Token, allowWhileClosing: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Web Renderer shutdown IPC was unavailable.");
        }

        CancellationTokenSource? session;
        CancellationTokenSource? accept;
        Process? process;
        WebRendererSidecarJob? job;
        Task? acceptLoop;
        lock (_gate)
        {
            session = _sessionCancellation; _sessionCancellation = null;
            accept = _acceptCancellation; _acceptCancellation = null;
            process = _process; _process = null;
            job = _sidecarJob; _sidecarJob = null;
            acceptLoop = _acceptLoop; _acceptLoop = null;
            _outbound?.Writer.TryComplete(); _outbound = null;
        }

        try
        {
            session?.Cancel();
            accept?.Cancel();
            if (acceptLoop is not null)
            {
                try { await acceptLoop.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None); }
                catch (TimeoutException) { _logger.LogWarning("Web Renderer pipe accept loop did not end within its shutdown grace period."); }
                catch (OperationCanceledException) { }
            }
            if (process is { HasExited: false })
            {
                try { await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None); }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Web Renderer sidecar did not exit gracefully; terminating process tree.");
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
                }
            }
            if (process is not null && !process.HasExited)
                throw new TimeoutException("Web Renderer sidecar did not exit after forced termination.");
            if (!IsPortAvailable(_options.Address, _options.Port))
                throw new TimeoutException("Web Renderer port was not released after stopping sidecar.");
            SetState(WebRendererLifecycleState.Stopped, null);
        }
        finally
        {
            process?.Dispose();
            job?.Dispose();
            session?.Dispose();
            accept?.Dispose();
            _closingSession = false;
        }
    }

    /// <summary>安全地重启 sidecar。</summary>
    public Task RestartRendererAsync(CancellationToken cancellationToken = default) => RunLifecycleAsync("Restart", async token => { await StopCoreAsync(token); await StartCoreAsync(token); }, cancellationToken);

    /// <summary>应用已保存的插件设置。</summary>
    public void ApplySettings(WebRendererPluginSettings settings)
    {
        _options = new WebRendererLaunchOptions(settings.Host, settings.Port, !settings.StartWithApplication, settings.LogProtocol, null)
        { ExitTimeout = TimeSpan.FromMilliseconds(settings.ExitTimeoutMs is > 0 and <= 30000 ? settings.ExitTimeoutMs : 2000), EnterTimeout = TimeSpan.FromMilliseconds(settings.EnterTimeoutMs is > 0 and <= 30000 ? settings.EnterTimeoutMs : 2000) };
        NotifyStatus();
    }

    /// <summary>重新检测 runtime 并尝试启动。</summary>
    public Task RetryAsync(CancellationToken cancellationToken = default) => StartRendererAsync(cancellationToken);

    private Task RunLifecycleAsync(string name, Func<CancellationToken, Task> operation, CancellationToken cancellationToken) =>
        _lifecycleCoordinator is null ? operation(cancellationToken) : _lifecycleCoordinator.RunAsync(name, TimeSpan.FromSeconds(15), operation, cancellationToken);

    private Task StartCoreAsync(CancellationToken cancellationToken) { _manualStopped = false; return TryStartAsync(cancellationToken); }

    private async Task TryStartAsync(CancellationToken cancellationToken)
    {
        if (_stopping.IsCancellationRequested) return;
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_process is { HasExited: false }) return;
            SetState(WebRendererLifecycleState.StartingProcess, null);
            var runtime = await _runtimeDetector.DetectAsync();
            if (!runtime.IsAvailable || runtime.DotnetPath is null) { SetState(WebRendererLifecycleState.Faulted, runtime.ErrorMessage ?? "ASP.NET Core Runtime unavailable."); return; }
            if (!IsPortAvailable(_options.Address, _options.Port)) { SetState(WebRendererLifecycleState.Faulted, $"Web Renderer port {_options.Address}:{_options.Port} is unavailable."); return; }
            var hostPath = Path.Combine(_plugin.Info.PluginFolderPath, "Host", "neo-bpsys-wpf.WebRenderer.Host.dll");
            if (!File.Exists(hostPath)) { SetState(WebRendererLifecycleState.Faulted, $"Web Renderer sidecar file missing: {hostPath}"); return; }
            var pipeName = $"neo-bpsys-wpf.web-renderer.{Environment.ProcessId}.{Guid.NewGuid():N}";
            _closingSession = false;
            _acceptCancellation = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            _acceptLoop = AcceptLoopAsync(pipeName, _acceptCancellation.Token);
            var startInfo = new ProcessStartInfo(runtime.DotnetPath) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(hostPath)! };
            var parentStartTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
            startInfo.ArgumentList.Add(hostPath); startInfo.ArgumentList.Add("--pipe"); startInfo.ArgumentList.Add(pipeName); startInfo.ArgumentList.Add("--parent-pid"); startInfo.ArgumentList.Add(Environment.ProcessId.ToString()); startInfo.ArgumentList.Add("--parent-start-ticks"); startInfo.ArgumentList.Add(parentStartTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)); startInfo.ArgumentList.Add("--address"); startInfo.ArgumentList.Add(_options.Address); startInfo.ArgumentList.Add("--port"); startInfo.ArgumentList.Add(_options.Port.ToString()); startInfo.ArgumentList.Add("--plugin-version"); startInfo.ArgumentList.Add(_plugin.Info.Manifest.Version);
            _sidecarJob = CreateSidecarJob();
            _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Web Renderer sidecar.");
            try { _sidecarJob?.Assign(_process); }
            catch
            {
                if (_process is { HasExited: false }) _process.Kill(true);
                throw;
            }
            _process.EnableRaisingEvents = true; _process.Exited += OnSidecarExited;
            Observe(ObserveOutputAsync(_process.StandardOutput, "stdout", _stopping.Token)); Observe(ObserveOutputAsync(_process.StandardError, "stderr", _stopping.Token));
            _logger.LogInformation("Web Renderer sidecar process started at http://{Address}:{Port}", _options.Address, _options.Port);
            SetState(WebRendererLifecycleState.WaitingForPipe, null);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested) { SetState(WebRendererLifecycleState.Faulted, ex.Message); _logger.LogError(ex, "Web Renderer sidecar start failed."); }
        finally { _startLock.Release(); }
    }

    private async Task AcceptLoopAsync(string pipeName, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !_manualStopped)
        {
            await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            try { await pipe.WaitForConnectionAsync(cancellationToken); await HandleSessionAsync(pipe, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception ex) { SetState(WebRendererLifecycleState.Faulted, $"IPC session failed: {ex.Message}"); _logger.LogWarning(ex, "Web Renderer IPC session ended."); await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken); }
        }
    }

    private async Task HandleSessionAsync(Stream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, leaveOpen: true);
        var readyLine = await reader.ReadLineAsync(cancellationToken);
        var ready = readyLine is null ? null : JsonSerializer.Deserialize<WebRendererIpcMessage>(readyLine);
        if (ready is null || ready.ProtocolVersion != WebRendererIpcProtocol.Version || ready.Type != WebRendererIpcProtocol.SidecarReady) throw new InvalidDataException("Expected sidecar.ready as the first IPC message.");
        _lastInboundSequence = ready.Sequence;
        var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var outbound = Channel.CreateUnbounded<WebRendererOutbound>(new UnboundedChannelOptions { SingleReader = true });
        lock (_gate) { _sessionCancellation?.Cancel(); _sessionCancellation = session; _outbound = outbound; _bootstrapAck = null; }
        SetState(WebRendererLifecycleState.PipeConnected, null);
        _logger.LogInformation("Web Renderer pipe connected; sidecar.ready received.");
        var writerTask = Task.Run(() => WriteLoopAsync(pipe, outbound.Reader, session.Token), session.Token);
        var readTask = Task.Run(() => ReadLoopAsync(reader, session.Token), session.Token);
        await QueueAsync(WebRendererIpcProtocol.HostHello, new { hostVersion = AppConstants.AppVersion, pluginVersion = _plugin.Info.Manifest.Version }, session.Token);
        _logger.LogInformation("Web Renderer host.hello sent.");
        await RefreshBootstrapAsync(session.Token);
        await readTask;
        session.Cancel(); outbound.Writer.TryComplete();
        try { await writerTask; } catch (OperationCanceledException) { }
        lock (_gate) if (ReferenceEquals(_outbound, outbound)) { _outbound = null; _sessionCancellation = null; }
        if (!_manualStopped && !_stopping.IsCancellationRequested) SetState(WebRendererLifecycleState.WaitingForPipe, "IPC disconnected; waiting for sidecar reconnect.");
    }

    private async Task ReadLoopAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var message = JsonSerializer.Deserialize<WebRendererIpcMessage>(line);
            if (message is null || message.ProtocolVersion != WebRendererIpcProtocol.Version || message.Sequence <= Interlocked.Read(ref _lastInboundSequence)) continue;
            Interlocked.Exchange(ref _lastInboundSequence, message.Sequence);
            if (message.Type == WebRendererIpcProtocol.BootstrapApplied)
            {
                var applied = message.Payload.Deserialize<WebRendererBootstrapApplied>();
                if (applied is not null) _bootstrapAck?.TrySetResult(applied);
            }
            else if (message.Type == WebRendererIpcProtocol.BootstrapRejected)
            {
                var failure = message.Payload.Deserialize<WebRendererBootstrapFailure>();
                _bootstrapAck?.TrySetException(new InvalidOperationException(failure?.Message ?? "Sidecar rejected bootstrap."));
            }
            else if (message.Type == WebRendererIpcProtocol.SidecarClientsChanged && message.Payload.TryGetProperty("count", out var count) && count.TryGetInt32(out var clientCount)) _runtimePublisher?.SetClientCount(clientCount);
            else if ((message.Type == WebRendererIpcProtocol.TransitionExitCompleted || message.Type == WebRendererIpcProtocol.TransitionEnterCompleted) && message.Payload.TryGetProperty("correlationId", out var correlation)) _transitionGateway?.Acknowledge(correlation.GetString() ?? string.Empty, message.Type == WebRendererIpcProtocol.TransitionEnterCompleted);
        }
    }

    private async Task RefreshBootstrapAsync(CancellationToken cancellationToken)
    {
        if (_bootstrapBuilder is null) return;
        lock (_gate) if (_outbound is null) return;
        await _bootstrapLock.WaitAsync(cancellationToken);
        try
        {
            SetState(WebRendererLifecycleState.BuildingBootstrap, null);
            var generation = Interlocked.Increment(ref _bootstrapGeneration);
            WebRendererBootstrapSnapshot snapshot;
            try { snapshot = await _bootstrapBuilder.BuildAsync(generation, cancellationToken); }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                var failure = new WebRendererBootstrapFailure(generation, "BootstrapBuildFailed", ex.Message);
                await QueueAsync(WebRendererIpcProtocol.BootstrapFailed, failure, cancellationToken); SetState(WebRendererLifecycleState.Faulted, failure.Message); return;
            }
            var completion = new TaskCompletionSource<WebRendererBootstrapApplied>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate) _bootstrapAck = completion;
            SetState(WebRendererLifecycleState.WaitingForBootstrapAck, null);
            await QueueAsync(WebRendererIpcProtocol.BootstrapReplace, snapshot, cancellationToken);
            _logger.LogInformation("Web Renderer bootstrap.replace sent for generation {Generation}.", generation);
            WebRendererBootstrapApplied applied;
            try { applied = await completion.Task.WaitAsync(BootstrapAckTimeout, cancellationToken); }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested) { SetState(WebRendererLifecycleState.Faulted, $"Bootstrap acknowledgement failed: {ex.Message}"); return; }
            if (applied.Generation != generation || !string.Equals(applied.ActivePackageId, snapshot.ActivePackageId, StringComparison.Ordinal)) { SetState(WebRendererLifecycleState.Faulted, "Bootstrap acknowledgement does not match the current generation."); return; }
            lock (_gate) _confirmedSnapshot = snapshot;
            _transitionGateway?.UpdateGeneration(snapshot.Generation); _runtimePublisher?.ReplaceLayout(snapshot); _runtimePublisher?.PublishConfirmedSnapshot();
            SetState(WebRendererLifecycleState.Ready, null);
            _logger.LogInformation("Web Renderer bootstrap.applied received; Web Renderer Ready ({Generation}, {WindowCount} windows).", generation, applied.WindowCount);
        }
        finally { _bootstrapLock.Release(); }
    }

    /// <summary>响应活动包切换或设计器保存，重新发布完整真实布局。</summary>
    public void Receive(FrontedLayoutPackagesChangedMessage message) => Observe(RefreshBootstrapAsync(_stopping.Token));

    private async Task QueueAsync(string type, object payload, CancellationToken cancellationToken, bool allowWhileClosing = false)
    {
        if (_closingSession && !allowWhileClosing) return;
        Channel<WebRendererOutbound>? channel; lock (_gate) channel = _outbound;
        if (channel is null) return;
        await channel.Writer.WriteAsync(new WebRendererOutbound(type, payload), cancellationToken);
    }

    private async Task WriteLoopAsync(Stream pipe, ChannelReader<WebRendererOutbound> reader, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            var message = new WebRendererIpcMessage { ProtocolVersion = WebRendererIpcProtocol.Version, Sequence = Interlocked.Increment(ref _sequence), Type = item.Type, Payload = JsonSerializer.SerializeToElement(item.Payload) };
            await writer.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
        }
    }

    private void OnRuntimeUpdated(object? sender, WebRendererRuntimeUpdate update)
    {
        if (Status.LifecycleState != WebRendererLifecycleState.Ready || update.Generation != Status.BootstrapGeneration) return;
        Observe(QueueAsync(update.IsSnapshot ? WebRendererIpcProtocol.RuntimeSnapshot : WebRendererIpcProtocol.RuntimeBindingPatch, update, _stopping.Token));
    }
    private void OnBehaviorEventPublished(object? sender, WebRendererBehaviorEvent value) { if (Status.LifecycleState == WebRendererLifecycleState.Ready) Observe(QueueAsync(WebRendererIpcProtocol.BehaviorEvent, value, _stopping.Token)); }
    private void OnTransitionSignalPublished(object? sender, WebTransitionSignal signal) { if (Status.LifecycleState == WebRendererLifecycleState.Ready) Observe(QueueAsync(signal.Type, new { correlationId = signal.Session.CorrelationId, generation = signal.Session.Generation, requests = signal.Session.Requests, reason = signal.Reason }, _stopping.Token)); }

    private void SetState(WebRendererLifecycleState state, string? error)
    {
        lock (_gate) { _state = state; _lastError = error; }
        Observe(QueueAsync(WebRendererIpcProtocol.SessionState, new WebRendererSessionState(state, Interlocked.Read(ref _bootstrapGeneration), error is null ? null : "IpcOrBootstrapError", error), _stopping.Token));
        NotifyStatus();
    }
    private void NotifyStatus() => StatusChanged?.Invoke(this, EventArgs.Empty);
    private void Observe(Task task) => _ = task.ContinueWith(completed => _logger.LogWarning(completed.Exception, "Web Renderer background operation failed."), TaskContinuationOptions.OnlyOnFaulted);
    private async Task ObserveOutputAsync(StreamReader reader, string stream, CancellationToken cancellationToken) { try { while (await reader.ReadLineAsync(cancellationToken) is { } line) { if (stream == "stderr") lock (_gate) _lastError = line.Length > 2000 ? line[..2000] : line; _logger.LogInformation("Web Renderer sidecar {Stream}: {Line}", stream, line); } } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { } }
    private void OnSidecarExited(object? sender, EventArgs args)
    {
        if (_manualStopped || _stopping.IsCancellationRequested) return;
        CancellationTokenSource? accept = null;
        WebRendererSidecarJob? job = null;
        Process? process = null;
        lock (_gate)
        {
            if (!ReferenceEquals(sender, _process)) return;
            process = _process; _process = null;
            job = _sidecarJob; _sidecarJob = null;
            accept = _acceptCancellation; _acceptCancellation = null;
            _outbound?.Writer.TryComplete(); _outbound = null;
            _sessionCancellation?.Cancel(); _sessionCancellation = null;
        }
        accept?.Cancel();
        accept?.Dispose();
        job?.Dispose();
        process?.Dispose();
        SetState(WebRendererLifecycleState.Faulted, "Web Renderer sidecar exited unexpectedly.");
        Observe(RestartAfterDelayAsync());
    }

    private async Task RestartAfterDelayAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), _stopping.Token);
            await RunLifecycleAsync("Recovery", StartCoreAsync, _stopping.Token);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
    }
    private static bool IsPortAvailable(string address, int port) { if (!IPAddress.TryParse(address, out var ip)) return false; try { using var listener = new TcpListener(ip, port); listener.Start(); return true; } catch (SocketException) { return false; } }
    /// <inheritdoc />
    private WebRendererSidecarJob? CreateSidecarJob()
    {
        try { return new WebRendererSidecarJob(); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(ex, "Web Renderer Job Object is unavailable; parent-PID monitor is active as fallback.");
            return null;
        }
    }
    /// <inheritdoc />
    public void Dispose() { WeakReferenceMessenger.Default.UnregisterAll(this); if (_runtimePublisher is not null) { _runtimePublisher.Updated -= OnRuntimeUpdated; _runtimePublisher.BehaviorEventPublished -= OnBehaviorEventPublished; } if (_transitionGateway is WebTransitionGateway gateway) gateway.SignalPublished -= OnTransitionSignalPublished; _acceptCancellation?.Cancel(); _acceptCancellation?.Dispose(); _sidecarJob?.Dispose(); _stopping.Dispose(); _startLock.Dispose(); _bootstrapLock.Dispose(); _process?.Dispose(); }
    private sealed record WebRendererOutbound(string Type, object Payload);
}

/// <summary>供后台管理页显示的 Web Renderer 权威状态。</summary>
public sealed record WebRendererServiceStatus(bool IsRunning, int? ProcessId, string Address, int Port, int ClientCount,
    long BootstrapGeneration, string? LastError, bool LogProtocol, string? ActivePackageId, IReadOnlyList<string> Windows,
    WebRendererLifecycleState LifecycleState);

/// <summary>管理页使用的已确认 Web 前台窗口摘要。</summary>
public sealed record WebRendererPublishedWindow(string FullWindowType, bool IsLayoutAvailable, double? CanvasWidth,
    double? CanvasHeight, IReadOnlyList<string> Diagnostics);
