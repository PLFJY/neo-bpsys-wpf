using System.Text.Json;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 当布局 JSON 中存在插件控件但其插件工厂尚未可用时使用的通用配置。
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
