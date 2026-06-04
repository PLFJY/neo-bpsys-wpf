using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Declares explicit roots that may be scanned by the Designer v3 binding catalog.
/// </summary>
public interface IFrontedBindingRootProvider
{
    /// <summary>
    /// Gets binding roots. Runtime values must not be read by implementations.
    /// </summary>
    IReadOnlyList<FrontedBindingRootDescriptor> GetRoots();
}
