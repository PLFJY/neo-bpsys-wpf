using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// Loop 行为停止模式。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedLoopStopMode
{
    /// <summary>
    /// 立即停止。
    /// </summary>
    StopImmediately,

    /// <summary>
    /// 完成当前迭代后停止。
    /// </summary>
    CompleteCurrentIteration,

    /// <summary>
    /// 运行停止图后停止。
    /// </summary>
    RunStopGraph,

    /// <summary>
    /// 保持当前状态。
    /// </summary>
    HoldCurrentState
}
