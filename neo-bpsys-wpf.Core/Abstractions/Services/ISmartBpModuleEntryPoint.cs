using neo_bpsys_wpf.Core.Models.SmartBpModule;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Entry point implemented by the SmartBP runtime module.
/// </summary>
public interface ISmartBpModuleEntryPoint
{
    /// <summary>
    /// Creates the real SmartBP page content.
    /// </summary>
    /// <param name="hostServices">Host service provider.</param>
    /// <returns>WPF content object.</returns>
    object CreateSmartBpContent(IServiceProvider hostServices);

    /// <summary>
    /// Gets feature commands exposed by this module.
    /// </summary>
    /// <returns>Feature command list.</returns>
    IReadOnlyList<SmartBpFeatureCommand> GetFeatureCommands();
}
