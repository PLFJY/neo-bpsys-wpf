using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class TalentPage
{
    /// <summary>Talent and trait page tutorial key.</summary>
    public const string TutorialPageKey = "Page.Bp.TalentTrait";

    /// <summary>Talent and trait tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Talent and trait basic package id.</summary>
        public const string Basic = TutorialPackageIds.BpTalentTraitBasic;
    }

    /// <summary>
    /// Registers tutorials owned by the talent and trait page.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey, [TutorialPackages.Basic]);
        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.Basic,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.Step(
                    TutorialTargetNames.TalentTraitSelectorPanel,
                    "设置天赋和特质",
                    "这里用于设置本局角色的天赋和特质。这些信息会用于前台展示和赛后记录。按比赛实际情况选择后继续引导。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    TutorialTargetNames.SurvivorTalentSelector,
                    "求生者天赋",
                    "这里设置四名求生者的天赋。本教程不强制完整填写。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true),
                TutorialDefinitionHelpers.Step(
                    TutorialTargetNames.HunterTraitSelector,
                    "监管者特质",
                    "这里设置监管者天赋和辅助特质。确认实际比赛信息后继续对局引导。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));
    }
}
