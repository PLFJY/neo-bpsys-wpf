namespace neo_bpsys_wpf.Core.Plugins.Events;

/// <summary>
/// 插件事件接口
/// </summary>
public interface IPluginEvent
{
    /// <summary>
    /// 事件发生时间
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// 事件源插件ID（可选）
    /// </summary>
    string? SourcePluginId { get; }
}

/// <summary>
/// 插件事件基类
/// </summary>
public abstract class PluginEventBase : IPluginEvent
{
    /// <inheritdoc/>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <inheritdoc/>
    public string? SourcePluginId { get; init; }
}

/// <summary>
/// 可取消的插件事件接口
/// </summary>
public interface ICancellablePluginEvent : IPluginEvent
{
    /// <summary>
    /// 是否已取消
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// 取消事件
    /// </summary>
    void Cancel();
}

/// <summary>
/// 可取消的插件事件基类
/// </summary>
public abstract class CancellablePluginEventBase : PluginEventBase, ICancellablePluginEvent
{
    /// <inheritdoc/>
    public bool IsCancelled { get; private set; }

    /// <inheritdoc/>
    public void Cancel() => IsCancelled = true;
}
