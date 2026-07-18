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
    app.UseWebSockets();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapGet("/health", () => Results.Json(state.Snapshot()));
    app.MapGet("/", () => Results.File(Path.Combine(app.Environment.WebRootPath!, "index.html"), "text/html"));
    app.Map("/ws", async context =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await state.SendSnapshotAsync(socket, context.RequestAborted);
        await state.WaitForCloseAsync(socket, context.RequestAborted);
    });
    app.Lifetime.ApplicationStopping.Register(cancellation.Cancel);
    await app.RunAsync(cancellation.Token);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Web Renderer sidecar failed: {ex}");
    Environment.ExitCode = 1;
}
finally
{
    cancellation.Cancel();
    cancellation.Dispose();
}

internal sealed record SidecarSettings(string PipeName, IPAddress Address, int Port, string PluginVersion)
{
    public static SidecarSettings Parse(string[] args)
    {
        string? pipe = null;
        string address = "127.0.0.1";
        var port = 19527;
        var pluginVersion = "unknown";
        for (var index = 0; index < args.Length; index++)
        {
            if (index + 1 >= args.Length)
                continue;
            switch (args[index])
            {
                case "--pipe": pipe = args[++index]; break;
                case "--address": address = args[++index]; break;
                case "--port": port = int.Parse(args[++index], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--plugin-version": pluginVersion = args[++index]; break;
            }
        }
        if (string.IsNullOrWhiteSpace(pipe) || !IPAddress.TryParse(address, out var parsedAddress) || parsedAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || port is < 1 or > 65535)
            throw new ArgumentException("Invalid Web Renderer sidecar arguments.");
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

    public object Snapshot() => new
    {
        protocolVersion = WebRendererIpcProtocol.Version,
        status = "running",
        hostVersion = _hostVersion,
        pluginVersion = settings.PluginVersion,
        ipcStatus = _ipcStatus,
        listenAddress = settings.Address.ToString(),
        port = settings.Port
    };

    public async Task ConnectPipeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(".", settings.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(TimeSpan.FromSeconds(15), cancellationToken);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            await SendPipeAsync(writer, WebRendererIpcProtocol.SidecarReady, new { hostVersion = _hostVersion }, cancellationToken);
            SetIpcStatus("connected");
            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(5));
            var readTask = ReadPipeAsync(reader, cancellationToken);
            while (await heartbeat.WaitForNextTickAsync(cancellationToken))
                await SendPipeAsync(writer, WebRendererIpcProtocol.Heartbeat, new { }, cancellationToken);
            await readTask;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"IPC connection failed: {ex.Message}");
            SetIpcStatus("disconnected");
        }
    }

    private async Task ReadPipeAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var message = JsonSerializer.Deserialize<WebRendererIpcMessage>(line);
            if (message is null)
                continue;
            if (message.Type == WebRendererIpcProtocol.HostHello)
            {
                if (message.Payload.TryGetProperty("hostVersion", out var version))
                    _hostVersion = version.GetString() ?? _hostVersion;
                SetIpcStatus("connected");
            }
            if (message.Type == WebRendererIpcProtocol.Shutdown)
                Environment.Exit(0);
        }
        SetIpcStatus("disconnected");
        Environment.Exit(0);
    }

    private async Task SendPipeAsync(StreamWriter writer, string type, object payload, CancellationToken cancellationToken)
    {
        var message = new WebRendererIpcMessage
        {
            ProtocolVersion = WebRendererIpcProtocol.Version,
            Sequence = Interlocked.Increment(ref _sequence),
            Type = type,
            Payload = JsonSerializer.SerializeToElement(payload)
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
    }

    private void SetIpcStatus(string value)
    {
        _ipcStatus = value;
        lock (_gate)
        {
            foreach (var socket in _sockets.Where(socket => socket.State == WebSocketState.Open).ToArray())
                _ = SendSnapshotAsync(socket, CancellationToken.None);
        }
    }

    public async Task SendSnapshotAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        lock (_gate) _sockets.Add(socket);
        var content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Snapshot()));
        await socket.SendAsync(content, WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task WaitForCloseAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16];
        try { while (await socket.ReceiveAsync(buffer, cancellationToken) is { MessageType: not WebSocketMessageType.Close }) { } }
        finally { lock (_gate) _sockets.Remove(socket); }
    }
}
