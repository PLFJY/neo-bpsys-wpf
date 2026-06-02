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

    public override void Write(Utf8JsonWriter writer, FontWeight value, JsonSerializerOptions options)
    {
        var name = StringMap.FirstOrDefault(item => item.Value == value).Key;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new JsonException($"Unsupported FontWeight value: {value}");
        }

        writer.WriteStringValue(name);
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
