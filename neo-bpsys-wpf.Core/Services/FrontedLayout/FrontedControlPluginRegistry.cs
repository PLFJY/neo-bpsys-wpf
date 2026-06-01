using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Collects plugin control descriptors contributed during startup.
/// </summary>
public sealed class FrontedControlPluginRegistry(
    IReadOnlySet<string> builtInControlTypes) : IFrontedControlPluginRegistry
{
    private readonly Dictionary<string, IFrontedPluginControlDescriptor> _descriptors =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IFrontedPluginControlDescriptor> Descriptors => _descriptors.Values.ToArray();

    public void Register<TConfig>(FrontedPluginControlDescriptor<TConfig> descriptor)
        where TConfig : FrontedControlConfigBase
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ValidateDescriptor(descriptor);

        if (builtInControlTypes.Contains(descriptor.FullControlType))
        {
            throw new FrontedLayoutConfigException(
                $"Plugin ControlType '{descriptor.FullControlType}' cannot replace a built-in fronted control.");
        }

        if (!_descriptors.TryAdd(descriptor.FullControlType, descriptor))
        {
            throw new FrontedLayoutConfigException(
                $"Plugin ControlType '{descriptor.FullControlType}' has already been registered.");
        }
    }

    private static void ValidateDescriptor<TConfig>(FrontedPluginControlDescriptor<TConfig> descriptor)
        where TConfig : FrontedControlConfigBase
    {
        if (!FrontedPluginControlType.IsValidPart(descriptor.PackageId))
        {
            throw new FrontedLayoutConfigException(
                $"Plugin control descriptor PackageId '{descriptor.PackageId}' is invalid.");
        }

        if (!FrontedPluginControlType.IsValidPart(descriptor.ControlTypeName))
        {
            throw new FrontedLayoutConfigException(
                $"Plugin control descriptor ControlTypeName '{descriptor.ControlTypeName}' is invalid.");
        }

        FrontedPluginControlType.Parse(descriptor.FullControlType);

        if (!typeof(FrontedControlConfigBase).IsAssignableFrom(descriptor.ConfigType))
        {
            throw new FrontedLayoutConfigException(
                $"Plugin ControlType '{descriptor.FullControlType}' ConfigType must inherit FrontedControlConfigBase.");
        }

        if (!typeof(TConfig).IsAssignableFrom(descriptor.ConfigType))
        {
            throw new FrontedLayoutConfigException(
                $"Plugin ControlType '{descriptor.FullControlType}' ConfigType must be assignable to {typeof(TConfig).Name}.");
        }

        if (descriptor.CreateControl is null)
        {
            throw new FrontedLayoutConfigException(
                $"Plugin ControlType '{descriptor.FullControlType}' requires CreateControl.");
        }
    }
}
