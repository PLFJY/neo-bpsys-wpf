using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Registry used by plugins to register Designer v3 fronted controls.
/// </summary>
public interface IFrontedControlPluginRegistry
{
    /// <summary>
    /// Registers a plugin fronted control descriptor.
    /// </summary>
    void Register<TConfig>(FrontedPluginControlDescriptor<TConfig> descriptor)
        where TConfig : FrontedControlConfigBase;
}
