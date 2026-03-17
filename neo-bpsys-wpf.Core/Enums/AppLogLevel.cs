namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 表示应用可切换的日志输出级别。
/// </summary>
public enum AppLogLevel
{
    /// <summary>
    /// 输出最详细的追踪信息。
    /// </summary>
    Verbose,

    /// <summary>
    /// 输出调试信息。
    /// </summary>
    Debug,

    /// <summary>
    /// 输出常规运行信息。
    /// </summary>
    Information,

    /// <summary>
    /// 仅输出警告及以上信息。
    /// </summary>
    Warning,

    /// <summary>
    /// 仅输出错误及以上信息。
    /// </summary>
    Error,

    /// <summary>
    /// 仅输出严重错误信息。
    /// </summary>
    Fatal
}
