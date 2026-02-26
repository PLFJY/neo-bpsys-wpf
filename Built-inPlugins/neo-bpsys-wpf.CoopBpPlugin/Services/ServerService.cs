using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.CoopBpPlugin.Models;

namespace neo_bpsys_wpf.CoopBpPlugin.Services;

/// <summary>
/// 服务器服务接口
/// 提供TCP服务器功能，包括客户端管理、消息广播等
/// </summary>
public interface IServerService : IDisposable
{
    /// <summary>服务器是否正在运行</summary>
    bool IsRunning { get; }

    /// <summary>已连接的客户端数量</summary>
    int ConnectedClientsCount { get; }

    /// <summary>已连接客户端列表</summary>
    ObservableCollection<ClientInfo> ConnectedClients { get; }

    /// <summary>客户端连接事件</summary>
    event EventHandler<ClientInfo>? ClientConnected;

    /// <summary>客户端断开事件</summary>
    event EventHandler<ClientInfo>? ClientDisconnected;

    /// <summary>服务器错误事件</summary>
    event EventHandler<string>? ServerError;

    /// <summary>收到消息事件</summary>
    event EventHandler<NetworkMessage>? MessageReceived;

    /// <summary>启动服务器</summary>
    Task<bool> StartAsync(RoomSettings settings);

    /// <summary>停止服务器</summary>
    Task StopAsync();

    /// <summary>广播消息到所有客户端</summary>
    Task BroadcastAsync(NetworkMessage message);

    /// <summary>发送消息到指定客户端</summary>
    Task SendToClientAsync(string sessionId, NetworkMessage message);
}

/// <summary>
/// 服务器服务实现
/// 负责TCP连接管理、客户端认证、消息处理等功能
/// 支持IPv4和IPv6双栈监听
/// </summary>
public class ServerService : IServerService
{
    private readonly ILogger<ServerService> _logger;
    private TcpListener? _tcpListenerV4;
    private TcpListener? _tcpListenerV6;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new();
    private readonly ConcurrentDictionary<string, ClientInfo> _clientInfos = new();
    private readonly ConcurrentDictionary<string, DateTime> _bannedIps = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _failedAttempts = new();
    private RoomSettings? _settings;

    public bool IsRunning => _tcpListenerV4 != null || _tcpListenerV6 != null;
    public int ConnectedClientsCount => _clients.Count;
    public ObservableCollection<ClientInfo> ConnectedClients { get; } = [];

    public event EventHandler<ClientInfo>? ClientConnected;
    public event EventHandler<ClientInfo>? ClientDisconnected;
    public event EventHandler<string>? ServerError;
    public event EventHandler<NetworkMessage>? MessageReceived;

    public ServerService(ILogger<ServerService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 启动服务器
    /// 同时监听IPv4和IPv6地址
    /// </summary>
    public async Task<bool> StartAsync(RoomSettings settings)
    {
        if (IsRunning)
        {
            _logger.LogWarning("Server is already running");
            return false;
        }

        _settings = settings;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            // IPv4 监听器
            _tcpListenerV4 = new TcpListener(IPAddress.Any, settings.Port);
            _tcpListenerV4.Start();
            _logger.LogInformation("IPv4 server started, listening on port {Port}", settings.Port);

            // IPv6 监听器
            try
            {
                _tcpListenerV6 = new TcpListener(IPAddress.IPv6Any, settings.Port);
                _tcpListenerV6.Start();
                _logger.LogInformation("IPv6 server started, listening on port {Port}", settings.Port);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IPv6 not available, continuing with IPv4 only");
                _tcpListenerV6 = null;
            }

            // 启动客户端接受任务
            _ = Task.Run(() => AcceptClientsAsync(_tcpListenerV4, "IPv4", _cancellationTokenSource.Token), _cancellationTokenSource.Token);
            if (_tcpListenerV6 != null)
            {
                _ = Task.Run(() => AcceptClientsAsync(_tcpListenerV6, "IPv6", _cancellationTokenSource.Token), _cancellationTokenSource.Token);
            }

            // 启动封禁IP清理任务
            _ = Task.Run(() => CleanupBannedIpsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start server");
            ServerError?.Invoke(this, $"Failed to start server: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 停止服务器
    /// 断开所有客户端连接并释放资源
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _logger.LogInformation("Stopping server...");

        _cancellationTokenSource?.Cancel();

        // 关闭所有客户端连接
        foreach (var client in _clients.Values)
        {
            try
            {
                client.Close();
            }
            catch { }
        }

        _clients.Clear();
        _clientInfos.Clear();
        ConnectedClients.Clear();

        _tcpListenerV4?.Stop();
        _tcpListenerV4 = null;
        _tcpListenerV6?.Stop();
        _tcpListenerV6 = null;

        _logger.LogInformation("Server stopped");
    }

    /// <summary>
    /// 接受客户端连接的循环
    /// </summary>
    private async Task AcceptClientsAsync(TcpListener listener, string protocol, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && listener != null)
        {
            try
            {
                var tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);
                var endpoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                var clientIp = endpoint?.Address.ToString() ?? "Unknown";

                // 检查IP是否被封禁
                if (IsIpBanned(clientIp))
                {
                    _logger.LogWarning("Rejected connection from banned IP: {Ip} ({Protocol})", clientIp, protocol);
                    tcpClient.Close();
                    continue;
                }

                _ = Task.Run(() => HandleClientAsync(tcpClient, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting client connection ({Protocol})", protocol);
            }
        }
    }

    /// <summary>
    /// 处理客户端连接
    /// 包括握手认证、消息接收、心跳检测等
    /// </summary>
    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var endpoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
        var clientIp = endpoint?.Address.ToString() ?? "Unknown";
        string? sessionId = null;

        try
        {
            using var stream = tcpClient.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) { AutoFlush = true };

            // 等待握手消息，超时30秒
            var handshakeTimeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            var readTask = reader.ReadLineAsync(cancellationToken).AsTask();

            var completedTask = await Task.WhenAny(handshakeTimeoutTask, readTask);

            if (completedTask == handshakeTimeoutTask)
            {
                _logger.LogWarning("Client handshake timeout: {Ip}", clientIp);
                await SendResponseAsync(writer, new NetworkMessage(MessageType.Error, "Handshake timeout"));
                tcpClient.Close();
                return;
            }

            var handshakeLine = await readTask;
            if (string.IsNullOrEmpty(handshakeLine))
            {
                tcpClient.Close();
                return;
            }

            // 解析握手消息
            var handshakeMessage = JsonSerializer.Deserialize<NetworkMessage>(handshakeLine);
            if (handshakeMessage == null || handshakeMessage.Type != MessageType.Handshake)
            {
                _logger.LogWarning("Invalid handshake message from: {Ip}", clientIp);
                await SendResponseAsync(writer, new NetworkMessage(MessageType.Error, "Invalid handshake"));
                tcpClient.Close();
                return;
            }

            var handshakeData = JsonSerializer.Deserialize<HandshakeData>(handshakeMessage.Data ?? "{}");
            if (handshakeData == null)
            {
                await SendResponseAsync(writer, new NetworkMessage(MessageType.Error, "Invalid handshake data"));
                tcpClient.Close();
                return;
            }

            // 验证密码
            if (_settings != null && !string.IsNullOrEmpty(_settings.Password) && _settings.Password != handshakeData.Password)
            {
                _logger.LogWarning("Client password incorrect: {Ip}", clientIp);
                RecordFailedAttempt(clientIp);

                // 检测爆破行为
                if (_settings.EnableBruteForceProtection && IsBruteForceAttempt(clientIp))
                {
                    BanIp(clientIp);
                    _logger.LogWarning("Brute force detected, IP banned: {Ip}", clientIp);
                }

                await SendResponseAsync(writer, new NetworkMessage(MessageType.HandshakeResponse,
                    JsonSerializer.Serialize(new HandshakeResponseData { Success = false, ErrorMessage = "Incorrect password" })));
                tcpClient.Close();
                return;
            }

            // 检查最大连接数
            if (_settings != null && _clients.Count >= _settings.MaxConnections)
            {
                _logger.LogWarning("Max connections reached, rejecting: {Ip}", clientIp);
                await SendResponseAsync(writer, new NetworkMessage(MessageType.HandshakeResponse,
                    JsonSerializer.Serialize(new HandshakeResponseData { Success = false, ErrorMessage = "Room is full" })));
                tcpClient.Close();
                return;
            }

            // 清除失败的尝试记录
            ClearFailedAttempts(clientIp);

            // 创建客户端信息
            sessionId = Guid.NewGuid().ToString();
            var clientInfo = new ClientInfo
            {
                SessionId = sessionId,
                ComputerName = handshakeData.ComputerName,
                IpAddress = clientIp,
                ConnectTime = DateTime.Now,
                LastHeartbeat = DateTime.Now
            };

            _clients[sessionId] = tcpClient;
            _clientInfos[sessionId] = clientInfo;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ConnectedClients.Add(clientInfo);
            });

            _logger.LogInformation("Client connected: {ComputerName} ({Ip})", clientInfo.ComputerName, clientInfo.IpAddress);
            ClientConnected?.Invoke(this, clientInfo);

            // 发送握手成功响应
            await SendResponseAsync(writer, new NetworkMessage(MessageType.HandshakeResponse,
                JsonSerializer.Serialize(new HandshakeResponseData { Success = true, SessionId = sessionId })));

            // 启动心跳发送任务
            _ = Task.Run(() => SendHeartbeatAsync(sessionId, writer, cancellationToken), cancellationToken);

            // 消息接收循环
            while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
            {
                try
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrEmpty(line)) break;

                    var message = JsonSerializer.Deserialize<NetworkMessage>(line);
                    if (message == null) continue;

                    // 处理心跳响应
                    if (message.Type == MessageType.HeartbeatResponse)
                    {
                        if (_clientInfos.TryGetValue(sessionId, out var info))
                        {
                            info.LastHeartbeat = DateTime.Now;
                            if (message.Data != null && int.TryParse(message.Data, out var latency))
                            {
                                info.Latency = latency;
                            }
                        }
                    }
                    else
                    {
                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing client message: {SessionId}", sessionId);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client connection: {Ip}", clientIp);
        }
        finally
        {
            if (sessionId != null)
            {
                RemoveClient(sessionId);
            }
            else
            {
                tcpClient.Close();
            }
        }
    }

    /// <summary>
    /// 发送心跳消息到客户端
    /// </summary>
    private async Task SendHeartbeatAsync(string sessionId, StreamWriter writer, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(3000, cancellationToken);
                var sendTime = DateTime.UtcNow.Ticks;
                await SendResponseAsync(writer, new NetworkMessage(MessageType.Heartbeat, sendTime.ToString()));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 发送响应消息到客户端
    /// </summary>
    private static async Task SendResponseAsync(StreamWriter writer, NetworkMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        await writer.WriteLineAsync(json);
    }

    /// <summary>
    /// 移除客户端连接
    /// </summary>
    private void RemoveClient(string sessionId)
    {
        if (_clients.TryRemove(sessionId, out var client))
        {
            try
            {
                client.Close();
            }
            catch { }
        }

        if (_clientInfos.TryRemove(sessionId, out var clientInfo))
        {
            _ = Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ConnectedClients.Remove(clientInfo);
            });
            _logger.LogInformation("Client disconnected: {ComputerName} ({Ip})", clientInfo.ComputerName, clientInfo.IpAddress);
            ClientDisconnected?.Invoke(this, clientInfo);
        }
    }

    /// <summary>
    /// 检查IP是否被封禁
    /// </summary>
    private bool IsIpBanned(string ip)
    {
        if (_bannedIps.TryGetValue(ip, out var banEndTime))
        {
            if (DateTime.Now < banEndTime)
                return true;
            _bannedIps.TryRemove(ip, out _);
        }
        return false;
    }

    /// <summary>
    /// 封禁IP地址
    /// </summary>
    private void BanIp(string ip)
    {
        if (_settings == null) return;
        var banEndTime = DateTime.Now.AddMinutes(_settings.BruteForceBanDurationMinutes);
        _bannedIps[ip] = banEndTime;
    }

    /// <summary>
    /// 记录失败的连接尝试
    /// </summary>
    private void RecordFailedAttempt(string ip)
    {
        if (_settings == null || !_settings.EnableBruteForceProtection) return;

        var attempts = _failedAttempts.GetOrAdd(ip, _ => []);
        lock (attempts)
        {
            attempts.Add(DateTime.Now);
            var windowStart = DateTime.Now.AddMinutes(-_settings.FailedAttemptsWindowMinutes);
            attempts.RemoveAll(t => t < windowStart);
        }
    }

    /// <summary>
    /// 检测是否为爆破攻击
    /// </summary>
    private bool IsBruteForceAttempt(string ip)
    {
        if (_settings == null) return false;

        if (_failedAttempts.TryGetValue(ip, out var attempts))
        {
            lock (attempts)
            {
                return attempts.Count >= _settings.MaxFailedAttempts;
            }
        }
        return false;
    }

    /// <summary>
    /// 清除失败尝试记录
    /// </summary>
    private void ClearFailedAttempts(string ip)
    {
        _failedAttempts.TryRemove(ip, out _);
    }

    /// <summary>
    /// 定期清理过期的封禁IP
    /// </summary>
    private async Task CleanupBannedIpsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);

            var now = DateTime.Now;
            foreach (var kvp in _bannedIps)
            {
                if (kvp.Value < now)
                {
                    _bannedIps.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    /// <summary>
    /// 广播消息到所有客户端
    /// </summary>
    public async Task BroadcastAsync(NetworkMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        var tasks = new List<Task>();

        foreach (var kvp in _clients)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    if (kvp.Value.Connected)
                    {
                        var stream = kvp.Value.GetStream();
                        var writer = new StreamWriter(stream) { AutoFlush = true };
                        await writer.WriteLineAsync(json);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to broadcast message to client {SessionId}", kvp.Key);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 发送消息到指定客户端
    /// </summary>
    public async Task SendToClientAsync(string sessionId, NetworkMessage message)
    {
        if (!_clients.TryGetValue(sessionId, out var client) || !client.Connected)
        {
            _logger.LogWarning("Client {SessionId} not found or disconnected", sessionId);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(message);
            var stream = client.GetStream();
            var writer = new StreamWriter(stream) { AutoFlush = true };
            await writer.WriteLineAsync(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to client {SessionId}", sessionId);
        }
    }

    public void Dispose()
    {
        StopAsync().Wait();
        _cancellationTokenSource?.Dispose();
    }
}
