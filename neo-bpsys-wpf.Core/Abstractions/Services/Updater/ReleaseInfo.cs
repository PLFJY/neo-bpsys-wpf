using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Abstractions.Services.Updater;


    public class ReleaseInfo
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = string.Empty;
        [JsonPropertyName("body")]
        public string Body { get; init; } = string.Empty;
        [JsonPropertyName("assets")]
        public AssetsInfo[] Assets { get; init; } = [];
    }