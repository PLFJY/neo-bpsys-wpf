using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Why a .bpui layout depends on a plugin.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedPluginDependencyReason
{
    /// <summary>
    /// The dependency source is unknown or was not provided by the package.
    /// </summary>
    Unknown,

    /// <summary>
    /// The package contains one or more controls whose <c>ControlType</c> belongs to this plugin.
    /// </summary>
    FrontedControl,

    /// <summary>
    /// The package contains one or more plugin window layouts for this plugin.
    /// </summary>
    FrontedWindow,

    /// <summary>
    /// The package needs this plugin for both plugin controls and plugin window layouts.
    /// </summary>
    Both
}
