using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Text.Json;
using System.Windows;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Adapts a plugin descriptor to the existing fronted control factory abstraction.
/// </summary>
public sealed class FrontedPluginControlAdapter<TConfig>(
    FrontedPluginControlDescriptor<TConfig> descriptor) : IFrontedControl
    where TConfig : FrontedControlConfigBase
{
    /// <inheritdoc />
    public string ControlType => descriptor.FullControlType;

    /// <inheritdoc />
    public Type ConfigType => descriptor.ConfigType;

    /// <summary>
    /// Plugin descriptor represented by this adapter.
    /// </summary>
    public IFrontedPluginControlDescriptor Descriptor => descriptor;

    /// <inheritdoc />
    public FrameworkElement Create(string name, FrontedControlConfigBase config, FrontedControlBuildContext context)
    {
        var typedConfig = ConvertConfig(name, config);
        return descriptor.CreateControl(name, typedConfig, context);
    }

    private TConfig ConvertConfig(string name, FrontedControlConfigBase config)
    {
        if (config is TConfig typedConfig)
        {
            return typedConfig;
        }

        if (config is not PluginFrontedControlConfig)
        {
            throw new FrontedLayoutConfigException(
                $"Control '{name}' with ControlType '{ControlType}' uses config type '{config.GetType().Name}', expected '{ConfigType.Name}'.");
        }

        try
        {
            var json = JsonSerializer.Serialize(config, config.GetType());
            var converted = (FrontedControlConfigBase?)JsonSerializer.Deserialize(json, ConfigType);
            if (converted is TConfig result)
            {
                return result;
            }
        }
        catch (JsonException ex)
        {
            throw new FrontedLayoutConfigException(
                $"Control '{name}' with ControlType '{ControlType}' could not be converted to plugin config '{ConfigType.Name}'.",
                ex);
        }

        throw new FrontedLayoutConfigException(
            $"Control '{name}' with ControlType '{ControlType}' could not be converted to plugin config '{ConfigType.Name}'.");
    }
}
