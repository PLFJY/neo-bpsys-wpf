using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class BanSurPage : ITutorialOwner<BanSurPage>
{
    /// <summary>Survivor ban page tutorial key.</summary>
    public const string TutorialPageKey = "Page.Bp.BanSur";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Survivor ban tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Character selector basic package reference.</summary>
        public static readonly TutorialPackageRef CharacterSelectorBasic = new(TutorialPackageIds.BpCharacterSelectorBasic);
    }

    /// <summary>
    /// Registers tutorials owned by the survivor ban page.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<BanSurPage>()
            .Package(Tours.CharacterSelectorBasic)
                .Step("角色选择器教学")
                    .Text("现在我们来教学角色选择器的使用")
                    .TargetDescendantType(nameof(FirstBanSurvivorSelectorHost), typeof(CharacterSelector))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                .Step("先按空格匹配角色")
                    .Text("这是角色选择器，不是普通下拉框。请先输入一个角色的全称、拼音全拼或简拼，然后按空格触发匹配。这一步先不要点确认。")
                    .TargetDescendantType(nameof(FirstBanSurvivorSelectorHost), typeof(CharacterSelector))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .Placement(ProductTourPlacement.RightTop)
                    .AllowMissingTarget()
                    .WaitFor(TutorialSignalIds.CharacterSelectorSearchCommitted)
                .Step("确认角色选择")
                    .Text("匹配到角色后，再按 Enter / Tab 或点击确认按钮完成选择。")
                    .TargetDescendantType(nameof(FirstBanSurvivorSelectorHost), typeof(CharacterSelector))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .Placement(ProductTourPlacement.RightTop)
                    .WaitFor(TutorialSignalIds.CharacterSelectorSelectionConfirmed)
                    .PostStepAction((_, _) =>
                    {
                        var gameGuidanceService = IAppHost.Host!.Services.GetRequiredService<IGameGuidanceService>();
                        gameGuidanceService.MoveToStepAsync(4);
                        return Task.CompletedTask;
                    })
                .Build();
    }
}
