using neo_bpsys_wpf.WebRenderer.Protocol;
using System.Net;
using System.Net.WebSockets;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;

var settings = SidecarSettings.Parse(args);
var state = new WebRendererHostState(settings);
var cancellation = new CancellationTokenSource();

try
{
    _ = state.ConnectPipeAsync(cancellation.Token);
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.ConfigureKestrel(options => options.Listen(settings.Address, settings.Port));
    var app = builder.Build();
    app.Use(async (context, next) =>
    {
        await next();
        if (context.Request.Path.StartsWithSegments("/assets"))
            context.Response.Headers.CacheControl = "no-store";
    });
    app.UseWebSockets();
    app.UseStaticFiles();
    app.MapGet("/health", () => Results.Json(state.Health()));
    app.MapGet("/", () => Results.File(Path.Combine(app.Environment.WebRootPath!, "index.html"), "text/html"));
    app.MapGet("/render/{encodedFullWindowType}", (string encodedFullWindowType) =>
        state.HasWindow(encodedFullWindowType)
            ? Results.File(Path.Combine(app.Environment.WebRootPath!, "index.html"), "text/html")
            : Results.NotFound(new { error = "UnknownWindow" }));
    app.MapGet("/api/windows", () => Results.Json(state.Windows()));
    app.MapGet("/api/bootstrap/{encodedFullWindowType}", (string encodedFullWindowType) => state.Bootstrap(encodedFullWindowType));
    app.MapGet("/assets/{resourceToken}", (string resourceToken) => state.Asset(resourceToken));
    app.Map("/ws", async context =>
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await state.AttachAsync(socket, context.RequestAborted);
        await state.WaitForCloseAsync(socket, context.RequestAborted);
    });
    app.Lifetime.ApplicationStopping.Register(cancellation.Cancel);
    await app.RunAsync(cancellation.Token);
}
catch (Exception ex) { Console.Error.WriteLine($"Web Renderer sidecar failed: {ex}"); Environment.ExitCode = 1; }
finally { cancellation.Cancel(); cancellation.Dispose(); }

internal sealed record SidecarSettings(string PipeName, IPAddress Address, int Port, string PluginVersion)
{
    public static SidecarSettings Parse(string[] args)
    {
        string? pipe = null; var address = "127.0.0.1"; var port = 19527; var pluginVersion = "unknown";
        for (var index = 0; index < args.Length; index++)
        {
            if (index + 1 >= args.Length) continue;
            switch (args[index]) { case "--pipe": pipe = args[++index]; break; case "--address": address = args[++index]; break; case "--port": port = int.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture); break; case "--plugin-version": pluginVersion = args[++index]; break; }
        }
        if (string.IsNullOrWhiteSpace(pipe) || !IPAddress.TryParse(address, out var parsedAddress) || parsedAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || port is < 1 or > 65535) throw new ArgumentException("Invalid Web Renderer sidecar arguments.");
        return new(pipe, parsedAddress, port, pluginVersion);
    }
}

internal sealed class WebRendererHostState(SidecarSettings settings)
{
    private readonly object _gate = new();
    private readonly List<WebSocket> _sockets = [];
    private long _sequence;
    private string _ipcStatus = "connecting";
    private string _hostVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    private JsonDocument? _bootstrap;
    private JsonDocument? _runtime;
    private StreamWriter? _pipeWriter;

    public object Health() => new { protocolVersion = WebRendererIpcProtocol.Version, status = "running", hostVersion = _hostVersion, pluginVersion = settings.PluginVersion, ipcStatus = _ipcStatus, listenAddress = settings.Address.ToString(), port = settings.Port };

    public object Windows()
    {
        lock (_gate)
        {
            if (_bootstrap is null) return Array.Empty<object>();
            return _bootstrap.RootElement.GetProperty("Windows").EnumerateArray().Select(window => new { fullWindowType = window.GetProperty("FullWindowType").GetString(), displayName = window.GetProperty("DisplayName").GetString(), available = window.GetProperty("Layout").ValueKind != JsonValueKind.Null, diagnostics = window.GetProperty("Diagnostics") }).ToArray();
        }
    }

    public bool HasWindow(string encoded) => FindWindow(encoded) is not null;

    public IResult Bootstrap(string encoded)
    {
        var window = FindWindow(encoded);
        if (window is null) return Results.NotFound(new { error = "UnknownWindow" });
        return Results.Json(window.Value);
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
        if (asset.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.String)
            return Results.File(Convert.FromBase64String(data.GetString()!), contentType, enableRangeProcessing: false, lastModified: null, entityTag: null);
        var path = asset.TryGetProperty("FilePath", out var pathProperty) ? pathProperty.GetString() : null;
        return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? Results.NotFound() : Results.File(path, contentType, enableRangeProcessing: false);
    }

    private JsonElement? FindWindow(string encoded)
    {
        string value;
        try { value = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/') + new string('=', (4 - encoded.Length % 4) % 4))); }
        catch { return null; }
        lock (_gate)
        {
            if (_bootstrap is null || !_bootstrap.RootElement.TryGetProperty("Windows", out var windows)) return null;
            foreach (var window in windows.EnumerateArray())
                if (string.Equals(window.GetProperty("FullWindowType").GetString(), value, StringComparison.Ordinal)) return window.Clone();
        }
        return null;
    }

    public async Task ConnectPipeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(TimeSpan.FromSeconds(15), cancellationToken);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            _pipeWriter = writer;
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            await SendPipeAsync(writer, WebRendererIpcProtocol.SidecarReady, new { hostVersion = _hostVersion }, cancellationToken); SetIpcStatus("connected");
            await ReadPipeAsync(reader, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { Console.Error.WriteLine($"IPC connection failed: {ex.Message}"); SetIpcStatus("disconnected"); }
    }

    private async Task ReadPipeAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var message = JsonSerializer.Deserialize<WebRendererIpcMessage>(line); if (message is null) continue;
            if (message.Type == WebRendererIpcProtocol.HostHello && message.Payload.TryGetProperty("hostVersion", out var version)) { _hostVersion = version.GetString() ?? _hostVersion; SetIpcStatus("connected"); }
            if (message.Type == WebRendererIpcProtocol.BootstrapReplace) { lock (_gate) { _bootstrap?.Dispose(); _bootstrap = JsonDocument.Parse(message.Payload.GetRawText()); } }
            if (message.Type == WebRendererIpcProtocol.RuntimeSnapshot) { lock (_gate) { _runtime?.Dispose(); _runtime = JsonDocument.Parse(message.Payload.GetRawText()); } await BroadcastAsync(new { type = "snapshot", payload = message.Payload }); }
            if (message.Type == WebRendererIpcProtocol.RuntimeBindingPatch) await BroadcastAsync(new { type = "bindingPatch", payload = message.Payload });
            if (message.Type == WebRendererIpcProtocol.BootstrapChanged) await BroadcastAsync(new { type = WebRendererIpcProtocol.BootstrapChanged, payload = message.Payload });
            if (message.Type == WebRendererIpcProtocol.Shutdown) Environment.Exit(0);
        }
        _pipeWriter = null;
        SetIpcStatus("disconnected"); Environment.Exit(0);
    }

    private async Task SendPipeAsync(StreamWriter writer, string type, object payload, CancellationToken cancellationToken) => await writer.WriteLineAsync(JsonSerializer.Serialize(new WebRendererIpcMessage { ProtocolVersion = WebRendererIpcProtocol.Version, Sequence = Interlocked.Increment(ref _sequence), Type = type, Payload = JsonSerializer.SerializeToElement(payload) }).AsMemory(), cancellationToken);
    private void SetIpcStatus(string value) { _ipcStatus = value; _ = BroadcastAsync(new { type = "status", payload = Health() }); }
    public async Task AttachAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        JsonElement? runtime = null;
        int count;
        lock (_gate) { _sockets.Add(socket); count = _sockets.Count; if (_runtime is not null) runtime = _runtime.RootElement.Clone(); }
        await SendSocketAsync(socket, new { type = "serverStatus", payload = Health() }, cancellationToken);
        await SendPipeAsyncCurrentClientsAsync(count, cancellationToken);
        if (runtime is not null) await SendSocketAsync(socket, new { type = "snapshot", payload = runtime.Value }, cancellationToken);
    }
    private async Task BroadcastAsync(object value) { WebSocket[] sockets; lock (_gate) sockets = _sockets.Where(socket => socket.State == WebSocketState.Open).ToArray(); await Task.WhenAll(sockets.Select(socket => SendSocketAsync(socket, value, CancellationToken.None))); }
    private static async Task SendSocketAsync(WebSocket socket, object value, CancellationToken cancellationToken) { var content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)); await socket.SendAsync(content, WebSocketMessageType.Text, true, cancellationToken); }
    public async Task WaitForCloseAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16];
        try { while (await socket.ReceiveAsync(buffer, cancellationToken) is { MessageType: not WebSocketMessageType.Close }) { } }
        finally { int count; lock (_gate) { _sockets.Remove(socket); count = _sockets.Count; } await SendPipeAsyncCurrentClientsAsync(count, CancellationToken.None); }
    }

    private Task SendPipeAsyncCurrentClientsAsync(int count, CancellationToken cancellationToken)
    {
        var writer = _pipeWriter;
        return writer is null ? Task.CompletedTask : SendPipeAsync(writer, WebRendererIpcProtocol.SidecarClientsChanged, new { count }, cancellationToken);
    }
}
