using System.Windows;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class PickPage
{
    /// <summary>Character pick page tutorial key.</summary>
    public const string TutorialPageKey = "Page.Bp.Pick";

    /// <summary>Character pick tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Pick character basic package id.</summary>
        public const string PickCharacterBasic = TutorialPackageIds.BpPickCharacterBasic;

        /// <summary>Global ban record package id.</summary>
        public const string GlobalBanRecordBasic = TutorialPackageIds.BpGlobalBanRecordBasic;
    }

    /// <summary>Pick page tutorial target names.</summary>
    public static class TutorialTargets
    {
        /// <summary>Current survivor team's global ban record panel target tag.</summary>
        public const string CurrentSurvivorGlobalBanRecordPanel = "CurrentSurvivorGlobalBanRecordPanel";
    }

    /// <summary>
    /// Registers tutorials owned by the pick page.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey,
        [
            TutorialPackages.PickCharacterBasic,
            TutorialPackages.GlobalBanRecordBasic
        ]);

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.PickCharacterBasic,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.DescendantTypeStep(
                    nameof(FirstSurvivorPickSelectorHost),
                    typeof(CharacterSelector).FullName!,
                    "选择 1、2 号角色",
                    "继续在 Pick 页面选择 1、2 号求生者角色，选择结果会记录到全局禁选中。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.CharacterSelectorSelectionConfirmed,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.GlobalBanRecordBasic,
            TutorialPageKey,
            2,
            [
                TutorialDefinitionHelpers.ElementTagStep(
                    TutorialTargets.CurrentSurvivorGlobalBanRecordPanel,
                    "全局禁选记录",
                    "刚刚的选择已经被记录到全局禁选中。全局禁选会影响后续场次，新建对局会清空当局选择但保留这些记录。",
                    ProductTourInteractionMode.AllowAll,
                    allowMissing: true,
                    cardOffset: new Point(80, 0))
            ]));
    }
}
