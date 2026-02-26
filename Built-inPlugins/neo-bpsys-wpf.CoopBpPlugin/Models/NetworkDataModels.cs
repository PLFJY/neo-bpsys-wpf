using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.CoopBpPlugin.Models;

/// <summary>
/// 握手数据
/// 客户端连接时发送的初始数据
/// </summary>
public class HandshakeData
{
    /// <summary>计算机名称</summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>房间密码</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>客户端版本</summary>
    public string ClientVersion { get; set; } = "1.0.0.0";
}

/// <summary>
/// 握手响应数据
/// 服务器对客户端握手请求的响应
/// </summary>
public class HandshakeResponseData
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; }

    /// <summary>错误消息（失败时）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>会话ID（成功时）</summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// 客户端信息
/// 存储已连接客户端的详细信息
/// </summary>
public class ClientInfo
{
    /// <summary>会话ID（唯一标识）</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>计算机名称</summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>IP地址</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>连接时间</summary>
    public DateTime ConnectTime { get; set; }

    /// <summary>延迟（毫秒）</summary>
    public int Latency { get; set; }

    /// <summary>最后心跳时间</summary>
    public DateTime LastHeartbeat { get; set; }
}

/// <summary>
/// 房间设置
/// 创建房间时的配置参数
/// </summary>
public class RoomSettings
{
    /// <summary>监听端口</summary>
    public int Port { get; set; } = 9527;

    /// <summary>房间密码</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>最大连接数</summary>
    public int MaxConnections { get; set; } = 10;

    /// <summary>是否启用爆破保护</summary>
    public bool EnableBruteForceProtection { get; set; } = true;

    /// <summary>爆破封禁时长（分钟）</summary>
    public int BruteForceBanDurationMinutes { get; set; } = 30;

    /// <summary>最大失败尝试次数</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>失败尝试窗口时间（分钟）</summary>
    public int FailedAttemptsWindowMinutes { get; set; } = 5;
}

/// <summary>
/// 加入房间设置
/// 客户端连接服务器时的配置参数
/// </summary>
public class JoinRoomSettings
{
    /// <summary>服务器IP地址（支持IPv4和IPv6）</summary>
    public string ServerIp { get; set; } = string.Empty;

    /// <summary>服务器端口</summary>
    public int Port { get; set; } = 9527;

    /// <summary>房间密码</summary>
    public string Password { get; set; } = string.Empty;
}
