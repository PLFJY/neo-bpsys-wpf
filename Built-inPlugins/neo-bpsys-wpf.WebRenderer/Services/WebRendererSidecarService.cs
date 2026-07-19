using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.WebRenderer.Protocol;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;
using static neo_bpsys_wpf.WebRenderer.Services.WebRendererProtocolVersion;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>
/// 管理独立 Web Renderer sidecar 进程及其命名管道连接。
/// </summary>
public sealed class WebRendererSidecarService : IHostedService, IDisposable, IRecipient<FrontedLayoutPackagesChangedMessage>
{
    private const string RuntimeDownloadUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/10.0";
    private static readonly TimeSpan ShutdownIpcTimeout = TimeSpan.FromMilliseconds(750);
    private WebRendererLaunchOptions _options;
    private readonly WebRendererRuntimeDetector _runtimeDetector;
    private readonly WebRendererPlugin _plugin;
    private readonly ISnackbarService _snackbarService;
    private readonly ILogger<WebRendererSidecarService> _logger;
    private readonly WebRendererBootstrapBuilder? _bootstrapBuilder;
    private readonly WebRendererRuntimeStatePublisher? _runtimePublisher;
    private readonly IWebTransitionGateway? _transitionGateway;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    // StreamWriter does not permit a Dispose while WriteLineAsync is in progress.
    // This lock owns both writes and the hand-off to shutdown disposal.
    private readonly SemaphoreSlim _pipeWriteLock = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly WebRendererSidecarJob? _sidecarJob;
    private Process? _process;
    private NamedPipeServerStream? _pipe;
    private StreamWriter? _pipeWriter;
    private long _sequence;
    private bool _suppressedForSession;
    private string? _lastSidecarError;
    private long _bootstrapGeneration;
    private WebRendererBootstrapSnapshot? _lastSnapshot;
    private bool _manualStopped;

    /// <summary>当服务状态改变时发生。</summary>
    public event EventHandler? StatusChanged;

    /// <summary>获取可安全显示的当前服务状态。</summary>
    public WebRendererServiceStatus Status => new(
        _process is { HasExited: false }, _process?.Id, _options.Address, _options.Port,
        _runtimePublisher?.ClientCount ?? 0, _bootstrapGeneration, _lastSidecarError, _options.LogProtocol,
        _lastSnapshot?.ActivePackageId, _lastSnapshot?.Windows.Select(item => item.FullWindowType).ToArray() ?? []);

    /// <summary>
    /// 获取当前 bootstrap 中已公开窗口的显示摘要。
    /// </summary>
    /// <returns>不包含物理路径或资源 token 的窗口摘要。</returns>
    public IReadOnlyList<WebRendererPublishedWindow> GetPublishedWindows()
    {
        return _lastSnapshot?.Windows.Select(window => new WebRendererPublishedWindow(
            window.FullWindowType,
            window.Layout is not null,
            window.Layout?.CanvasSettings.CanvasWidth,
            window.Layout?.CanvasSettings.CanvasHeight,
            window.Diagnostics)).ToArray() ?? [];
    }

    /// <summary>获取是否已成功生成并发送过当前布局 bootstrap。</summary>
    public bool HasBootstrapSnapshot => _lastSnapshot is not null;

    /// <summary>
    /// 初始化 sidecar 服务。
    /// </summary>
    public WebRendererSidecarService(WebRendererLaunchOptions options, WebRendererRuntimeDetector runtimeDetector,
        WebRendererPlugin plugin, ISnackbarService snackbarService, ILogger<WebRendererSidecarService> logger,
        WebRendererBootstrapBuilder? bootstrapBuilder = null,
        WebRendererRuntimeStatePublisher? runtimePublisher = null,
        IWebTransitionGateway? transitionGateway = null)
    {
        _options = options;
        _runtimeDetector = runtimeDetector;
        _plugin = plugin;
        _snackbarService = snackbarService;
        _logger = logger;
        _bootstrapBuilder = bootstrapBuilder;
        _runtimePublisher = runtimePublisher;
        _transitionGateway = transitionGateway;
        try
        {
            _sidecarJob = new WebRendererSidecarJob();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or System.ComponentModel.Win32Exception)
        {
            // The parent-PID monitor remains as a fallback if the host itself is
            // constrained by another Job Object or the platform lacks this API.
            _logger.LogWarning(ex, "Web Renderer could not create its sidecar job; using parent-process monitoring only.");
        }
        if (_runtimePublisher is not null)
        {
            _runtimePublisher.Updated += OnRuntimeUpdated;
            _runtimePublisher.BehaviorEventPublished += OnBehaviorEventPublished;
        }
        if (_transitionGateway is WebTransitionGateway gateway) gateway.SignalPublished += OnTransitionSignalPublished;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        WeakReferenceMessenger.Default.Register(this);
        if (_options.NoStart)
        {
            _logger.LogInformation("Web Renderer sidecar startup was disabled by --web-no-start.");
            return;
        }

        if (_options.ValidationError is not null)
        {
            _logger.LogError("Web Renderer startup option error: {Error}", _options.ValidationError);
            ShowNotification(_options.ValidationError, false);
            return;
        }

        await StartRendererAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        await StopRendererAsync(cancellationToken);
    }

    /// <summary>启动 sidecar；可由管理页重复调用。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步操作。</returns>
    public Task StartRendererAsync(CancellationToken cancellationToken = default)
    {
        _manualStopped = false;
        return TryStartAsync(cancellationToken);
    }

    /// <summary>停止 sidecar，但不停止宿主 HostedService。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步操作。</returns>
    public async Task StopRendererAsync(CancellationToken cancellationToken = default)
    {
        _manualStopped = true;
        try
        {
            // A broken or non-reading pipe must never make a WPF management-page
            // command wait indefinitely. Shutdown delivery is best-effort only.
            using var shutdownCancellation = new CancellationTokenSource(ShutdownIpcTimeout);
            await SendAsync(WebRendererIpcProtocol.Shutdown, new { reason = "host-stopping" }, shutdownCancellation.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            _logger.LogDebug(ex, "Web Renderer shutdown IPC was not delivered.");
        }

        StreamWriter? writer;
        NamedPipeServerStream? pipe;
        await _pipeWriteLock.WaitAsync(CancellationToken.None);
        try
        {
            // Clear the shared references while holding the write lock. Subsequent
            // publishers become no-ops, and any earlier write has completed.
            writer = _pipeWriter;
            pipe = _pipe;
            _pipeWriter = null;
            _pipe = null;
            writer?.Dispose();
            pipe?.Dispose();
        }
        finally
        {
            _pipeWriteLock.Release();
        }
        var process = _process;
        if (process is { HasExited: false })
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Web Renderer sidecar did not exit in time; terminating its process tree.");
                process.Kill(entireProcessTree: true);
            }
        }
        if (process is not null && process.HasExited)
        {
            process.Dispose();
            if (ReferenceEquals(_process, process)) _process = null;
        }
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>安全地重启 sidecar。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步操作。</returns>
    public async Task RestartRendererAsync(CancellationToken cancellationToken = default)
    {
        await StopRendererAsync(cancellationToken);
        await StartRendererAsync(cancellationToken);
    }

    /// <summary>应用管理页保存的设置；命令行覆盖仍由首次启动选项保持。</summary>
    /// <param name="settings">已验证的插件设置。</param>
    public void ApplySettings(WebRendererPluginSettings settings)
    {
        _options = new WebRendererLaunchOptions(settings.Host, settings.Port, !settings.StartWithApplication, settings.LogProtocol, null)
        {
            ExitTimeout = TimeSpan.FromMilliseconds(settings.ExitTimeoutMs is > 0 and <= 30000 ? settings.ExitTimeoutMs : 2000),
            EnterTimeout = TimeSpan.FromMilliseconds(settings.EnterTimeoutMs is > 0 and <= 30000 ? settings.EnterTimeoutMs : 2000)
        };
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 重新检测 runtime 并在尚未启动时启动 sidecar。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步操作。</returns>
    public Task RetryAsync(CancellationToken cancellationToken = default)
    {
        _suppressedForSession = false;
        return StartRendererAsync(cancellationToken);
    }

    private async Task TryStartAsync(CancellationToken cancellationToken)
    {
        if (_suppressedForSession || _stopping.IsCancellationRequested)
            return;

        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_process is { HasExited: false })
                return;

            var runtime = await _runtimeDetector.DetectAsync();
            if (!runtime.IsAvailable || runtime.DotnetPath is null)
            {
                _logger.LogWarning("Web Renderer is unavailable: {Reason}", runtime.ErrorMessage);
                ShowNotification(runtime.ErrorMessage ?? "未检测到 ASP.NET Core Runtime 10 (x64)。", true);
                return;
            }

            if (!IsPortAvailable(_options.Address, _options.Port))
            {
                var message = $"Web Renderer 无法启动：{_options.Address}:{_options.Port} 已被其他进程占用。请在 Web Renderer 管理页更换端口，或关闭占用该端口的旧实例。";
                _lastSidecarError = message;
                _logger.LogError("{Message}", message);
                ShowNotification(message, false);
                StatusChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            var hostPath = Path.Combine(_plugin.Info.PluginFolderPath, "Host", "neo-bpsys-wpf.WebRenderer.Host.dll");
            if (!File.Exists(hostPath))
            {
                var message = $"Web Renderer sidecar 文件缺失：{hostPath}";
                _logger.LogError("{Message}", message);
                ShowNotification(message, false);
                return;
            }

            var pipeName = $"neo-bpsys-wpf.web-renderer.{Environment.ProcessId}.{Guid.NewGuid():N}";
            _pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            _ = AcceptPipeAsync(_pipe, _stopping.Token);
            _lastSidecarError = null;

            var startInfo = new ProcessStartInfo(runtime.DotnetPath)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(hostPath)!
            };
            startInfo.ArgumentList.Add(hostPath);
            startInfo.ArgumentList.Add("--pipe");
            startInfo.ArgumentList.Add(pipeName);
            startInfo.ArgumentList.Add("--parent-pid");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--address");
            startInfo.ArgumentList.Add(_options.Address);
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add(_options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--plugin-version");
            startInfo.ArgumentList.Add(_plugin.Info.Manifest.Version);

            _process = Process.Start(startInfo);
            if (_process is null)
                throw new InvalidOperationException("无法创建 Web Renderer sidecar 进程。");
            try
            {
                _sidecarJob?.Assign(_process);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogWarning(ex, "Web Renderer sidecar could not be assigned to the host job.");
            }
            _process.EnableRaisingEvents = true;
            _process.Exited += OnSidecarExited;
            _ = ObserveOutputAsync(_process.StandardError, "stderr", _stopping.Token);
            _ = ObserveOutputAsync(_process.StandardOutput, "stdout", _stopping.Token);
            _logger.LogInformation("Started Web Renderer sidecar at http://{Address}:{Port}", _options.Address, _options.Port);
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _logger.LogError(ex, "Failed to start Web Renderer sidecar.");
            _lastSidecarError = ex.Message;
            StatusChanged?.Invoke(this, EventArgs.Empty);
            ShowNotification($"Web Renderer 启动失败：{ex.Message}", false);
        }
        finally
        {
            _startLock.Release();
        }
    }

    private async Task AcceptPipeAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Waiting for Web Renderer sidecar IPC connection.");
            await pipe.WaitForConnectionAsync(cancellationToken);
            await _pipeWriteLock.WaitAsync(cancellationToken);
            try
            {
                // StopRendererAsync may have disposed this accepted pipe while the
                // sidecar was connecting. Never publish a writer for an old pipe.
                if (!ReferenceEquals(_pipe, pipe) || _manualStopped)
                    return;
                _pipeWriter = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            }
            finally
            {
                _pipeWriteLock.Release();
            }
            _logger.LogInformation("Web Renderer sidecar IPC connected.");
            await SendAsync(WebRendererIpcProtocol.HostHello, new { hostVersion = AppConstants.AppVersion, pluginVersion = _plugin.Info.Manifest.Version }, cancellationToken);
            await RefreshBootstrapAsync(cancellationToken);
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
            while (!cancellationToken.IsCancellationRequested && await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                var message = JsonSerializer.Deserialize<WebRendererIpcMessage>(line);
                if (message is not null)
                {
                    _logger.LogDebug("Web Renderer IPC received {Type} ({Sequence})", message.Type, message.Sequence);
                    if (message.Type == WebRendererIpcProtocol.SidecarClientsChanged
                        && message.Payload.TryGetProperty("count", out var count)
                        && count.TryGetInt32(out var clientCount))
                        _runtimePublisher?.SetClientCount(clientCount);
                    else if ((message.Type == WebRendererIpcProtocol.TransitionExitCompleted || message.Type == WebRendererIpcProtocol.TransitionEnterCompleted)
                             && message.Payload.TryGetProperty("correlationId", out var correlation))
                        _transitionGateway?.Acknowledge(correlation.GetString() ?? string.Empty, message.Type == WebRendererIpcProtocol.TransitionEnterCompleted);
                    StatusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (IOException ex) when (_process is { HasExited: true } || _stopping.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Web Renderer IPC closed after sidecar shutdown.");
        }
        catch (Exception ex)
        {
            _lastSidecarError = $"Web Renderer IPC connection failed: {ex.Message}";
            _logger.LogWarning(ex, "Web Renderer IPC connection ended unexpectedly.");
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task SendAsync(string type, object payload, CancellationToken cancellationToken)
    {
        await _pipeWriteLock.WaitAsync(cancellationToken);
        try
        {
            var writer = _pipeWriter;
            if (writer is null)
                return;
            var message = new WebRendererIpcMessage
            {
                ProtocolVersion = WebRendererIpcProtocol.Version,
                Sequence = Interlocked.Increment(ref _sequence),
                Type = type,
                Payload = JsonSerializer.SerializeToElement(payload)
            };
            await writer.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
        }
        finally
        {
            _pipeWriteLock.Release();
        }
    }

    /// <summary>响应布局包激活或 Designer 保存，刷新 sidecar 静态布局。</summary>
    public void Receive(FrontedLayoutPackagesChangedMessage message) => _ = RefreshBootstrapAsync(_stopping.Token);

    private async Task RefreshBootstrapAsync(CancellationToken cancellationToken)
    {
        if (_bootstrapBuilder is null || _pipeWriter is null)
            return;
        try
        {
            var snapshot = await _bootstrapBuilder.BuildAsync(Interlocked.Increment(ref _bootstrapGeneration), cancellationToken);
            _lastSnapshot = snapshot;
            _logger.LogInformation(
                "Web Renderer bootstrap refreshed for package {PackageId}: {WindowCount} windows, {RenderableWindowCount} layouts.",
                snapshot.ActivePackageId,
                snapshot.Windows.Count,
                snapshot.Windows.Count(window => window.Layout is not null));
            _transitionGateway?.UpdateGeneration(snapshot.Generation);
            _runtimePublisher?.ReplaceLayout(snapshot);
            await SendAsync(WebRendererIpcProtocol.BootstrapReplace, snapshot, cancellationToken);
            await SendAsync(WebRendererIpcProtocol.BootstrapChanged, new { generation = snapshot.Generation }, cancellationToken);
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _lastSidecarError = $"Web Renderer bootstrap refresh failed: {ex.Message}";
            _logger.LogWarning(ex, "Web Renderer bootstrap refresh failed.");
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnRuntimeUpdated(object? sender, WebRendererRuntimeUpdate update)
    {
        var type = update.IsSnapshot ? WebRendererIpcProtocol.RuntimeSnapshot : WebRendererIpcProtocol.RuntimeBindingPatch;
        _ = SendAsync(type, update, _stopping.Token);
    }

    private void OnBehaviorEventPublished(object? sender, WebRendererBehaviorEvent behaviorEvent) =>
        _ = SendAsync(WebRendererIpcProtocol.BehaviorEvent, behaviorEvent, _stopping.Token);

    private void OnTransitionSignalPublished(object? sender, WebTransitionSignal signal) =>
        _ = SendAsync(signal.Type, new
        {
            correlationId = signal.Session.CorrelationId,
            generation = signal.Session.Generation,
            requests = signal.Session.Requests,
            reason = signal.Reason
        }, _stopping.Token);

    private async Task ObserveOutputAsync(StreamReader reader, string streamName, CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (streamName == "stderr")
                    _lastSidecarError = line.Length > 2000 ? line[..2000] : line;
                _logger.LogInformation("Web Renderer sidecar {Stream}: {Line}", streamName, line);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void OnSidecarExited(object? sender, EventArgs args)
    {
        if (_stopping.IsCancellationRequested || sender is not Process process)
            return;
        if (_manualStopped) return;
        _logger.LogError("Web Renderer sidecar exited unexpectedly with code {ExitCode}. Stderr: {Stderr}", process.ExitCode, _lastSidecarError);
        var missingRuntime = _lastSidecarError?.Contains("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase) == true;
        var detail = string.IsNullOrWhiteSpace(_lastSidecarError) ? "请查看应用日志。" : _lastSidecarError;
        ShowNotification(missingRuntime
            ? "Web Renderer 需要 ASP.NET Core Runtime 10 (x64)。"
            : $"Web Renderer 已意外退出（退出码 {process.ExitCode}）：{detail}", missingRuntime);
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowNotification(string message, bool showRuntimeActions)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var panel = new StackPanel();
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 });
            if (showRuntimeActions)
            {
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
                var download = new System.Windows.Controls.Button { Content = "前往下载", Margin = new Thickness(0, 0, 8, 0) };
                download.Click += (_, _) => Process.Start(new ProcessStartInfo(RuntimeDownloadUrl) { UseShellExecute = true });
                var retry = new System.Windows.Controls.Button { Content = "重新检测", Margin = new Thickness(0, 0, 8, 0) };
                retry.Click += async (_, _) => await RetryAsync();
                var suppress = new System.Windows.Controls.Button { Content = "暂不启用" };
                suppress.Click += (_, _) => _suppressedForSession = true;
                actions.Children.Add(download);
                actions.Children.Add(retry);
                actions.Children.Add(suppress);
                panel.Children.Add(actions);
            }
            _snackbarService.Show("Web Renderer", panel, ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.Warning24), TimeSpan.Zero, true);
        });
    }

    private static bool IsPortAvailable(string address, int port)
    {
        if (!IPAddress.TryParse(address, out var parsedAddress)) return false;
        try
        {
            using var listener = new TcpListener(parsedAddress, port);
            listener.Start();
            return true;
        }
        catch (SocketException) { return false; }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        if (_runtimePublisher is not null)
        {
            _runtimePublisher.Updated -= OnRuntimeUpdated;
            _runtimePublisher.BehaviorEventPublished -= OnBehaviorEventPublished;
        }
        if (_transitionGateway is WebTransitionGateway gateway) gateway.SignalPublished -= OnTransitionSignalPublished;
        _stopping.Dispose();
        _startLock.Dispose();
        _pipeWriteLock.Dispose();
        _sidecarJob?.Dispose();
        _process?.Dispose();
        // Dispose is only reached after the Generic Host has awaited StopAsync.
        // Do not race StreamWriter.Dispose with a pending IPC write here.
        _pipeWriter?.Dispose();
        _pipe?.Dispose();
    }
}

/// <summary>供后台管理页显示的 Web Renderer 状态。</summary>
public sealed record WebRendererServiceStatus(bool IsRunning, int? ProcessId, string Address, int Port,
    int ClientCount, long BootstrapGeneration, string? LastError, bool LogProtocol, string? ActivePackageId,
    IReadOnlyList<string> Windows);

/// <summary>管理页使用的已公开 Web 前台窗口摘要。</summary>
public sealed record WebRendererPublishedWindow(string FullWindowType, bool IsLayoutAvailable,
    double? CanvasWidth, double? CanvasHeight, IReadOnlyList<string> Diagnostics);
