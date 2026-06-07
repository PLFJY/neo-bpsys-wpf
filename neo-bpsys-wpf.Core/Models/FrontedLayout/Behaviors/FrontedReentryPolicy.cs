using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedReentryPolicy
{
    InterruptPrevious,
    IgnoreIfRunning,
    Queue,
    AllowParallel
}
