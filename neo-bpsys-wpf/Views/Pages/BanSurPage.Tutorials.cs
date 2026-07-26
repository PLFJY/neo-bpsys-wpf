using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class BanSurPage : ITutorialOwner<BanSurPage>
{
    /// <summary>求生者 Ban 页面教程键。</summary>
    public const string TutorialPageKey = "Page.Bp.BanSur";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>求生者 Ban 教程包引用。</summary>
    public static class Tours
    {
        /// <summary>角色选择器基础包引用。</summary>
        public static readonly TutorialPackageRef CharacterSelectorBasic = new(TutorialPackageIds.BpCharacterSelectorBasic);
    }

    /// <summary>
    /// 注册求生者 Ban 页面拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<BanSurPage>()
            .Package(Tours.CharacterSelectorBasic)
                .StepKey("Step.BpCharacterSelectorBasic.0.Title")
                    .TextKey("Step.BpCharacterSelectorBasic.0.Description")
                    .TargetDescendantType(nameof(FirstBanSurvivorSelectorHost), typeof(CharacterSelector))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .StepKey("Step.BpCharacterSelectorBasic.1.Title")
                    .TextKey("Step.BpCharacterSelectorBasic.1.Description")
                    .TargetDescendantType(nameof(FirstBanSurvivorSelectorHost), typeof(CharacterSelector))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .Placement(ProductTourPlacement.RightTop)
                    .AllowMissingTarget()
                    .WaitFor(TutorialSignalIds.CharacterSelectorSearchCommitted)
                .StepKey("Step.BpCharacterSelectorBasic.2.Title")
                    .TextKey("Step.BpCharacterSelectorBasic.2.Description")
                    .TargetDescendantType(nameof(FirstBanSurvivorSelectorHost), typeof(CharacterSelector))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .Placement(ProductTourPlacement.RightTop)
                    .WaitFor(TutorialSignalIds.CharacterSelectorSelectionConfirmed)
                    .PostStepAction((context, _) =>
                    {
                        var gameGuidanceService = context.Services.GetRequiredService<IGameGuidanceService>();
                        return gameGuidanceService.MoveToStepAsync(4);
                    })
                .Build();
    }
}
