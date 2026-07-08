using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using neo_bpsys_wpf.Views.Windows;

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
        var registrar = new TutorialDefinitionRegistrar(packageRegistry, sequenceRegistry, flowRegistry);

        MainWindow.RegisterTutorials(registrar);

        FrontManagePage.RegisterTutorials(registrar);
        FrontedWindowsView.RegisterTutorials(registrar);
        FrontedLayoutPackagesView.RegisterTutorials(registrar);
        FrontedDesignerWindow.RegisterTutorials(registrar);

        TeamInfoPage.RegisterTutorials(registrar);
        BanSurPage.RegisterTutorials(registrar);
        BanHunPage.RegisterTutorials(registrar);
        PickPage.RegisterTutorials(registrar);
        ScorePage.RegisterTutorials(registrar);
        SmartBpPage.RegisterTutorials(registrar);

        FirstRunStandardBpTour.RegisterTutorials(registrar);
        NeoBpsysTutorialFlows.Register(flowRegistry);
    }
}
