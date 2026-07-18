using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.WebRenderer.Protocol;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>
/// 管理独立 Web Renderer sidecar 进程及其命名管道连接。
/// </summary>
public sealed class WebRendererSidecarService : IHostedService, IDisposable, IRecipient<FrontedLayoutPackagesChangedMessage>
{
    private const string RuntimeDownloadUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/10.0";
    private readonly WebRendererLaunchOptions _options;
    private readonly WebRendererRuntimeDetector _runtimeDetector;
    private readonly WebRendererPlugin _plugin;
    private readonly ISnackbarService _snackbarService;
    private readonly ILogger<WebRendererSidecarService> _logger;
    private readonly WebRendererBootstrapBuilder? _bootstrapBuilder;
    private readonly WebRendererRuntimeStatePublisher? _runtimePublisher;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private Process? _process;
    private NamedPipeServerStream? _pipe;
    private StreamWriter? _pipeWriter;
    private long _sequence;
    private bool _suppressedForSession;
    private string? _lastSidecarError;
    private long _bootstrapGeneration;

    /// <summary>
    /// 初始化 sidecar 服务。
    /// </summary>
    public WebRendererSidecarService(WebRendererLaunchOptions options, WebRendererRuntimeDetector runtimeDetector,
        WebRendererPlugin plugin, ISnackbarService snackbarService, ILogger<WebRendererSidecarService> logger,
        WebRendererBootstrapBuilder? bootstrapBuilder = null,
        WebRendererRuntimeStatePublisher? runtimePublisher = null)
    {
        _options = options;
        _runtimeDetector = runtimeDetector;
        _plugin = plugin;
        _snackbarService = snackbarService;
        _logger = logger;
        _bootstrapBuilder = bootstrapBuilder;
        _runtimePublisher = runtimePublisher;
        if (_runtimePublisher is not null)
            _runtimePublisher.Updated += OnRuntimeUpdated;
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

        await TryStartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        try
        {
            await SendAsync(WebRendererIpcProtocol.Shutdown, new { reason = "host-stopping" }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Web Renderer shutdown IPC was not delivered.");
        }

        _pipeWriter?.Dispose();
        _pipe?.Dispose();
        if (_process is { HasExited: false } process)
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
    }

    /// <summary>
    /// 重新检测 runtime 并在尚未启动时启动 sidecar。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步操作。</returns>
    public Task RetryAsync(CancellationToken cancellationToken = default)
    {
        _suppressedForSession = false;
        return TryStartAsync(cancellationToken);
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
            startInfo.ArgumentList.Add("--address");
            startInfo.ArgumentList.Add(_options.Address);
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add(_options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--plugin-version");
            startInfo.ArgumentList.Add(_plugin.Info.Manifest.Version);

            _process = Process.Start(startInfo);
            if (_process is null)
                throw new InvalidOperationException("无法创建 Web Renderer sidecar 进程。");
            _process.EnableRaisingEvents = true;
            _process.Exited += OnSidecarExited;
            _ = ObserveOutputAsync(_process.StandardError, "stderr", _stopping.Token);
            _ = ObserveOutputAsync(_process.StandardOutput, "stdout", _stopping.Token);
            _logger.LogInformation("Started Web Renderer sidecar at http://{Address}:{Port}", _options.Address, _options.Port);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _logger.LogError(ex, "Failed to start Web Renderer sidecar.");
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
            await pipe.WaitForConnectionAsync(cancellationToken);
            _pipeWriter = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
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
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Web Renderer IPC connection ended unexpectedly.");
        }
    }

    private async Task SendAsync(string type, object payload, CancellationToken cancellationToken)
    {
        if (_pipeWriter is null)
            return;
        var message = new WebRendererIpcMessage
        {
            ProtocolVersion = WebRendererIpcProtocol.Version,
            Sequence = Interlocked.Increment(ref _sequence),
            Type = type,
            Payload = JsonSerializer.SerializeToElement(payload)
        };
        await _pipeWriter.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
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
            _runtimePublisher?.ReplaceLayout(snapshot);
            await SendAsync(WebRendererIpcProtocol.BootstrapReplace, snapshot, cancellationToken);
            await SendAsync(WebRendererIpcProtocol.BootstrapChanged, new { generation = snapshot.Generation }, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Web Renderer bootstrap refresh failed.");
        }
    }

    private void OnRuntimeUpdated(object? sender, WebRendererRuntimeUpdate update)
    {
        var type = update.IsSnapshot ? WebRendererIpcProtocol.RuntimeSnapshot : WebRendererIpcProtocol.RuntimeBindingPatch;
        _ = SendAsync(type, update, _stopping.Token);
    }

    private async Task ObserveOutputAsync(StreamReader reader, string streamName, CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (streamName == "stderr")
                    _lastSidecarError = line.Length > 800 ? line[..800] : line;
                _logger.LogInformation("Web Renderer sidecar {Stream}: {Line}", streamName, line);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void OnSidecarExited(object? sender, EventArgs args)
    {
        if (_stopping.IsCancellationRequested || sender is not Process process)
            return;
        _logger.LogError("Web Renderer sidecar exited unexpectedly with code {ExitCode}. Stderr: {Stderr}", process.ExitCode, _lastSidecarError);
        var missingRuntime = _lastSidecarError?.Contains("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase) == true;
        var detail = string.IsNullOrWhiteSpace(_lastSidecarError) ? "请查看应用日志。" : _lastSidecarError;
        ShowNotification(missingRuntime
            ? "Web Renderer 需要 ASP.NET Core Runtime 10 (x64)。"
            : $"Web Renderer 已意外退出（退出码 {process.ExitCode}）：{detail}", missingRuntime);
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

    /// <inheritdoc />
    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        if (_runtimePublisher is not null)
            _runtimePublisher.Updated -= OnRuntimeUpdated;
        _stopping.Dispose();
        _startLock.Dispose();
        _process?.Dispose();
        _pipeWriter?.Dispose();
        _pipe?.Dispose();
    }
}
