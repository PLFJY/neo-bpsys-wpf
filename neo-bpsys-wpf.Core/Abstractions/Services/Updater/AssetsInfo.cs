using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Abstractions.Services.Updater;

public class AssetsInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;
}