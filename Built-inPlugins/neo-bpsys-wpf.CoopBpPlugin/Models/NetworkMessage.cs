using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.CoopBpPlugin.Models;

/// <summary>
/// 消息类型枚举
/// 定义网络通信中使用的各种消息类型
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageType
{
    /// <summary>握手请求</summary>
    Handshake,

    /// <summary>握手响应</summary>
    HandshakeResponse,

    /// <summary>心跳请求</summary>
    Heartbeat,

    /// <summary>心跳响应</summary>
    HeartbeatResponse,

    /// <summary>完整数据同步</summary>
    FullSync,

    /// <summary>游戏进度变化</summary>
    GameProgressChanged,

    /// <summary>队伍交换</summary>
    TeamSwapped,

    /// <summary>新游戏</summary>
    NewGame,

    /// <summary>角色选择</summary>
    CharacterPicked,

    /// <summary>角色禁用</summary>
    CharacterBanned,

    /// <summary>天赋变化</summary>
    TalentChanged,

    /// <summary>特质变化</summary>
    TraitChanged,

    /// <summary>地图选择</summary>
    MapPicked,

    /// <summary>地图禁用</summary>
    MapBanned,

    /// <summary>计时器启动</summary>
    TimerStart,

    /// <summary>计时器停止</summary>
    TimerStop,

    /// <summary>玩家数据变化</summary>
    PlayerDataChanged,

    /// <summary>全局禁用变化</summary>
    GlobalBanChanged,

    /// <summary>队员上场状态变化</summary>
    MemberOnFieldChanged,

    /// <summary>错误消息</summary>
    Error,

    /// <summary>断开连接</summary>
    Disconnect
}

/// <summary>
/// 网络消息类
/// 用于客户端和服务器之间的通信
/// </summary>
public class NetworkMessage
{
    /// <summary>消息类型</summary>
    public MessageType Type { get; set; }

    /// <summary>消息数据（JSON格式）</summary>
    public string? Data { get; set; }

    /// <summary>消息时间戳</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>默认构造函数</summary>
    public NetworkMessage() { }

    /// <summary>
    /// 创建网络消息
    /// </summary>
    /// <param name="type">消息类型</param>
    /// <param name="data">消息数据</param>
    public NetworkMessage(MessageType type, string? data = null)
    {
        Type = type;
        Data = data;
        Timestamp = DateTime.UtcNow;
    }
}
