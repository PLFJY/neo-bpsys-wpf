using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedBehaviorKind
{
    /// <summary>
    /// Runs after a matching event has already happened.
    /// </summary>
    OneShot,

    /// <summary>
    /// Starts and stops according to matching lifecycle triggers.
    /// </summary>
    Loop,

    /// <summary>
    /// Runs an exit graph before a business state change and an enter graph after the change is committed.
    /// </summary>
    Transition
}
