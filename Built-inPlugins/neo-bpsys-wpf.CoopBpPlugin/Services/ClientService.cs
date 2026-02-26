using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.CoopBpPlugin.Models;

namespace neo_bpsys_wpf.CoopBpPlugin.Services;

/// <summary>
/// 客户端服务接口
/// 提供TCP客户端功能，包括连接管理、消息收发等
/// </summary>
public interface IClientService : IDisposable
{
    /// <summary>是否已连接到服务器</summary>
    bool IsConnected { get; }

    /// <summary>当前延迟（毫秒）</summary>
    int Latency { get; }

    /// <summary>会话ID</summary>
    string? SessionId { get; }

    /// <summary>连接成功事件</summary>
    event EventHandler? Connected;

    /// <summary>断开连接事件</summary>
    event EventHandler? Disconnected;

    /// <summary>连接错误事件</summary>
    event EventHandler<string>? ConnectionError;

    /// <summary>收到消息事件</summary>
    event EventHandler<NetworkMessage>? MessageReceived;

    /// <summary>连接到服务器</summary>
    Task<bool> ConnectAsync(JoinRoomSettings settings);

    /// <summary>断开连接</summary>
    Task DisconnectAsync();

    /// <summary>发送消息</summary>
    Task SendAsync(NetworkMessage message);
}

/// <summary>
/// 客户端服务实现
/// 负责TCP连接管理、消息收发、心跳检测等功能
/// 支持IPv4和IPv6连接
/// </summary>
public class ClientService : IClientService
{
    private readonly ILogger<ClientService> _logger;
    private TcpClient? _tcpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private StreamWriter? _writer;
    private string? _sessionId;
    private int _latency;

    public bool IsConnected => _tcpClient?.Connected ?? false;
    public int Latency => _latency;
    public string? SessionId => _sessionId;

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<string>? ConnectionError;
    public event EventHandler<NetworkMessage>? MessageReceived;

    public ClientService(ILogger<ClientService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 连接到服务器
    /// 自动尝试IPv6和IPv4连接
    /// </summary>
    public async Task<bool> ConnectAsync(JoinRoomSettings settings)
    {
        if (IsConnected)
        {
            _logger.LogWarning("Client is already connected");
            return false;
        }

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            // 尝试解析服务器地址
            var addresses = await Dns.GetHostAddressesAsync(settings.ServerIp);
            IPAddress? ipv4Address = null;
            IPAddress? ipv6Address = null;

            foreach (var addr in addresses)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    ipv4Address = addr;
                else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                    ipv6Address = addr;
            }

            _logger.LogInformation("Connecting to {Ip}:{Port}...", settings.ServerIp, settings.Port);

            // 优先尝试IPv6连接，失败后回退到IPv4
            bool connected = false;
            Exception? lastException = null;

            // 尝试IPv6连接
            if (ipv6Address != null)
            {
                try
                {
                    _tcpClient = new TcpClient(AddressFamily.InterNetworkV6);
                    await _tcpClient.ConnectAsync(ipv6Address, settings.Port, _cancellationTokenSource.Token);
                    connected = true;
                    _logger.LogInformation("Connected via IPv6");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "IPv6 connection failed, trying IPv4");
                    _tcpClient?.Dispose();
                    _tcpClient = null;
                    lastException = ex;
                }
            }

            // IPv6失败，尝试IPv4连接
            if (!connected && ipv4Address != null)
            {
                try
                {
                    _tcpClient = new TcpClient(AddressFamily.InterNetwork);
                    await _tcpClient.ConnectAsync(ipv4Address, settings.Port, _cancellationTokenSource.Token);
                    connected = true;
                    _logger.LogInformation("Connected via IPv4");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "IPv4 connection failed");
                    _tcpClient?.Dispose();
                    _tcpClient = null;
                    lastException = ex;
                }
            }

            if (!connected)
            {
                var errorMsg = lastException?.Message ?? "Unable to connect to server";
                _logger.LogError("Failed to connect: {Error}", errorMsg);
                ConnectionError?.Invoke(this, $"Connection failed: {errorMsg}");
                return false;
            }

            _logger.LogInformation("TCP connection established");

            var stream = _tcpClient.GetStream();
            var reader = new StreamReader(stream);
            _writer = new StreamWriter(stream) { AutoFlush = true };

            // 发送握手消息
            var handshakeData = new HandshakeData
            {
                ComputerName = Environment.MachineName,
                Password = settings.Password,
                ClientVersion = "1.0.0.0"
            };

            var handshakeMessage = new NetworkMessage(MessageType.Handshake, JsonSerializer.Serialize(handshakeData));
            await _writer.WriteLineAsync(JsonSerializer.Serialize(handshakeMessage));

            // 等待握手响应
            var responseLine = await reader.ReadLineAsync(_cancellationTokenSource.Token);
            if (string.IsNullOrEmpty(responseLine))
            {
                ConnectionError?.Invoke(this, "Server did not respond");
                _tcpClient.Close();
                return false;
            }

            var response = JsonSerializer.Deserialize<NetworkMessage>(responseLine);
            if (response?.Type == MessageType.Error)
            {
                ConnectionError?.Invoke(this, response.Data ?? "Unknown error");
                _tcpClient.Close();
                return false;
            }

            // 处理握手响应
            if (response?.Type == MessageType.HandshakeResponse)
            {
                var responseData = JsonSerializer.Deserialize<HandshakeResponseData>(response.Data ?? "{}");
                if (responseData == null || !responseData.Success)
                {
                    ConnectionError?.Invoke(this, responseData?.ErrorMessage ?? "Connection failed");
                    _tcpClient.Close();
                    return false;
                }

                _sessionId = responseData.SessionId;
                _logger.LogInformation("Connected to server, SessionId: {SessionId}", _sessionId);
            }

            // 启动消息接收和心跳任务
            _ = Task.Run(() => ReceiveMessagesAsync(reader, _cancellationTokenSource.Token), _cancellationTokenSource.Token);
            _ = Task.Run(() => HeartbeatLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            Connected?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (SocketException ex)
        {
            var errorMsg = ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => "Connection refused, please check server address and port",
                SocketError.HostUnreachable => "Unable to reach server, please check network",
                SocketError.TimedOut => "Connection timed out",
                _ => $"Connection failed: {ex.Message}"
            };
            _logger.LogError(ex, "Failed to connect to server");
            ConnectionError?.Invoke(this, errorMsg);
            _tcpClient?.Close();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to server");
            ConnectionError?.Invoke(this, $"Connection failed: {ex.Message}");
            _tcpClient?.Close();
            return false;
        }
    }

    /// <summary>
    /// 断开与服务器的连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (!IsConnected) return;

        _logger.LogInformation("Disconnecting...");

        try
        {
            // 发送断开连接消息
            if (_writer != null && _tcpClient?.Connected == true)
            {
                var disconnectMessage = new NetworkMessage(MessageType.Disconnect);
                await _writer.WriteLineAsync(JsonSerializer.Serialize(disconnectMessage));
            }
        }
        catch { }

        _cancellationTokenSource?.Cancel();
        _tcpClient?.Close();
        _tcpClient = null;
        _sessionId = null;

        _logger.LogInformation("Disconnected");
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 接收消息的循环
    /// </summary>
    private async Task ReceiveMessagesAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _tcpClient?.Connected == true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line))
                {
                    _logger.LogWarning("Server closed the connection");
                    break;
                }

                var message = JsonSerializer.Deserialize<NetworkMessage>(line);
                if (message == null) continue;

                // 处理心跳消息
                if (message.Type == MessageType.Heartbeat)
                {
                    if (long.TryParse(message.Data, out var sendTime))
                    {
                        var latency = (int)((DateTime.UtcNow.Ticks - sendTime) / TimeSpan.TicksPerMillisecond);
                        _latency = latency;
                    }
                    var response = new NetworkMessage(MessageType.HeartbeatResponse, _latency.ToString());
                    await SendAsync(response);
                }
                else if (message.Type == MessageType.Disconnect)
                {
                    _logger.LogInformation("Server requested disconnect");
                    break;
                }
                else
                {
                    MessageReceived?.Invoke(this, message);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages");
        }
        finally
        {
            _ = Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _tcpClient?.Close();
                _tcpClient = null;
                _sessionId = null;
                ConnectionError?.Invoke(this, "Connection to server lost");
                Disconnected?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    /// <summary>
    /// 心跳循环
    /// </summary>
    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _tcpClient?.Connected == true)
        {
            try
            {
                await Task.Delay(3000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 发送消息到服务器
    /// </summary>
    public async Task SendAsync(NetworkMessage message)
    {
        if (_writer == null || _tcpClient?.Connected != true)
        {
            _logger.LogWarning("Not connected to server");
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(message);
            await _writer.WriteLineAsync(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            throw;
        }
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
        _cancellationTokenSource?.Dispose();
    }
}
