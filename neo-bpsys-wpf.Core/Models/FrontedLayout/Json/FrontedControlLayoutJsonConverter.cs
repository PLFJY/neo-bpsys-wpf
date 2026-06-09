using System.Text.Json;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Json;

/// <summary>
/// Reads and writes the window-centric v3 control layout object.
/// </summary>
public sealed class FrontedControlLayoutJsonConverter : JsonConverter<FrontedControlLayout>
{
    /// <inheritdoc />
    public override FrontedControlLayout Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FrontedLayoutConfigException("Fronted control layout root must be a JSON object.");
        }

        var layout = new FrontedControlLayout();
        if (root.TryGetProperty(nameof(FrontedControlLayout.RequiredPlugins), out var pluginsElement)
            && pluginsElement.ValueKind != JsonValueKind.Null)
        {
            if (pluginsElement.ValueKind != JsonValueKind.Array)
            {
                throw new FrontedLayoutConfigException("ControlLayout.RequiredPlugins must be a JSON array or null.");
            }

            layout.RequiredPlugins = JsonSerializer.Deserialize<List<FrontedPluginDependency>>(
                pluginsElement.GetRawText(),
                options) ?? [];
        }

        if (!root.TryGetProperty(nameof(FrontedControlLayout.Controls), out var controlsElement)
            || controlsElement.ValueKind == JsonValueKind.Null)
        {
            return layout;
        }

        if (controlsElement.ValueKind != JsonValueKind.Object)
        {
            throw new FrontedLayoutConfigException("ControlLayout.Controls must be a JSON object or null.");
        }

        foreach (var controlProperty in controlsElement.EnumerateObject())
        {
            layout.Controls[controlProperty.Name] = FrontedCanvasConfigJsonConverter.ReadControl(
                controlProperty.Name,
                controlProperty.Value,
                options);
        }

        return layout;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        FrontedControlLayout value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WritePropertyName(nameof(FrontedControlLayout.RequiredPlugins));
        JsonSerializer.Serialize(writer, value.RequiredPlugins, options);

        writer.WritePropertyName(nameof(FrontedControlLayout.Controls));
        writer.WriteStartObject();
        foreach (var (name, control) in value.Controls)
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, control, control.GetType(), options);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
