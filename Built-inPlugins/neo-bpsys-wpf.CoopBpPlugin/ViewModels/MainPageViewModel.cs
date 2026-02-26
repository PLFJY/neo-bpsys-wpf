using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.CoopBpPlugin.Models;
using neo_bpsys_wpf.CoopBpPlugin.Services;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.CoopBpPlugin.ViewModels;

/// <summary>
/// 连接模式枚举
/// </summary>
public enum ConnectionMode
{
    /// <summary>未连接</summary>
    None,

    /// <summary>主机模式</summary>
    Host,

    /// <summary>客户端模式</summary>
    Client
}

/// <summary>
/// 主页面视图模型
/// 管理房间创建、加入、客户端列表等UI逻辑
/// </summary>
public partial class MainPageViewModel : ViewModelBase
{
    private readonly ILogger<MainPageViewModel> _logger;
    private readonly IServerService _serverService;
    private readonly IClientService _clientService;
    private readonly IDataSyncService _dataSyncService;
    private readonly PluginSettings _settings;

    /// <summary>当前连接模式</summary>
    [ObservableProperty]
    private ConnectionMode _currentMode = ConnectionMode.None;

    /// <summary>是否为主机模式</summary>
    [ObservableProperty]
    private bool _isHostMode;

    /// <summary>是否为客户端模式</summary>
    [ObservableProperty]
    private bool _isClientMode;

    /// <summary>是否已连接</summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>状态消息</summary>
    [ObservableProperty]
    private string _statusMessage = "未连接";

    /// <summary>主机端口</summary>
    [ObservableProperty]
    private int _hostPort = 9527;

    /// <summary>主机密码</summary>
    [ObservableProperty]
    private string _hostPassword = string.Empty;

    /// <summary>最大连接数</summary>
    [ObservableProperty]
    private int _maxConnections = 10;

    /// <summary>是否启用爆破保护</summary>
    [ObservableProperty]
    private bool _enableBruteForceProtection = true;

    /// <summary>客户端-服务器IP</summary>
    [ObservableProperty]
    private string _clientServerIp = string.Empty;

    /// <summary>客户端-服务器端口</summary>
    [ObservableProperty]
    private int _clientPort = 9527;

    /// <summary>客户端-密码</summary>
    [ObservableProperty]
    private string _clientPassword = string.Empty;

    /// <summary>客户端延迟</summary>
    [ObservableProperty]
    private int _clientLatency;

    /// <summary>本地IP地址</summary>
    [ObservableProperty]
    private string _localIpAddress = "获取中...";

    /// <summary>已连接客户端列表</summary>
    public ObservableCollection<ClientInfo> ConnectedClients { get; }

    public MainPageViewModel(
        ILogger<MainPageViewModel> logger,
        IServerService serverService,
        IClientService clientService,
        IDataSyncService dataSyncService,
        PluginSettings settings)
    {
        _logger = logger;
        _serverService = serverService;
        _clientService = clientService;
        _dataSyncService = dataSyncService;
        _settings = settings;

        ConnectedClients = _serverService.ConnectedClients;

        // 从设置中加载上次使用的值
        HostPort = _settings.DefaultPort;
        MaxConnections = _settings.MaxConnections;
        EnableBruteForceProtection = _settings.EnableBruteForceProtection;
        ClientServerIp = _settings.LastConnectedIp;
        ClientPort = _settings.LastConnectedPort;

        // 订阅服务器事件
        _serverService.ClientConnected += OnServerClientConnected;
        _serverService.ClientDisconnected += OnServerClientDisconnected;
        _serverService.ServerError += OnServerError;

        // 订阅客户端事件
        _clientService.Connected += OnClientServiceConnected;
        _clientService.Disconnected += OnClientServiceDisconnected;
        _clientService.ConnectionError += OnConnectionError;

        // 获取本地IP地址
        UpdateLocalIpAddress();
    }

    /// <summary>
    /// 获取本地IP地址
    /// 优先获取IPv4地址用于显示
    /// </summary>
    private void UpdateLocalIpAddress()
    {
        try
        {
            // 获取所有网络接口的IP地址
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ipv4Addresses = host.AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                .ToList();
            var ipv6Addresses = host.AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6 && !ip.IsIPv6LinkLocal)
                .ToList();

            if (ipv4Addresses.Any())
            {
                LocalIpAddress = string.Join(", ", ipv4Addresses.Select(ip => ip.ToString()));
            }
            else if (ipv6Addresses.Any())
            {
                LocalIpAddress = string.Join(", ", ipv6Addresses.Select(ip => ip.ToString()));
            }
            else
            {
                LocalIpAddress = "无法获取";
            }
        }
        catch
        {
            LocalIpAddress = "无法获取";
        }
    }

    /// <summary>
    /// 创建房间命令
    /// </summary>
    [RelayCommand]
    private async Task CreateRoomAsync()
    {
        // 密码为空时确认
        if (string.IsNullOrEmpty(HostPassword))
        {
            var confirm = await MessageBoxHelper.ShowConfirmAsync(
                "未设置房间密码，存在安全风险。确定要继续吗？",
                "安全警告",
                "继续创建",
                "取消");
            if (!confirm) return;
        }

        // 禁用爆破保护时确认
        if (!EnableBruteForceProtection)
        {
            var confirm = await MessageBoxHelper.ShowConfirmAsync(
                "禁用爆破识别可能导致房间被恶意攻击。确定要禁用吗？",
                "安全警告",
                "确定禁用",
                "取消");
            if (!confirm)
            {
                EnableBruteForceProtection = true;
            }
        }

        var roomSettings = new RoomSettings
        {
            Port = HostPort,
            Password = HostPassword,
            MaxConnections = MaxConnections,
            EnableBruteForceProtection = EnableBruteForceProtection,
            BruteForceBanDurationMinutes = _settings.BruteForceBanDurationMinutes,
            MaxFailedAttempts = _settings.MaxFailedAttempts,
            FailedAttemptsWindowMinutes = _settings.FailedAttemptsWindowMinutes
        };

        var success = await _serverService.StartAsync(roomSettings);

        if (success)
        {
            CurrentMode = ConnectionMode.Host;
            IsHostMode = true;
            IsClientMode = false;
            IsConnected = true;
            StatusMessage = $"房间已创建，监听端口 {HostPort}";
            _dataSyncService.StartServerSync();
            _logger.LogInformation("Room created successfully, port: {Port}", HostPort);

            // 保存设置
            _settings.DefaultPort = HostPort;
            _settings.MaxConnections = MaxConnections;
            _settings.EnableBruteForceProtection = EnableBruteForceProtection;
        }
        else
        {
            await MessageBoxHelper.ShowErrorAsync("创建房间失败，请检查端口是否被占用", "创建失败");
        }
    }

    /// <summary>
    /// 关闭房间命令
    /// </summary>
    [RelayCommand]
    private async Task CloseRoomAsync()
    {
        _dataSyncService.StopServerSync();
        await _serverService.StopAsync();

        CurrentMode = ConnectionMode.None;
        IsHostMode = false;
        IsClientMode = false;
        IsConnected = false;
        StatusMessage = "房间已关闭";
        _logger.LogInformation("Room closed");
    }

    /// <summary>
    /// 加入房间命令
    /// </summary>
    [RelayCommand]
    private async Task JoinRoomAsync()
    {
        if (string.IsNullOrWhiteSpace(ClientServerIp))
        {
            await MessageBoxHelper.ShowErrorAsync("请输入服务器IP地址", "输入错误");
            return;
        }

        var joinSettings = new JoinRoomSettings
        {
            ServerIp = ClientServerIp,
            Port = ClientPort,
            Password = ClientPassword
        };

        StatusMessage = "正在连接...";
        var success = await _clientService.ConnectAsync(joinSettings);

        if (success)
        {
            CurrentMode = ConnectionMode.Client;
            IsHostMode = false;
            IsClientMode = true;
            IsConnected = true;
            StatusMessage = $"已连接到 {ClientServerIp}:{ClientPort}";
            _dataSyncService.StartClientSync();
            _logger.LogInformation("Connected to server: {Ip}:{Port}", ClientServerIp, ClientPort);

            // 保存设置
            _settings.LastConnectedIp = ClientServerIp;
            _settings.LastConnectedPort = ClientPort;
        }
    }

    /// <summary>
    /// 离开房间命令
    /// </summary>
    [RelayCommand]
    private async Task LeaveRoomAsync()
    {
        _dataSyncService.StopClientSync();
        await _clientService.DisconnectAsync();

        CurrentMode = ConnectionMode.None;
        IsHostMode = false;
        IsClientMode = false;
        IsConnected = false;
        ClientLatency = 0;
        StatusMessage = "已断开连接";
        _logger.LogInformation("Disconnected from server");
    }

    /// <summary>
    /// 踢出客户端命令
    /// </summary>
    [RelayCommand]
    private async Task KickClientAsync(ClientInfo? client)
    {
        if (client == null) return;

        var confirm = await MessageBoxHelper.ShowConfirmAsync(
            $"确定要将 {client.ComputerName} ({client.IpAddress}) 踢出房间吗？",
            "确认踢出",
            "确定",
            "取消");

        if (confirm)
        {
            await _serverService.SendToClientAsync(client.SessionId, new NetworkMessage(MessageType.Disconnect));
            _logger.LogInformation("Kicked client: {ComputerName} ({Ip})", client.ComputerName, client.IpAddress);
        }
    }

    /// <summary>
    /// 服务器客户端连接事件处理
    /// </summary>
    private void OnServerClientConnected(object? sender, ClientInfo client)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusMessage = $"房间运行中，{ConnectedClients.Count} 人在线";
        });
    }

    /// <summary>
    /// 服务器客户端断开事件处理
    /// </summary>
    private void OnServerClientDisconnected(object? sender, ClientInfo client)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusMessage = $"房间运行中，{ConnectedClients.Count} 人在线";
        });
    }

    /// <summary>
    /// 客户端连接成功事件处理
    /// </summary>
    private void OnClientServiceConnected(object? sender, EventArgs e)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusMessage = $"已连接到 {ClientServerIp}:{ClientPort}";
        });
    }

    /// <summary>
    /// 客户端断开连接事件处理
    /// </summary>
    private void OnClientServiceDisconnected(object? sender, EventArgs e)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (CurrentMode == ConnectionMode.Client)
            {
                CurrentMode = ConnectionMode.None;
                IsHostMode = false;
                IsClientMode = false;
                IsConnected = false;
                StatusMessage = "连接已断开";
            }
        });
    }

    /// <summary>
    /// 服务器错误事件处理
    /// </summary>
    private void OnServerError(object? sender, string error)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await MessageBoxHelper.ShowErrorAsync(error, "服务器错误");
        });
    }

    /// <summary>
    /// 连接错误事件处理
    /// </summary>
    private void OnConnectionError(object? sender, string error)
    {
        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await MessageBoxHelper.ShowErrorAsync(error, "连接失败");
            CurrentMode = ConnectionMode.None;
            IsHostMode = false;
            IsClientMode = false;
            IsConnected = false;
            StatusMessage = "连接失败";
        });
    }

    /// <summary>
    /// 更新延迟显示
    /// </summary>
    public void UpdateLatency()
    {
        if (CurrentMode == ConnectionMode.Client)
        {
            ClientLatency = _clientService.Latency;
        }
    }

    /// <summary>
    /// 手动发送完整同步数据包命令
    /// </summary>
    [RelayCommand]
    private async Task SendFullSyncAsync()
    {
        if (CurrentMode == ConnectionMode.Host)
        {
            await _dataSyncService.RequestFullSyncAsync();
            StatusMessage = $"已发送完整同步数据包 - {DateTime.Now:HH:mm:ss}";
            _logger.LogInformation("Manual full sync sent");
        }
        else if (CurrentMode == ConnectionMode.Client)
        {
            // 客户端请求服务端发送完整同步
            var message = new NetworkMessage(MessageType.FullSync);
            await _clientService.SendAsync(message);
            StatusMessage = $"已请求完整同步 - {DateTime.Now:HH:mm:ss}";
            _logger.LogInformation("Manual full sync request sent");
        }
    }

    /// <summary>
    /// 手动发送测试消息命令
    /// </summary>
    [RelayCommand]
    private async Task SendTestMessageAsync()
    {
        var testMessage = new NetworkMessage(MessageType.Error, $"Test message at {DateTime.Now:HH:mm:ss}");

        if (CurrentMode == ConnectionMode.Host)
        {
            await _serverService.BroadcastAsync(testMessage);
            StatusMessage = $"已广播测试消息 - {DateTime.Now:HH:mm:ss}";
            _logger.LogInformation("Test message broadcasted");
        }
        else if (CurrentMode == ConnectionMode.Client)
        {
            await _clientService.SendAsync(testMessage);
            StatusMessage = $"已发送测试消息 - {DateTime.Now:HH:mm:ss}";
            _logger.LogInformation("Test message sent");
        }
    }
}
