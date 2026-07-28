using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace neo_bpsys_wpf.Core.Converters;

/// <summary>
/// JSON converter for WPF FontWeight values in legacy settings.
/// </summary>
public sealed class FontWeightJsonConverter : JsonConverter<FontWeight>
{
    private static readonly Dictionary<int, FontWeight> NumberMap = new()
    {
        [100] = FontWeights.Thin,
        [200] = FontWeights.ExtraLight,
        [300] = FontWeights.Light,
        [400] = FontWeights.Normal,
        [500] = FontWeights.Medium,
        [600] = FontWeights.SemiBold,
        [700] = FontWeights.Bold,
        [800] = FontWeights.ExtraBold,
        [900] = FontWeights.Black
    };

    private static readonly Dictionary<string, FontWeight> StringMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Thin"] = FontWeights.Thin,
        ["ExtraLight"] = FontWeights.ExtraLight,
        ["Light"] = FontWeights.Light,
        ["Normal"] = FontWeights.Normal,
        ["Medium"] = FontWeights.Medium,
        ["SemiBold"] = FontWeights.SemiBold,
        ["Bold"] = FontWeights.Bold,
        ["ExtraBold"] = FontWeights.ExtraBold,
        ["Black"] = FontWeights.Black
    };

    /// <summary>
    /// 从 JSON 读取 <see cref="FontWeight"/> 值，支持数字和字符串两种格式。
    /// </summary>
    /// <param name="reader">JSON 读取器</param>
    /// <param name="typeToConvert">目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>解析后的 <see cref="FontWeight"/> 值</returns>
    /// <exception cref="JsonException">JSON 令牌格式不支持</exception>
    public override FontWeight Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => FontWeights.Normal,
            JsonTokenType.String => ReadString(reader.GetString()),
            JsonTokenType.Number => ReadNumber(reader.GetInt32()),
            _ => throw new JsonException($"Unsupported FontWeight JSON token: {reader.TokenType}")
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, FontWeight value, JsonSerializerOptions options)
    {
        var name = StringMap.FirstOrDefault(item => item.Value == value).Key;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new JsonException($"Unsupported FontWeight value: {value}");
        }

        writer.WriteStringValue(name);
    }

    /// <summary>
    /// 尝试解析 2.x 实际序列化的 FontWeight 值。
    /// </summary>
    /// <param name="element">JSON 值。</param>
    /// <param name="fontWeight">解析后的字重。</param>
    /// <returns>是否为已知的旧版字重表示。</returns>
    public static bool TryParseLegacy(JsonElement element, out FontWeight fontWeight)
    {
        if (element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out var number)
            && NumberMap.TryGetValue(number, out fontWeight))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (!string.IsNullOrWhiteSpace(text)
                && (StringMap.TryGetValue(text, out fontWeight)
                    || (int.TryParse(text, out number) && NumberMap.TryGetValue(number, out fontWeight))))
            {
                return true;
            }
        }

        fontWeight = default;
        return false;
    }

    private static FontWeight ReadString(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && StringMap.TryGetValue(value, out var fontWeight))
        {
            return fontWeight;
        }

        throw new JsonException($"Invalid FontWeight string: {value}");
    }

    private static FontWeight ReadNumber(int value)
    {
        if (NumberMap.TryGetValue(value, out var fontWeight))
        {
            return fontWeight;
        }

        throw new JsonException($"Invalid FontWeight number: {value}");
    }
}
