namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// 插件状态枚举
/// Plugin state enumeration
/// </summary>
public enum PluginState
{
    /// <summary>
    /// 未加载
    /// Not loaded
    /// </summary>
    NotLoaded,

    /// <summary>
    /// 已加载但未启动
    /// Loaded but not started
    /// </summary>
    Loaded,

    /// <summary>
    /// 正在初始化
    /// Initializing
    /// </summary>
    Initializing,

    /// <summary>
    /// 已启动运行中
    /// Running
    /// </summary>
    Running,

    /// <summary>
    /// 已停止
    /// Stopped
    /// </summary>
    Stopped,

    /// <summary>
    /// 错误状态
    /// Error state
    /// </summary>
    Error,

    /// <summary>
    /// 已卸载
    /// Unloaded
    /// </summary>
    Unloaded
}
