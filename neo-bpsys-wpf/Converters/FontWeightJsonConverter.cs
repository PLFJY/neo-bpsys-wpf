using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 字体权重的Json转换器
/// </summary>
public class FontWeightJsonConverter : JsonConverter<FontWeight>
{
    private static readonly Dictionary<int, FontWeight> NumberMap = new Dictionary<int, FontWeight>
    {
        { 100, FontWeights.Thin },
        { 200, FontWeights.ExtraLight },
        { 300, FontWeights.Light },
        { 400, FontWeights.Normal },
        { 500, FontWeights.Medium },
        { 600, FontWeights.SemiBold },
        { 700, FontWeights.Bold },
        { 800, FontWeights.ExtraBold },
        { 900, FontWeights.Black }
    };
    private static readonly Dictionary<string, FontWeight> StringMap = new Dictionary<string, FontWeight>(StringComparer.OrdinalIgnoreCase)
    {
        { "Thin", FontWeights.Thin },
        { "ExtraLight", FontWeights.ExtraLight },
        { "Light", FontWeights.Light },
        { "Normal", FontWeights.Normal },
        { "Medium", FontWeights.Medium },
        { "SemiBold", FontWeights.SemiBold },
        { "Bold", FontWeights.Bold },
        { "ExtraBold", FontWeights.ExtraBold },
        { "Black", FontWeights.Black }
    };
    public override FontWeight Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return FontWeights.Normal;
            case JsonTokenType.String:
                {
                    var value = reader.GetString()!;
                    if (StringMap.TryGetValue(value, out var fw))
                        return fw;
                    throw new JsonException($"无效的字体权重字符串: {value}");
                }
            case JsonTokenType.Number:
                {
                    var value = reader.GetInt32();
                    if (NumberMap.TryGetValue(value, out var fw))
                        return fw;
                    throw new JsonException($"无效的字体权重数值: {value}");
                }
            default:
                throw new JsonException($"不支持的JSON token类型: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, FontWeight value, JsonSerializerOptions options)
    {
        var name = StringMap.FirstOrDefault(kvp => kvp.Value == value).Key
                   ?? throw new JsonException($"不支持的FontWeight值: {value}");
        writer.WriteStringValue(name);
    }
}