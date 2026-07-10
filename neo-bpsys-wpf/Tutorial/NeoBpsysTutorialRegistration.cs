using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Views.FrontedDesigner;
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
        var builder = new TutorialBuilder(packageRegistry, sequenceRegistry, flowRegistry);

        builder.RegisterOwner<MainWindow>();

        builder.RegisterOwner<FrontManagePage>();
        builder.RegisterOwner<FrontedWindowsView>();
        builder.RegisterOwner<FrontedDesignerWindow>();
        builder.RegisterOwner<BehaviorPanelView>();
        builder.RegisterOwner<FrontedBehaviorAnimationEditorWindow>();

        builder.RegisterOwner<TeamInfoPage>();
        builder.RegisterOwner<BanSurPage>();
        builder.RegisterOwner<BanHunPage>();
        builder.RegisterOwner<PickPage>();
        builder.RegisterOwner<TalentPage>();
        builder.RegisterOwner<ScorePage>();

        builder.RegisterApp<App>();
        NeoBpsysTutorialFlows.Register(builder);
    }
}
