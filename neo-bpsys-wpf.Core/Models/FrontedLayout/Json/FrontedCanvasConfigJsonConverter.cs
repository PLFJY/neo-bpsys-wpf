using System.Text.Json;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Json;

/// <summary>
/// 读取和写入 v3 root-level 控件结构的 Canvas 配置 converter。
/// </summary>
public class FrontedCanvasConfigJsonConverter : JsonConverter<FrontedCanvasConfig>
{
    private static readonly HashSet<string> ReservedPropertyNames =
    [
        nameof(FrontedCanvasConfig.Version),
        nameof(FrontedCanvasConfig.CanvasWidth),
        nameof(FrontedCanvasConfig.CanvasHeight),
        nameof(FrontedCanvasConfig.BackgroundImage)
    ];

    /// <inheritdoc />
    public override FrontedCanvasConfig Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FrontedLayoutConfigException("Fronted canvas config root must be a JSON object.");
        }

        var config = new FrontedCanvasConfig
        {
            Version = ReadRequiredInt(root, nameof(FrontedCanvasConfig.Version)),
            CanvasWidth = ReadRequiredDouble(root, nameof(FrontedCanvasConfig.CanvasWidth)),
            CanvasHeight = ReadRequiredDouble(root, nameof(FrontedCanvasConfig.CanvasHeight)),
            BackgroundImage = ReadOptionalString(root, nameof(FrontedCanvasConfig.BackgroundImage))
        };

        if (config.Version != 3)
        {
            throw new FrontedLayoutConfigException($"Unsupported fronted canvas config version: {config.Version}.");
        }

        foreach (var property in root.EnumerateObject())
        {
            if (ReservedPropertyNames.Contains(property.Name))
            {
                continue;
            }

            config.Controls[property.Name] = ReadControl(property.Name, property.Value, options);
        }

        return config;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, FrontedCanvasConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(FrontedCanvasConfig.Version), 3);
        writer.WriteNumber(nameof(FrontedCanvasConfig.CanvasWidth), value.CanvasWidth);
        writer.WriteNumber(nameof(FrontedCanvasConfig.CanvasHeight), value.CanvasHeight);

        if (value.BackgroundImage is null)
        {
            writer.WriteNull(nameof(FrontedCanvasConfig.BackgroundImage));
        }
        else
        {
            writer.WriteString(nameof(FrontedCanvasConfig.BackgroundImage), value.BackgroundImage);
        }

        foreach (var (name, control) in value.Controls)
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, control, control.GetType(), options);
        }

        writer.WriteEndObject();
    }

    private static FrontedControlConfigBase ReadControl(
        string controlName,
        JsonElement element,
        JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FrontedLayoutConfigException($"Control '{controlName}' must be a JSON object.");
        }

        if (!element.TryGetProperty(nameof(FrontedControlConfigBase.ControlType), out var controlTypeElement)
            || controlTypeElement.ValueKind != JsonValueKind.String)
        {
            throw new FrontedLayoutConfigException($"Control '{controlName}' is missing ControlType.");
        }

        var controlType = controlTypeElement.GetString();
        var json = element.GetRawText();
        try
        {
            return controlType switch
            {
                "Text" => JsonSerializer.Deserialize<TextFrontedControlConfig>(json, options)
                    ?? throw new FrontedLayoutConfigException($"Control '{controlName}' could not be read as Text."),
                "LocalizedText" => JsonSerializer.Deserialize<LocalizedTextControlConfig>(json, options)
                    ?? throw new FrontedLayoutConfigException(
                        $"Control '{controlName}' could not be read as LocalizedText."),
                "Image" => JsonSerializer.Deserialize<ImageFrontedControlConfig>(json, options)
                    ?? throw new FrontedLayoutConfigException($"Control '{controlName}' could not be read as Image."),
                "GlobalScoreRow" => JsonSerializer.Deserialize<GlobalScoreRowControlConfig>(json, options)
                    ?? throw new FrontedLayoutConfigException(
                        $"Control '{controlName}' could not be read as GlobalScoreRow."),
                "TalentTraitDisplay" => JsonSerializer.Deserialize<TalentTraitDisplayControlConfig>(json, options)
                    ?? throw new FrontedLayoutConfigException(
                        $"Control '{controlName}' could not be read as TalentTraitDisplay."),
                "GameProgressText" => JsonSerializer.Deserialize<GameProgressTextControlConfig>(json, options)
                    ?? throw new FrontedLayoutConfigException(
                        $"Control '{controlName}' could not be read as GameProgressText."),
                "MapNameText" => JsonSerializer.Deserialize<MapNameTextControlConfig>(json, options)
                    ?? throw new FrontedLayoutConfigException(
                        $"Control '{controlName}' could not be read as MapNameText."),
                _ => throw new FrontedLayoutConfigException(
                    $"Control '{controlName}' has unsupported ControlType '{controlType}'.")
            };
        }
        catch (JsonException ex)
        {
            throw new FrontedLayoutConfigException(
                $"Control '{controlName}' with ControlType '{controlType}' has invalid JSON shape.",
                ex);
        }
    }

    private static int ReadRequiredInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            throw new FrontedLayoutConfigException($"Missing required property '{propertyName}'.");
        }

        try
        {
            return property.GetInt32();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new FrontedLayoutConfigException($"Property '{propertyName}' must be a JSON number.", ex);
        }
    }

    private static double ReadRequiredDouble(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            throw new FrontedLayoutConfigException($"Missing required property '{propertyName}'.");
        }

        try
        {
            return property.GetDouble();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new FrontedLayoutConfigException($"Property '{propertyName}' must be a JSON number.", ex);
        }
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new FrontedLayoutConfigException($"Property '{propertyName}' must be a JSON string or null.");
        }

        return property.GetString();
    }
}
