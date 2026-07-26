using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Views.FrontedDesigner;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Pages.FrontManage;
using neo_bpsys_wpf.Views.Windows;

namespace neo_bpsys_wpf.Tutorial;

/// <summary>
/// 注册 neo-bpsys-wpf 内置的教程包和流程。
/// </summary>
public static class NeoBpsysTutorialRegistration
{
    /// <summary>
    /// 注册所有内置的教程定义。
    /// </summary>
    /// <param name="packageRegistry">包注册表。</param>
    /// <param name="sequenceRegistry">序列注册表。</param>
    /// <param name="flowRegistry">流程注册表。</param>
    public static void Register(
        ITutorialPackageRegistry packageRegistry,
        ITutorialSequenceRegistry sequenceRegistry,
        ITutorialFlowRegistry flowRegistry)
    {
        var builder = new TutorialBuilder(packageRegistry, sequenceRegistry, flowRegistry);

        builder.RegisterOwner<MainWindow>();

        builder.RegisterOwner<FrontManagePage>();
        builder.RegisterOwner<FrontedWindowsView>();
        builder.RegisterOwner<FrontedLayoutPackagesView>();
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
