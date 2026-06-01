using System.Text.Json;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Generic config used when a plugin control is present in layout JSON before its plugin factory is available.
/// </summary>
public class PluginFrontedControlConfig : FrontedControlConfigBase
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];

    [JsonIgnore]
    public string? PackageId => FrontedPluginControlType.TryParse(ControlType, out var parsed)
        ? parsed.PackageId
        : null;

    [JsonIgnore]
    public string? ControlTypeName => FrontedPluginControlType.TryParse(ControlType, out var parsed)
        ? parsed.ControlTypeName
        : null;
}
