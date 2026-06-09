using System.Text.Json;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Json;

/// <summary>
/// Reads and writes window-centric canvas settings, including typed BO state controls.
/// </summary>
public sealed class FrontedCanvasSettingsJsonConverter : JsonConverter<FrontedCanvasSettings>
{
    /// <inheritdoc />
    public override FrontedCanvasSettings Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FrontedLayoutConfigException("CanvasSettings must be a JSON object.");
        }

        var settings = new FrontedCanvasSettings
        {
            CanvasWidth = ReadOptionalDouble(root, nameof(FrontedCanvasSettings.CanvasWidth), 1440D),
            CanvasHeight = ReadOptionalDouble(root, nameof(FrontedCanvasSettings.CanvasHeight), 810D),
            BackgroundImage = ReadOptionalString(root, nameof(FrontedCanvasSettings.BackgroundImage)),
            EnableBoModeStates = ReadOptionalBoolean(root, nameof(FrontedCanvasSettings.EnableBoModeStates)),
            BoModeStates = ReadBoModeStates(root, options)
        };

        return settings;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        FrontedCanvasSettings value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(FrontedCanvasSettings.CanvasWidth), value.CanvasWidth);
        writer.WriteNumber(nameof(FrontedCanvasSettings.CanvasHeight), value.CanvasHeight);
        if (value.BackgroundImage is null)
        {
            writer.WriteNull(nameof(FrontedCanvasSettings.BackgroundImage));
        }
        else
        {
            writer.WriteString(nameof(FrontedCanvasSettings.BackgroundImage), value.BackgroundImage);
        }

        writer.WriteBoolean(nameof(FrontedCanvasSettings.EnableBoModeStates), value.EnableBoModeStates);
        writer.WritePropertyName(nameof(FrontedCanvasSettings.BoModeStates));
        WriteBoModeStates(writer, value.BoModeStates, options);
        writer.WriteEndObject();
    }

    private static Dictionary<string, FrontedCanvasStateConfig> ReadBoModeStates(
        JsonElement root,
        JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(nameof(FrontedCanvasSettings.BoModeStates), out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new FrontedLayoutConfigException("CanvasSettings.BoModeStates must be a JSON object or null.");
        }

        var states = new Dictionary<string, FrontedCanvasStateConfig>(StringComparer.Ordinal);
        foreach (var stateProperty in property.EnumerateObject())
        {
            if (stateProperty.Value.ValueKind != JsonValueKind.Object)
            {
                throw new FrontedLayoutConfigException($"BO mode state '{stateProperty.Name}' must be a JSON object.");
            }

            var state = new FrontedCanvasStateConfig
            {
                BackgroundImage = ReadOptionalString(
                    stateProperty.Value,
                    nameof(FrontedCanvasStateConfig.BackgroundImage)),
                RequiredPlugins = ReadOptionalList<FrontedPluginDependency>(
                    stateProperty.Value,
                    nameof(FrontedCanvasStateConfig.RequiredPlugins),
                    options)
            };

            if (stateProperty.Value.TryGetProperty(nameof(FrontedCanvasStateConfig.Controls), out var controlsElement))
            {
                if (controlsElement.ValueKind is not JsonValueKind.Object and not JsonValueKind.Null)
                {
                    throw new FrontedLayoutConfigException(
                        $"CanvasSettings.BoModeStates.{stateProperty.Name}.Controls must be a JSON object or null.");
                }

                if (controlsElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var controlProperty in controlsElement.EnumerateObject())
                    {
                        state.Controls[controlProperty.Name] = FrontedCanvasConfigJsonConverter.ReadControl(
                            controlProperty.Name,
                            controlProperty.Value,
                            options);
                    }
                }
            }

            states[stateProperty.Name] = state;
        }

        return states;
    }

    private static void WriteBoModeStates(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, FrontedCanvasStateConfig> states,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (stateName, state) in states)
        {
            writer.WritePropertyName(stateName);
            writer.WriteStartObject();
            if (state.BackgroundImage is null)
            {
                writer.WriteNull(nameof(FrontedCanvasStateConfig.BackgroundImage));
            }
            else
            {
                writer.WriteString(nameof(FrontedCanvasStateConfig.BackgroundImage), state.BackgroundImage);
            }

            writer.WritePropertyName(nameof(FrontedCanvasStateConfig.RequiredPlugins));
            JsonSerializer.Serialize(writer, state.RequiredPlugins, options);
            writer.WritePropertyName(nameof(FrontedCanvasStateConfig.Controls));
            writer.WriteStartObject();
            foreach (var (name, control) in state.Controls)
            {
                writer.WritePropertyName(name);
                JsonSerializer.Serialize(writer, control, control.GetType(), options);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static double ReadOptionalDouble(JsonElement root, string propertyName, double fallback)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetDouble()
            : fallback;
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;
    }

    private static bool ReadOptionalBoolean(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property)
               && property.ValueKind != JsonValueKind.Null
               && property.GetBoolean();
    }

    private static List<T> ReadOptionalList<T>(
        JsonElement root,
        string propertyName,
        JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<T>>(property.GetRawText(), options) ?? [];
    }
}
