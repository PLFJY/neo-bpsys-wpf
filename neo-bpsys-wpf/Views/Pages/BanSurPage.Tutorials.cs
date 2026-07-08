using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class BanSurPage
{
    /// <summary>Survivor ban page tutorial key.</summary>
    public const string TutorialPageKey = "Page.Bp.BanSur";

    /// <summary>Survivor ban tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Character selector basic package id.</summary>
        public const string CharacterSelectorBasic = TutorialPackageIds.BpCharacterSelectorBasic;
    }

    /// <summary>
    /// Registers tutorials owned by the survivor ban page.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey,
        [
            TutorialPackages.CharacterSelectorBasic,
            TutorialPackageIds.BpSharedBasic,
            TutorialPackageIds.BpCharacterSelectorAdvanced
        ]);

        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.CharacterSelectorBasic,
            TutorialPageKey,
            1,
            [
                TutorialDefinitionHelpers.DescendantTypeStep(
                    nameof(FirstBanSurvivorSelectorHost),
                    typeof(CharacterSelector).FullName!,
                    "先按空格匹配角色",
                    "这是角色选择器，不是普通下拉框。请先输入一个角色的全称、拼音全拼或简拼，然后按空格触发匹配。这一步先不要点确认。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.CharacterSelectorSearchCommitted,
                    allowMissing: true),
                TutorialDefinitionHelpers.DescendantTypeStep(
                    nameof(FirstBanSurvivorSelectorHost),
                    typeof(CharacterSelector).FullName!,
                    "确认角色选择",
                    "匹配到角色后，再按 Enter / Tab 或点击确认按钮完成选择。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.CharacterSelectorSelectionConfirmed,
                    allowMissing: true)
            ]));

        registrar.RegisterPackage(CreateFallbackPackage(TutorialPackageIds.BpSharedBasic, 2));
        registrar.RegisterPackage(CreateFallbackPackage(TutorialPackageIds.BpCharacterSelectorAdvanced, 3));
    }

    private static TutorialPackageDefinition CreateFallbackPackage(string packageId, int sequence) =>
        TutorialDefinitionHelpers.Package(
            packageId,
            TutorialPageKey,
            sequence,
            [
                TutorialDefinitionHelpers.Step(
                    null,
                    "功能教学",
                    NeoBpsysTutorialTexts.GetFallbackDescription(packageId),
                    ProductTourInteractionMode.BlockAll)
            ]);
}
