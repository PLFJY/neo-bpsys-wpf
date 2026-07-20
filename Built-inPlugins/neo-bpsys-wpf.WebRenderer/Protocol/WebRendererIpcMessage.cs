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
    public const int Version = 6;

    /// <summary>主程序广播的权威会话状态。</summary>
    public const string SessionState = "session.state";

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

    /// <summary>sidecar 已原子应用 bootstrap 的确认消息。</summary>
    public const string BootstrapApplied = "bootstrap.applied";

    /// <summary>bootstrap 构建失败消息。</summary>
    public const string BootstrapFailed = "bootstrap.failed";

    /// <summary>sidecar 拒绝无效 bootstrap 的消息。</summary>
    public const string BootstrapRejected = "bootstrap.rejected";

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

    /// <summary>插件要求页面准备执行 Transition Exit 图。</summary>
    public const string TransitionPrepare = "transition.prepare";
    /// <summary>唯一的 C# commit 完成，页面可以执行 Enter 图。</summary>
    public const string TransitionCommitted = "transition.committed";
    /// <summary>取消当前页面过渡。</summary>
    public const string TransitionCancel = "transition.cancel";
    /// <summary>页面确认 Exit 图完成。</summary>
    public const string TransitionExitCompleted = "transition.exitCompleted";
    /// <summary>页面确认 Enter 图完成。</summary>
    public const string TransitionEnterCompleted = "transition.enterCompleted";
}

/// <summary>Web Renderer 主程序与 sidecar 共享的权威生命周期状态。</summary>
public enum WebRendererLifecycleState
{
    /// <summary>未启动。</summary>
    Stopped,
    /// <summary>正在启动 sidecar 进程。</summary>
    StartingProcess,
    /// <summary>正在等待命名管道。</summary>
    WaitingForPipe,
    /// <summary>命名管道已经连接。</summary>
    PipeConnected,
    /// <summary>正在构建真实布局 bootstrap。</summary>
    BuildingBootstrap,
    /// <summary>正在等待 sidecar 应用确认。</summary>
    WaitingForBootstrapAck,
    /// <summary>sidecar 已确认当前 bootstrap。</summary>
    Ready,
    /// <summary>正在停止。</summary>
    Stopping,
    /// <summary>会话发生可诊断故障。</summary>
    Faulted
}

/// <summary>主程序发送给 sidecar 的权威状态投影。</summary>
public sealed record WebRendererSessionState(WebRendererLifecycleState State, long Generation,
    string? ErrorCode = null, string? ErrorMessage = null);

/// <summary>sidecar 对完整 bootstrap 的应用确认。</summary>
public sealed record WebRendererBootstrapApplied(long Generation, int WindowCount,
    int RenderableWindowCount, string ActivePackageId);

/// <summary>bootstrap 构建或校验失败的结构化说明。</summary>
public sealed record WebRendererBootstrapFailure(long Generation, string Code, string Message);
