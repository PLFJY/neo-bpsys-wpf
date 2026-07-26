using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 前台行为重入策略。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedReentryPolicy
{
    /// <summary>
    /// 中断前一个执行。
    /// </summary>
    InterruptPrevious,

    /// <summary>
    /// 如果正在运行则忽略。
    /// </summary>
    IgnoreIfRunning,

    /// <summary>
    /// 排队等待。
    /// </summary>
    Queue,

    /// <summary>
    /// 允许并行执行。
    /// </summary>
    AllowParallel
}
