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

        /// <summary>Select four survivors package id.</summary>
        public const string SelectFourSurvivorsBasic = TutorialPackageIds.BpPickSelectFourSurvivorsBasic;

        /// <summary>Character changer package id.</summary>
        public const string CharacterChangerBasic = TutorialPackageIds.BpCharacterChangerBasic;
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
            TutorialPackages.SelectFourSurvivorsBasic,
            TutorialPackages.GlobalBanRecordBasic,
            TutorialPackages.CharacterChangerBasic
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
                    "先选择前两个求生者角色，选择结果会记录到全局禁选中。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.PickCharacterSelectedSurvivor2,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.SelectFourSurvivorsBasic,
            TutorialPageKey,
            2,
            [
                TutorialDefinitionHelpers.DescendantTypeStep(
                    nameof(FirstSurvivorPickSelectorHost),
                    typeof(CharacterSelector).FullName!,
                    "完成四个求生者选择",
                    "继续选择剩余求生者角色。四个求生者都选完后，再进入角色调整教学。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.PickSurvivorSlotsCompleted,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.GlobalBanRecordBasic,
            TutorialPageKey,
            3,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(GlobalBanRecordPanel),
                    "全局禁选记录",
                    "刚刚选择的角色会记录到全局禁选中。后续新对局会清空当前局选择，但会保留这些全局禁选记录。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.CharacterChangerBasic,
            TutorialPageKey,
            4,
            [
                TutorialDefinitionHelpers.DescendantTypeStep(
                    nameof(FirstSurvivorPickSelectorHost),
                    typeof(CharacterChanger).FullName!,
                    "调整已选角色",
                    "如果选择错误或需要临时调整角色，可以使用 CharacterChanger 修改已选角色。修改后会同步更新当前 BP 状态。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]));
    }
}
