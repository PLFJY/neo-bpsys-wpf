using Microsoft.Extensions.FileProviders;
using neo_bpsys_wpf.WebRenderer.Host;
using neo_bpsys_wpf.WebRenderer.Protocol;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

var settings = SidecarSettings.Parse(args);
using var cancellation = new CancellationTokenSource();
try
{
    var staticRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    var client = StaticClientVerifier.Verify(staticRoot);
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.ConfigureKestrel(options => options.Listen(settings.Address, settings.Port));
    builder.Services.AddSingleton<RemoteAssetAddressPolicy>();
    builder.Services.AddHttpClient("RemoteAssets", client => client.Timeout = Timeout.InfiniteTimeSpan)
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = RemoteAssetAddressPolicy.ConnectPublicAsync
        });
    builder.Services.AddSingleton<RemoteAssetFetcher>();
    var app = builder.Build();
    var state = new WebRendererHostState(settings, client.BuildId,
        app.Services.GetRequiredService<RemoteAssetFetcher>());
    state.Start(cancellation.Token);
    state.ShutdownRequested += (_, _) => app.Lifetime.StopApplication();
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/" || context.Request.Path == "/index.html" || context.Request.Path.StartsWithSegments("/render"))
            context.Response.Headers.CacheControl = "no-store";
        else if (context.Request.Path.StartsWithSegments("/assets") &&
                 (context.Request.Path.Value?.EndsWith(".js", StringComparison.OrdinalIgnoreCase) == true || context.Request.Path.Value?.EndsWith(".css", StringComparison.OrdinalIgnoreCase) == true))
            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        await next();
    });
    app.UseWebSockets();
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(staticRoot) });
    app.MapGet("/health", () => Results.Json(state.Health()));
    app.MapGet("/", () => Results.File(client.IndexPath, "text/html"));
    app.MapGet("/render/{encodedFullWindowType}", (string encodedFullWindowType) => state.Render(encodedFullWindowType, client.IndexPath));
    app.MapGet("/api/windows", () => state.Windows());
    app.MapGet("/api/bootstrap/{encodedFullWindowType}", (string encodedFullWindowType) => state.Bootstrap(encodedFullWindowType));
    app.MapGet("/bpui-assets/{resourceToken}", (string resourceToken) => state.Asset(resourceToken));
    app.MapGet("/runtime-assets/{token}", (string token, HttpContext context) => state.RuntimeAsset(token, context));
    app.MapGet("/remote-assets/{token}", (string token, HttpContext context) => state.RemoteAsset(token, context));
    app.Map("/ws", async context =>
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await state.AttachAsync(socket, context.RequestAborted);
        await state.WaitForCloseAsync(socket, context.RequestAborted);
    });
    app.Lifetime.ApplicationStopping.Register(cancellation.Cancel);
    _ = StopWhenParentExitsAsync(settings.ParentProcessId, settings.ParentProcessStartTicks, app.Lifetime, cancellation.Token);
    await app.RunAsync(cancellation.Token);
}
catch (Exception ex) { Console.Error.WriteLine($"Web Renderer sidecar failed: {ex}"); Environment.ExitCode = 1; }

static async Task StopWhenParentExitsAsync(int parentProcessId, long parentProcessStartTicks, IHostApplicationLifetime lifetime, CancellationToken cancellationToken)
{
    try
    {
        using var parent = Process.GetProcessById(parentProcessId);
        if (parent.StartTime.ToUniversalTime().Ticks != parentProcessStartTicks)
        {
            Console.Error.WriteLine("Web Renderer parent PID identity no longer matches; stopping sidecar.");
            lifetime.StopApplication();
            return;
        }
        await parent.WaitForExitAsync(cancellationToken);
        lifetime.StopApplication();
    }
    catch (ArgumentException) { lifetime.StopApplication(); }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
}

internal sealed record SidecarSettings(string PipeName, int ParentProcessId, long ParentProcessStartTicks, IPAddress Address, int Port, string PluginVersion)
{
    public static SidecarSettings Parse(string[] args)
    {
        string? pipe = null; var parentProcessId = 0; long parentProcessStartTicks = 0; var address = "127.0.0.1"; var port = 19527; var pluginVersion = "unknown";
        for (var index = 0; index < args.Length; index++)
        {
            if (index + 1 >= args.Length) continue;
            switch (args[index]) { case "--pipe": pipe = args[++index]; break; case "--parent-pid": parentProcessId = int.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture); break; case "--parent-start-ticks": parentProcessStartTicks = long.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture); break; case "--address": address = args[++index]; break; case "--port": port = int.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture); break; case "--plugin-version": pluginVersion = args[++index]; break; }
        }
        if (string.IsNullOrWhiteSpace(pipe) || parentProcessId <= 0 || parentProcessStartTicks <= 0 || !IPAddress.TryParse(address, out var parsedAddress) || parsedAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || port is < 1 or > 65535) throw new ArgumentException("Invalid Web Renderer sidecar arguments.");
        return new(pipe, parentProcessId, parentProcessStartTicks, parsedAddress, port, pluginVersion);
    }
}

internal sealed class WebRendererHostState(SidecarSettings settings, string clientBuildId,
    RemoteAssetFetcher? remoteAssets = null)
{
    private readonly object _gate = new();
    private readonly List<WebSocket> _sockets = [];
    private readonly Channel<WebRendererOutbound> _outbound = Channel.CreateUnbounded<WebRendererOutbound>(new UnboundedChannelOptions { SingleReader = true });
    private long _sequence;
    private long _lastInboundSequence;
    private bool _connected;
    private WebRendererLifecycleState _state = WebRendererLifecycleState.WaitingForPipe;
    private string? _errorCode;
    private string? _errorMessage;
    private string _hostVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    private long _generation;
    private JsonDocument? _bootstrap;
    private JsonDocument? _runtime;

    /// <summary>主程序通过 IPC 请求 sidecar 完整停止时发生。</summary>
    public event EventHandler? ShutdownRequested;

    public void Start(CancellationToken cancellationToken) => _ = RunConnectionLoopAsync(cancellationToken);

    public object Health()
    {
        lock (_gate) return new { protocolVersion = WebRendererIpcProtocol.Version, status = _state.ToString(), ipcStatus = _connected ? "connected" : "IpcUnavailable", generation = _generation, errorCode = _errorCode, errorMessage = _errorMessage, hostVersion = _hostVersion, pluginVersion = settings.PluginVersion, clientBuildId, activePackageId = ActivePackageIdLocked(), windowCount = WindowCountLocked(), listenAddress = settings.Address.ToString(), port = settings.Port };
    }

    public IResult Windows()
    {
        lock (_gate)
        {
            if (!_connected) return Problem("IpcUnavailable", "Named-pipe IPC is not connected.");
            if (_state == WebRendererLifecycleState.Faulted) return Problem(_errorCode ?? "BootstrapFailed", _errorMessage ?? "Bootstrap failed.");
            if (_state != WebRendererLifecycleState.Ready || _bootstrap is null) return Problem("BootstrapPending", "Web Renderer is waiting for the host layout bootstrap.");
            return Results.Json(WindowsLocked());
        }
    }

    public IResult Render(string encoded, string indexPath)
    {
        lock (_gate)
        {
            if (!_connected || _state != WebRendererLifecycleState.Ready || _bootstrap is null)
                return Results.Content("<!doctype html><title>Web Renderer</title><main>正在等待主程序布局数据。</main>", "text/html", Encoding.UTF8, StatusCodes.Status503ServiceUnavailable);
            var window = FindWindowLocked(encoded);
            if (window is null) return Results.NotFound(new { error = "UnknownWindow" });
            return window.Value.GetProperty("Layout").ValueKind == JsonValueKind.Null
                ? Results.Conflict(new { error = "LayoutUnavailable" })
                : Results.File(indexPath, "text/html");
        }
    }

    public IResult Bootstrap(string encoded)
    {
        lock (_gate)
        {
            if (!_connected) return Problem("IpcUnavailable", "Named-pipe IPC is not connected.");
            if (_state == WebRendererLifecycleState.Faulted) return Problem(_errorCode ?? "BootstrapFailed", _errorMessage ?? "Bootstrap failed.");
            if (_state != WebRendererLifecycleState.Ready || _bootstrap is null) return Problem("BootstrapPending", "Web Renderer is waiting for the host layout bootstrap.");
            var window = FindWindowLocked(encoded);
            return window is null ? Results.NotFound(new { error = "UnknownWindow" }) : Results.Json(window.Value);
        }
    }

    public IResult Asset(string token)
    {
        if (token.Length != 48 || token.Any(character => !Uri.IsHexDigit(character))) return Results.NotFound();
        JsonElement asset;
        lock (_gate)
        {
            if (_bootstrap is null || !_bootstrap.RootElement.GetProperty("Assets").TryGetProperty(token, out asset)) return Results.NotFound();
            asset = asset.Clone();
        }
        var contentType = asset.GetProperty("ContentType").GetString() ?? "application/octet-stream";
        if (asset.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.String) return Results.File(Convert.FromBase64String(data.GetString()!), contentType);
        var path = asset.TryGetProperty("FilePath", out var value) ? value.GetString() : null;
        return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? Results.NotFound() : Results.File(path, contentType);
    }

    /// <summary>返回由插件注册的动态图片；token 只能定位受控临时缓存。</summary>
    public IResult RuntimeAsset(string token, HttpContext context)
    {
        if (token.Length != 64 || token.Any(character => !Uri.IsHexDigit(character))) return Results.NotFound();
        var path = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-web-runtime-assets", token.ToLowerInvariant() + ".png");
        if (!File.Exists(path)) return Results.NotFound();
        context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return Results.File(path, "image/png", enableRangeProcessing: false);
    }

    /// <summary>返回当前 generation 已授权并完成缓存的远程图片。</summary>
    public IResult RemoteAsset(string token, HttpContext context)
    {
        long generation;
        lock (_gate) generation = _generation;
        if (remoteAssets is null || !remoteAssets.TryGet(token, generation, out var entry))
            return Results.NotFound();
        context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return Results.File(entry.Bytes, entry.ContentType, enableRangeProcessing: false);
    }

    private async Task RunConnectionLoopAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(250);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                Console.Error.WriteLine("Web Renderer IPC connecting to host.");
                await pipe.ConnectAsync(cancellationToken);
                lock (_gate) { _connected = true; _lastInboundSequence = 0; }
                Console.Error.WriteLine("Web Renderer pipe connected.");
                using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var writer = Task.Run(() => WriteLoopAsync(pipe, session.Token), session.Token);
                await QueueAsync(WebRendererIpcProtocol.SidecarReady, new { hostVersion = _hostVersion }, cancellationToken);
                await ReadLoopAsync(pipe, session.Token);
                session.Cancel(); _outbound.Writer.TryWrite(WebRendererOutbound.Flush());
                try { await writer; } catch (OperationCanceledException) { }
                delay = TimeSpan.FromMilliseconds(250);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception ex) { Console.Error.WriteLine($"Web Renderer IPC disconnected: {ex.Message}"); }
            lock (_gate) { _connected = false; if (_state != WebRendererLifecycleState.Stopping) _state = WebRendererLifecycleState.WaitingForPipe; }
            await BroadcastAsync(new { type = "status", payload = Health() });
            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 5000));
        }
    }

    private async Task ReadLoopAsync(Stream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, leaveOpen: true);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var message = JsonSerializer.Deserialize<WebRendererIpcMessage>(line);
            if (message is null || !AcceptInbound(message)) continue;
            switch (message.Type)
            {
                case WebRendererIpcProtocol.HostHello:
                    if (message.Payload.TryGetProperty("hostVersion", out var version)) _hostVersion = version.GetString() ?? _hostVersion;
                    Console.Error.WriteLine("Web Renderer host.hello received.");
                    break;
                case WebRendererIpcProtocol.SessionState:
                    ApplySessionState(message.Payload);
                    break;
                case WebRendererIpcProtocol.BootstrapReplace:
                    await ApplyBootstrapAsync(message.Payload, cancellationToken);
                    break;
                case WebRendererIpcProtocol.BootstrapFailed:
                    ApplyBootstrapFailure(message.Payload);
                    break;
                case WebRendererIpcProtocol.RuntimeSnapshot:
                    lock (_gate) { _runtime?.Dispose(); _runtime = JsonDocument.Parse(message.Payload.GetRawText()); }
                    await BroadcastAsync(new { type = "snapshot", payload = message.Payload });
                    break;
                case WebRendererIpcProtocol.RuntimeBindingPatch:
                    await BroadcastAsync(new { type = "bindingPatch", payload = message.Payload }); break;
                case WebRendererIpcProtocol.RemoteAssetFetch:
                    var request = message.Payload.Deserialize<WebRemoteAssetFetch>();
                    if (request is not null) _ = HandleRemoteAssetFetchAsync(request, cancellationToken);
                    break;
                case WebRendererIpcProtocol.BehaviorEvent:
                    await BroadcastAsync(new { type = "behavior.event", payload = message.Payload }); break;
                case WebRendererIpcProtocol.TransitionPrepare or WebRendererIpcProtocol.TransitionCommitted or WebRendererIpcProtocol.TransitionCancel:
                    await BroadcastAsync(new { type = message.Type, payload = message.Payload }); break;
                case WebRendererIpcProtocol.Shutdown:
                    lock (_gate) _state = WebRendererLifecycleState.Stopping;
                    ShutdownRequested?.Invoke(this, EventArgs.Empty);
                    return;
            }
        }
    }

    private bool AcceptInbound(WebRendererIpcMessage message)
    {
        lock (_gate)
        {
            if (message.ProtocolVersion != WebRendererIpcProtocol.Version || message.Sequence <= _lastInboundSequence) { Console.Error.WriteLine($"Web Renderer IPC rejected {message.Type}: protocol or sequence."); return false; }
            _lastInboundSequence = message.Sequence;
            return true;
        }
    }

    private async Task ApplyBootstrapAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        if (!TryValidateBootstrap(payload, out var failure, out var generation, out var activePackageId, out var windowCount, out var renderableCount))
        {
            await QueueAsync(WebRendererIpcProtocol.BootstrapRejected, failure!, cancellationToken); return;
        }
        lock (_gate)
        {
            if (generation <= _generation) { failure = new WebRendererBootstrapFailure(generation, "StaleGeneration", "Bootstrap generation is not newer than the accepted generation."); }
            else { _bootstrap?.Dispose(); _bootstrap = JsonDocument.Parse(payload.GetRawText()); _generation = generation; _errorCode = null; _errorMessage = null; }
        }
        if (failure is not null) { await QueueAsync(WebRendererIpcProtocol.BootstrapRejected, failure, cancellationToken); return; }
        remoteAssets?.SetGeneration(generation);
        Console.Error.WriteLine($"Web Renderer bootstrap.replace applied: generation {generation}.");
        await QueueAsync(WebRendererIpcProtocol.BootstrapApplied, new WebRendererBootstrapApplied(generation, windowCount, renderableCount, activePackageId), cancellationToken);
        await BroadcastAsync(new { type = WebRendererIpcProtocol.BootstrapChanged, payload = new { generation } });
    }

    private static bool TryValidateBootstrap(JsonElement payload, out WebRendererBootstrapFailure? failure, out long generation, out string activePackageId, out int windowCount, out int renderableCount)
    {
        failure = null; generation = 0; activePackageId = string.Empty; windowCount = 0; renderableCount = 0;
        try
        {
            if (!payload.TryGetProperty("ProtocolVersion", out var protocol) || protocol.GetInt32() != WebRendererIpcProtocol.Version) throw new InvalidOperationException("ProtocolVersionMismatch");
            generation = payload.GetProperty("Generation").GetInt64(); activePackageId = payload.GetProperty("ActivePackageId").GetString() ?? string.Empty;
            if (generation <= 0 || string.IsNullOrWhiteSpace(activePackageId)) throw new InvalidOperationException("InvalidBootstrapIdentity");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var window in payload.GetProperty("Windows").EnumerateArray())
            {
                var name = window.GetProperty("FullWindowType").GetString();
                if (string.IsNullOrWhiteSpace(name) || !names.Add(name)) throw new InvalidOperationException("InvalidWindowStructure");
                var layout = window.GetProperty("Layout");
                if (layout.ValueKind != JsonValueKind.Null)
                {
                    if (!layout.TryGetProperty("WindowSettings", out _) || !layout.TryGetProperty("CanvasSettings", out _) || !layout.TryGetProperty("ControlLayout", out _)) throw new InvalidOperationException("InvalidLayoutStructure");
                    renderableCount++;
                }
                windowCount++;
            }
            if (!payload.TryGetProperty("Assets", out var assets) || assets.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("InvalidAssets");
            return true;
        }
        catch (Exception ex) { failure = new WebRendererBootstrapFailure(generation, "BootstrapValidationFailed", ex.Message); return false; }
    }

    private void ApplySessionState(JsonElement payload)
    {
        try
        {
            var value = payload.Deserialize<WebRendererSessionState>();
            if (value is null) return;
            lock (_gate) { _state = value.State; _errorCode = value.ErrorCode; _errorMessage = value.ErrorMessage; }
            _ = BroadcastAsync(new { type = "status", payload = Health() });
        }
        catch (JsonException) { }
    }

    private async Task HandleRemoteAssetFetchAsync(WebRemoteAssetFetch request, CancellationToken cancellationToken)
    {
        if (remoteAssets is null) return;
        try
        {
            var entry = await remoteAssets.FetchAsync(request, cancellationToken);
            await QueueAsync(WebRendererIpcProtocol.RemoteAssetResolved,
                new WebRemoteAssetResult(request.Generation, request.Token, request.Revision, entry.ContentType),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            var diagnostic = ex is RemoteAssetException remote
                ? remote.Diagnostic
                : ex is OperationCanceledException ? "RemoteAssetRequestTimeout" : "RemoteAssetDownloadFailed";
            var tokenSummary = request.Token.Length <= 8 ? request.Token : request.Token[..8];
            Console.Error.WriteLine($"Web Renderer remote asset failed: {diagnostic}; generation={request.Generation}; token={tokenSummary}.");
            try
            {
                await QueueAsync(WebRendererIpcProtocol.RemoteAssetFailed,
                    new WebRemoteAssetResult(request.Generation, request.Token, request.Revision, Diagnostic: diagnostic),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        }
    }

    private void ApplyBootstrapFailure(JsonElement payload)
    {
        try { var failure = payload.Deserialize<WebRendererBootstrapFailure>(); if (failure is not null) lock (_gate) { _errorCode = failure.Code; _errorMessage = failure.Message; _state = WebRendererLifecycleState.Faulted; } }
        catch (JsonException) { }
    }

    private async Task QueueAsync(string type, object payload, CancellationToken cancellationToken) => await _outbound.Writer.WriteAsync(new(type, payload), cancellationToken);
    private async Task WriteLoopAsync(Stream pipe, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
        await foreach (var item in _outbound.Reader.ReadAllAsync(cancellationToken))
        {
            if (item.IsFlush) continue;
            var message = new WebRendererIpcMessage { ProtocolVersion = WebRendererIpcProtocol.Version, Sequence = Interlocked.Increment(ref _sequence), Type = item.Type!, Payload = JsonSerializer.SerializeToElement(item.Payload) };
            await writer.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
        }
    }

    public async Task AttachAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        JsonElement? runtime = null; int count;
        lock (_gate) { _sockets.Add(socket); count = _sockets.Count; if (_runtime is not null) runtime = _runtime.RootElement.Clone(); }
        await SendSocketAsync(socket, new { type = "serverStatus", payload = Health() }, cancellationToken);
        await QueueAsync(WebRendererIpcProtocol.SidecarClientsChanged, new { count }, cancellationToken);
        if (runtime is not null) await SendSocketAsync(socket, new { type = "snapshot", payload = runtime.Value }, cancellationToken);
    }

    public async Task WaitForCloseAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        try
        {
            while (await socket.ReceiveAsync(buffer, cancellationToken) is { MessageType: not WebSocketMessageType.Close } received)
            {
                if (received.MessageType != WebSocketMessageType.Text || !received.EndOfMessage) continue;
                try
                {
                    using var message = JsonDocument.Parse(buffer.AsMemory(0, received.Count));
                    if (!message.RootElement.TryGetProperty("type", out var typeProperty) || !message.RootElement.TryGetProperty("correlationId", out var idProperty)) continue;
                    var type = typeProperty.GetString(); var id = idProperty.GetString();
                    if (type is WebRendererIpcProtocol.TransitionExitCompleted or WebRendererIpcProtocol.TransitionEnterCompleted && !string.IsNullOrWhiteSpace(id))
                        await QueueAsync(type, new { correlationId = id }, cancellationToken);
                }
                catch (JsonException) { }
            }
        }
        finally { int count; lock (_gate) { _sockets.Remove(socket); count = _sockets.Count; } try { await QueueAsync(WebRendererIpcProtocol.SidecarClientsChanged, new { count }, CancellationToken.None); } catch { } }
    }

    private async Task BroadcastAsync(object value)
    {
        WebSocket[] sockets; lock (_gate) sockets = _sockets.Where(socket => socket.State == WebSocketState.Open).ToArray();
        await Task.WhenAll(sockets.Select(socket => SendSocketAsync(socket, value, CancellationToken.None)));
    }
    private static Task SendSocketAsync(WebSocket socket, object value, CancellationToken cancellationToken) => socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)), WebSocketMessageType.Text, true, cancellationToken);
    private JsonElement? FindWindowLocked(string encoded)
    {
        try
        {
            var value = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/') + new string('=', (4 - encoded.Length % 4) % 4)));
            if (_bootstrap is null) return null;
            foreach (var window in _bootstrap.RootElement.GetProperty("Windows").EnumerateArray()) if (string.Equals(window.GetProperty("FullWindowType").GetString(), value, StringComparison.Ordinal)) return window.Clone();
        }
        catch (FormatException) { }
        return null;
    }
    private object[] WindowsLocked() => _bootstrap!.RootElement.GetProperty("Windows").EnumerateArray().Select(window => new { fullWindowType = window.GetProperty("FullWindowType").GetString(), displayName = window.GetProperty("DisplayName").GetString(), available = window.GetProperty("Layout").ValueKind != JsonValueKind.Null, diagnostics = window.GetProperty("Diagnostics") }).Cast<object>().ToArray();
    private string? ActivePackageIdLocked() => _bootstrap?.RootElement.TryGetProperty("ActivePackageId", out var value) == true ? value.GetString() : null;
    private int WindowCountLocked() => _bootstrap?.RootElement.TryGetProperty("Windows", out var value) == true ? value.GetArrayLength() : 0;
    private static IResult Problem(string code, string detail) => Results.Problem(detail, statusCode: StatusCodes.Status503ServiceUnavailable, extensions: new Dictionary<string, object?> { ["error"] = code });
    private sealed record WebRendererOutbound(string? Type, object? Payload, bool IsFlush = false) { public static WebRendererOutbound Flush() => new(null, null, true); }
}
