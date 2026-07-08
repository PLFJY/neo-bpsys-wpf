using neo_bpsys_wpf.ProductTour;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// Registers built-in neo-bpsys-wpf tutorial packages and flows.
/// </summary>
public static class NeoBpsysTutorialRegistration
{
    /// <summary>
    /// Registers all built-in tutorial definitions.
    /// </summary>
    /// <param name="packageRegistry">Package registry.</param>
    /// <param name="sequenceRegistry">Sequence registry.</param>
    /// <param name="flowRegistry">Flow registry.</param>
    public static void Register(
        ITutorialPackageRegistry packageRegistry,
        ITutorialSequenceRegistry sequenceRegistry,
        ITutorialFlowRegistry flowRegistry)
    {
        NeoBpsysTutorialSequences.Register(sequenceRegistry);
        NeoBpsysTutorialPackages.Register(packageRegistry);
        NeoBpsysTutorialFlows.Register(flowRegistry);
    }
}
