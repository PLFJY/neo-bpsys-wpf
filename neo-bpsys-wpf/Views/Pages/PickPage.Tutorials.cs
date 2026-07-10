using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Windows;

namespace neo_bpsys_wpf.Views.Pages;

public partial class PickPage : ITutorialOwner<PickPage>
{
    /// <summary>Character pick page tutorial key.</summary>
    public const string TutorialPageKey = "Page.Bp.Pick";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Character pick tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Pick character basic package reference.</summary>
        public static readonly TutorialPackageRef PickCharacterBasic = new(TutorialPackageIds.BpPickCharacterBasic);

        /// <summary>Global ban record package reference.</summary>
        public static readonly TutorialPackageRef GlobalBanRecordBasic = new(TutorialPackageIds.BpGlobalBanRecordBasic);

        /// <summary>Select four survivors package reference.</summary>
        public static readonly TutorialPackageRef SelectFourSurvivorsBasic = new(TutorialPackageIds.BpPickSelectFourSurvivorsBasic);

        /// <summary>Character changer package reference.</summary>
        public static readonly TutorialPackageRef CharacterChangerBasic = new(TutorialPackageIds.BpCharacterChangerBasic);
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
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<PickPage>()
            .Package(Tours.SelectFourSurvivorsBasic)
                .Step("完成四个求生者选择")
                    .Text("继续选择剩余求生者角色。四个求生者都选完后，再进入角色调整教学。")
                    .TargetName(nameof(SurvivorPickSelectorGroupBorder))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .Placement(ProductTourPlacement.TopLeft)
                    .WaitFor(TutorialSignalIds.PickSurvivorSlotsCompleted)
            .Package(Tours.GlobalBanRecordBasic)
                .Step("全局禁选记录")
                    .Text("刚刚选择的角色会记录到全局禁选中。后续新对局会清空当前局选择，但会保留这些全局禁选记录。")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(GlobalBanRecordPanel)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .TargetName(nameof(GlobalBanRecordPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .PostStepAction((_, _) =>
                    {
                        var gameGuidanceService = IAppHost.Host!.Services.GetRequiredService<IGameGuidanceService>();
                        gameGuidanceService.MoveToStepAsync(9);
                        return Task.CompletedTask;
                    })
            .Package(Tours.CharacterChangerBasic)
                .Step("调整已选角色顺序")
                    .Text("在选择角色结束后，分配角色阶段，可以在这里使用角色调整功能。调整后会同步更新当前 BP 状态。\n点击数字按钮即可将当前玩家的求生者角色与对应位置的求生者角色进行互换")
                    .TargetName(nameof(SurvivorPickSelectorGroupBorder))
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(SurvivorPickSelectorGroupBorder)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .PostStepAction((_, _) =>
                    {
                        var gameGuidanceService = IAppHost.Host!.Services.GetRequiredService<IGameGuidanceService>();
                        gameGuidanceService.MoveToStepAsync(10);
                        return Task.CompletedTask;
                    })
                .Build();
    }
}
