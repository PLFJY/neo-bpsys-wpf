namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// 插件状态改变事件参数
/// Plugin state changed event args
/// </summary>
public class PluginStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 插件ID
    /// Plugin ID
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// 旧状态
    /// Old state
    /// </summary>
    public PluginState OldState { get; init; }

    /// <summary>
    /// 新状态
    /// New state
    /// </summary>
    public PluginState NewState { get; init; }

    /// <summary>
    /// 错误消息（如果有）
    /// Error message (if any)
    /// </summary>
    public string? ErrorMessage { get; init; }
}
