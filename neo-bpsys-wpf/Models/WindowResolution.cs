using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Models;

public class WindowResolution
{
    [JsonPropertyName("Width")]
    public int Width { get; set; }

    [JsonPropertyName("Height")]
    public int Height { get; set; }
}