using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 获取到的Github Release信息
/// </summary>
public record ReleaseInfo
{
    /// <summary>
    /// 标签名称（版本号）。
    /// </summary>
    [JsonPropertyName("tag_name")] public string TagName { get; init; } = string.Empty;

    /// <summary>
    /// Release 描述正文。
    /// </summary>
    [JsonPropertyName("body")] public string Body { get; init; } = string.Empty;

    /// <summary>
    /// 附件资源列表。
    /// </summary>
    [JsonPropertyName("assets")] public AssetsInfo[] Assets { get; init; } = [];

    /// <summary>
    /// Release 名称。
    /// </summary>
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
}