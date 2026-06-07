using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedLoopStopMode
{
    StopImmediately,
    CompleteCurrentIteration,
    RunStopGraph,
    HoldCurrentState
}
