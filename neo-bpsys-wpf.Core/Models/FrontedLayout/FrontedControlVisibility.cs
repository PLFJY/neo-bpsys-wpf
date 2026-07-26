using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 设计器 v3 控件可见性。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedControlVisibility
{
    Visible,
    Hidden,
    Collapsed
}
