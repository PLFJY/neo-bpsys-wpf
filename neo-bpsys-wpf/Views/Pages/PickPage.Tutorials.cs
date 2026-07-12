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
    /// <summary>角色 Pick 页面教程 Key。</summary>
    public const string TutorialPageKey = "Page.Bp.Pick";

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>角色 Pick 教程包引用。</summary>
    public static class Tours
    {
        /// <summary>Pick 角色基础包引用。</summary>
        public static readonly TutorialPackageRef PickCharacterBasic = new(TutorialPackageIds.BpPickCharacterBasic);

        /// <summary>全局 Ban 记录包引用。</summary>
        public static readonly TutorialPackageRef GlobalBanRecordBasic = new(TutorialPackageIds.BpGlobalBanRecordBasic);

        /// <summary>选择四名求生者包引用。</summary>
        public static readonly TutorialPackageRef SelectFourSurvivorsBasic = new(TutorialPackageIds.BpPickSelectFourSurvivorsBasic);

        /// <summary>角色更换包引用。</summary>
        public static readonly TutorialPackageRef CharacterChangerBasic = new(TutorialPackageIds.BpCharacterChangerBasic);
    }

    /// <summary>Pick 页面教程目标名称。</summary>
    public static class TutorialTargets
    {
        /// <summary>当前求生者队伍的全局 Ban 记录面板目标标签。</summary>
        public const string CurrentSurvivorGlobalBanRecordPanel = "CurrentSurvivorGlobalBanRecordPanel";
    }

    /// <summary>
    /// 注册 Pick 页面所拥有的教程。
    /// </summary>
    /// <param name="builder">教程构建器。</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<PickPage>()
            .Package(Tours.SelectFourSurvivorsBasic)
                .StepKey("Step.BpPickSelectFourSurvivorsBasic.0.Title")
                    .TextKey("Step.BpPickSelectFourSurvivorsBasic.0.Description")
                    .TargetName(nameof(SurvivorPickSelectorGroupBorder))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.TopLeft)
                    .Placement(ProductTourPlacement.TopLeft)
                    .WaitFor(TutorialSignalIds.PickSurvivorSlotsCompleted)
            .Package(Tours.GlobalBanRecordBasic)
                .StepKey("Step.BpGlobalBanRecordBasic.0.Title")
                    .TextKey("Step.BpGlobalBanRecordBasic.0.Description")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(GlobalBanRecordPanel)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .TargetName(nameof(GlobalBanRecordPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .PostStepAction((context, _) =>
                    {
                        var gameGuidanceService = context.Services.GetRequiredService<IGameGuidanceService>();
                        return gameGuidanceService.MoveToStepAsync(9);
                    })
            .Package(Tours.CharacterChangerBasic)
                .StepKey("Step.BpCharacterChangerBasic.0.Title")
                    .TextKey("Step.BpCharacterChangerBasic.0.Description")
                    .TargetName(nameof(SurvivorPickSelectorGroupBorder))
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(SurvivorPickSelectorGroupBorder)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .PostStepAction((context, _) =>
                    {
                        var gameGuidanceService = context.Services.GetRequiredService<IGameGuidanceService>();
                        return gameGuidanceService.MoveToStepAsync(10);
                    })
                .Build();
    }
}
