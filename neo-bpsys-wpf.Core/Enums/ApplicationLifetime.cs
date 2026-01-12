namespace neo_bpsys_wpf.Core.Enums;

public enum ApplicationLifetime
{
    /// <summary>
    /// 无
    /// </summary>
    None,
    /// <summary>
    /// 初始化应用（有 WPF,无应用主机）
    /// </summary>
    Initializing,
    /// <summary>
    /// 启动中，但设置未加载
    /// </summary>
    StartingOffline,
    /// <summary>
    /// 启动中，且设置已加载
    /// </summary>
    StartingOnline,
    /// <summary>
    /// 正常工作中
    /// </summary>
    Running,
    /// <summary>
    /// 停止应用中
    /// </summary>
    Stopping,
}