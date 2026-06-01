using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 默认 v3 前台控件工厂注册表。
/// </summary>
public class FrontedControlRegistry : IFrontedControlRegistry
{
    private readonly Dictionary<string, IFrontedControl> _controls;
    private readonly Dictionary<string, IFrontedPluginControlDescriptor> _pluginDescriptors;

    /// <summary>
    /// 初始化控件工厂注册表。
    /// </summary>
    public FrontedControlRegistry(IEnumerable<IFrontedControl> controls)
        : this(controls, [], null)
    {
    }

    /// <summary>
    /// 初始化控件工厂注册表。
    /// </summary>
    public FrontedControlRegistry(
        IEnumerable<IFrontedControl> controls,
        IEnumerable<IFrontedControlPluginContributor> pluginContributors,
        ILogger<FrontedControlRegistry>? logger)
    {
        _controls = new Dictionary<string, IFrontedControl>(StringComparer.OrdinalIgnoreCase);
        foreach (var control in controls)
        {
            if (!_controls.TryAdd(control.ControlType, control))
            {
                throw new FrontedLayoutConfigException(
                    $"Fronted ControlType '{control.ControlType}' has already been registered.");
            }
        }

        var builtInControlTypes = _controls.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pluginRegistry = new FrontedControlPluginRegistry(builtInControlTypes);
        foreach (var contributor in pluginContributors)
        {
            contributor.RegisterFrontedControls(pluginRegistry);
        }

        _pluginDescriptors = pluginRegistry.Descriptors.ToDictionary(
            descriptor => descriptor.FullControlType,
            StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in pluginRegistry.Descriptors)
        {
            var adapter = CreatePluginAdapter(descriptor);
            if (!_controls.TryAdd(adapter.ControlType, adapter))
            {
                throw new FrontedLayoutConfigException(
                    $"Plugin ControlType '{adapter.ControlType}' conflicts with an existing fronted control.");
            }

            logger?.LogInformation(
                "Registered plugin fronted control {ControlType} with config {ConfigType}.",
                adapter.ControlType,
                adapter.ConfigType.FullName);
        }
    }

    /// <inheritdoc />
    public IFrontedControl? GetControl(string controlType)
    {
        return _controls.GetValueOrDefault(controlType);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IFrontedControl> GetControls()
    {
        return _controls.Values.ToArray();
    }

    /// <inheritdoc />
    public bool IsPluginControlRegistered(string fullControlType)
    {
        return _pluginDescriptors.ContainsKey(fullControlType);
    }

    /// <inheritdoc />
    public IFrontedPluginControlDescriptor? GetPluginDescriptor(string fullControlType)
    {
        return _pluginDescriptors.GetValueOrDefault(fullControlType);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IFrontedPluginControlDescriptor> GetPluginDescriptors()
    {
        return _pluginDescriptors.Values.ToArray();
    }

    private static IFrontedControl CreatePluginAdapter(IFrontedPluginControlDescriptor descriptor)
    {
        var descriptorType = descriptor.GetType();
        if (!descriptorType.IsGenericType
            || descriptorType.GetGenericTypeDefinition() != typeof(FrontedPluginControlDescriptor<>))
        {
            throw new FrontedLayoutConfigException(
                $"Plugin ControlType '{descriptor.FullControlType}' descriptor type is not supported.");
        }

        var genericConfigType = descriptorType.GetGenericArguments()[0];
        var adapterType = typeof(FrontedPluginControlAdapter<>).MakeGenericType(genericConfigType);
        return (IFrontedControl)(Activator.CreateInstance(adapterType, descriptor)
            ?? throw new FrontedLayoutConfigException(
                $"Plugin ControlType '{descriptor.FullControlType}' adapter could not be created."));
    }
}
