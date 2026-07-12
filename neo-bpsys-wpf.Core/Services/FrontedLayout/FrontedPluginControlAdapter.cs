using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 将插件描述符适配到现有的前台控件工厂抽象。
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
    /// 此适配器表示的插件描述符。
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

        return FrontedPluginControlConfigMaterializer.Materialize(name, (PluginFrontedControlConfig)config, descriptor) is TConfig result
            ? result
            : throw new FrontedLayoutConfigException(
                $"Control '{name}' with ControlType '{ControlType}' could not be converted to plugin config '{ConfigType.Name}'.");
    }
}
