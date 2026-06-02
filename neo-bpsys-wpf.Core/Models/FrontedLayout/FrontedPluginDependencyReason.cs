using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Why a .bpui layout depends on a plugin.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedPluginDependencyReason
{
    Unknown,
    FrontedControl,
    FrontedWindow,
    Both
}
