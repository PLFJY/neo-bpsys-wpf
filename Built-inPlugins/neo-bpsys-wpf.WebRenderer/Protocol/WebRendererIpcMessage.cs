using System.Text.Json;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.WebRenderer.Protocol;

/// <summary>
/// Web Renderer 插件与 sidecar 之间传输的 IPC 消息信封。
/// </summary>
public sealed record WebRendererIpcMessage
{
    /// <summary>当前 IPC 协议版本。</summary>
    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }

    /// <summary>发送端单调递增的消息序号。</summary>
    [JsonPropertyName("sequence")]
    public required long Sequence { get; init; }

    /// <summary>消息类型。</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>消息负载。</summary>
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; init; }
}

/// <summary>
/// Web Renderer IPC 协议常量。
/// </summary>
public static class WebRendererIpcProtocol
{
    /// <summary>当前协议版本。</summary>
    public const int Version = 3;

    /// <summary>插件发送的主机元数据消息类型。</summary>
    public const string HostHello = "host.hello";

    /// <summary>sidecar 就绪消息类型。</summary>
    public const string SidecarReady = "sidecar.ready";

    /// <summary>心跳消息类型。</summary>
    public const string Heartbeat = "heartbeat";

    /// <summary>关闭消息类型。</summary>
    public const string Shutdown = "shutdown";

    /// <summary>错误消息类型。</summary>
    public const string Error = "error";

    /// <summary>插件发送的完整静态布局快照。</summary>
    public const string BootstrapReplace = "bootstrap.replace";

    /// <summary>sidecar 通知浏览器刷新布局快照。</summary>
    public const string BootstrapChanged = "bootstrap.changed";

    /// <summary>插件发送的完整运行时绑定快照。</summary>
    public const string RuntimeSnapshot = "runtime.snapshot";

    /// <summary>插件发送的增量绑定更新。</summary>
    public const string RuntimeBindingPatch = "runtime.bindingPatch";

    /// <summary>插件发送的运行时事件。</summary>
    public const string RuntimeEvent = "runtime.event";

    /// <summary>插件转发的只读前台行为语义事件。</summary>
    public const string BehaviorEvent = "behavior.event";

    /// <summary>sidecar 通知插件当前 WebSocket 客户端数量。</summary>
    public const string SidecarClientsChanged = "sidecar.clientsChanged";
}
