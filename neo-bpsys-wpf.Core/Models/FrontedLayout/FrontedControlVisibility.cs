using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Designer v3 control visibility.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedControlVisibility
{
    Visible,
    Hidden,
    Collapsed
}
