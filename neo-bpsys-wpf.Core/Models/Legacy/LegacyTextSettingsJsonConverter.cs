using neo_bpsys_wpf.Core.Converters;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace neo_bpsys_wpf.Core.Models.Legacy;

/// <summary>
/// 容错读取旧版 TextSettings，隔离单个字段的格式错误，避免其丢弃整个 legacy Config。
/// </summary>
public sealed class LegacyTextSettingsJsonConverter : JsonConverter<LegacyTextSettings>
{
    /// <inheritdoc />
    public override LegacyTextSettings? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Legacy TextSettings must be an object.");
        }

        var result = new LegacyTextSettings();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.NameEquals("Color"))
            {
                if (property.Value.ValueKind == JsonValueKind.String) result.Color = property.Value.GetString();
                else result.InvalidFields.Add(property.Name);
            }
            else if (property.NameEquals("FontFamilySite"))
            {
                if (property.Value.ValueKind == JsonValueKind.String) result.FontFamilySite = property.Value.GetString();
                else result.InvalidFields.Add(property.Name);
            }
            else if (property.NameEquals("FontSize"))
            {
                if (property.Value.TryGetDouble(out var fontSize)) result.FontSize = fontSize;
                else result.InvalidFields.Add(property.Name);
            }
            else if (property.NameEquals("FontWeight"))
            {
                if (FontWeightJsonConverter.TryParseLegacy(property.Value, out var weight)) result.FontWeight = weight;
                else result.InvalidFields.Add(property.Name);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LegacyTextSettings value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Color is not null) writer.WriteString(nameof(LegacyTextSettings.Color), value.Color);
        if (value.FontFamilySite is not null) writer.WriteString(nameof(LegacyTextSettings.FontFamilySite), value.FontFamilySite);
        writer.WriteString(nameof(LegacyTextSettings.FontWeight), value.FontWeight.ToString());
        writer.WriteNumber(nameof(LegacyTextSettings.FontSize), value.FontSize);
        writer.WriteEndObject();
    }
}
