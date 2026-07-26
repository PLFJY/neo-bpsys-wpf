using System.Text.Json;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 设置配置版本检测结果
/// </summary>
/// <param name="HasVersion">是否存在 Version 字段</param>
/// <param name="IsNullVersion">Version 是否为 null</param>
/// <param name="Version">Version 数值</param>
/// <param name="IsLegacy">是否为 legacy 配置</param>
public readonly record struct SettingsConfigVersionInfo(
    bool HasVersion,
    bool IsNullVersion,
    int? Version,
    bool IsLegacy);

/// <summary>
/// 设置配置版本检测工具
/// </summary>
public static class SettingsConfigVersionHelper
{
    /// <summary>
    /// 当前主设置配置版本
    /// </summary>
    public const int CurrentSettingsVersion = 3;

    /// <summary>
    /// 检测设置配置 JSON 字符串中的版本信息
    /// </summary>
    /// <param name="json">设置配置 JSON</param>
    public static SettingsConfigVersionInfo InspectJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return InspectRoot(document.RootElement);
    }

    /// <summary>
    /// 检测设置配置 JSON 根节点中的版本信息
    /// </summary>
    /// <param name="root">设置配置 JSON 根节点</param>
    public static SettingsConfigVersionInfo InspectRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Settings config root must be a JSON object.");
        }

        if (!TryGetPropertyCaseInsensitive(
                root,
                nameof(neo_bpsys_wpf.Core.Models.Settings.Version),
                out var versionElement))
        {
            return new SettingsConfigVersionInfo(false, false, null, true);
        }

        if (versionElement.ValueKind == JsonValueKind.Null)
        {
            return new SettingsConfigVersionInfo(true, true, null, true);
        }

        if (versionElement.ValueKind == JsonValueKind.Number
            && versionElement.TryGetInt32(out var version))
        {
            return new SettingsConfigVersionInfo(true, false, version, false);
        }

        return new SettingsConfigVersionInfo(true, false, null, false);
    }

    private static bool TryGetPropertyCaseInsensitive(
        JsonElement root,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
