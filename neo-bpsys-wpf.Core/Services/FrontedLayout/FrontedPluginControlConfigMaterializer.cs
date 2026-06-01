using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Text.Json;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Converts generic plugin configs loaded from JSON into typed plugin configs when the plugin is installed.
/// </summary>
public static class FrontedPluginControlConfigMaterializer
{
    /// <summary>
    /// Materializes a generic plugin config using the registered descriptor when possible.
    /// </summary>
    public static FrontedControlConfigBase Materialize(
        string controlName,
        FrontedControlConfigBase config,
        IFrontedControlRegistry? controlRegistry)
    {
        if (config is not PluginFrontedControlConfig pluginConfig
            || controlRegistry?.GetPluginDescriptor(pluginConfig.ControlType) is not { } descriptor)
        {
            return config;
        }

        return Materialize(controlName, pluginConfig, descriptor);
    }

    /// <summary>
    /// Materializes a generic plugin config using the supplied descriptor.
    /// </summary>
    public static FrontedControlConfigBase Materialize(
        string controlName,
        PluginFrontedControlConfig config,
        IFrontedPluginControlDescriptor descriptor)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, config.GetType());
            var converted = (FrontedControlConfigBase?)JsonSerializer.Deserialize(json, descriptor.ConfigType);
            if (converted is not null && descriptor.ConfigType.IsInstanceOfType(converted))
            {
                converted.ControlType = descriptor.FullControlType;
                return converted;
            }
        }
        catch (JsonException ex)
        {
            throw new FrontedLayoutConfigException(
                $"Control '{controlName}' with ControlType '{config.ControlType}' could not be converted to plugin config '{descriptor.ConfigType.Name}'.",
                ex);
        }

        throw new FrontedLayoutConfigException(
            $"Control '{controlName}' with ControlType '{config.ControlType}' could not be converted to plugin config '{descriptor.ConfigType.Name}'.");
    }
}
