using System.IO;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class TeamInfoPage : ITutorialOwner<TeamInfoPage>
{
    /// <summary>Team info page tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.TeamInfo;

    /// <inheritdoc />
    public static string TutorialKey => TutorialPageKey;

    /// <summary>Team info tutorial package references.</summary>
    public static class Tours
    {
        /// <summary>Team info basic package reference.</summary>
        public static readonly TutorialPackageRef Basic = new(TutorialPackageIds.TeamInfoBasic);

        /// <summary>Team name basic package reference.</summary>
        public static readonly TutorialPackageRef TeamNameBasic = new(TutorialPackageIds.TeamInfoTeamNameBasic);

        /// <summary>Team JSON import package reference.</summary>
        public static readonly TutorialPackageRef JsonImport = new(TutorialPackageIds.TeamInfoJsonImport);

        /// <summary>Preset team JSON import package reference.</summary>
        public static readonly TutorialPackageRef JsonImportPreset = new(TutorialPackageIds.TeamInfoJsonImportPreset);

        /// <summary>Team player management package reference.</summary>
        public static readonly TutorialPackageRef PlayerManage = new(TutorialPackageIds.TeamInfoPlayerManage);

        /// <summary>Team info advanced package reference.</summary>
        public static readonly TutorialPackageRef Advanced = new(TutorialPackageIds.TeamInfoAdvanced);
    }

    /// <summary>
    /// Registers tutorials owned by the team info page.
    /// </summary>
    /// <param name="builder">Tutorial builder.</param>
    public static void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.ForPage<TeamInfoPage>()
            .Package(Tours.TeamNameBasic)
                .Step("填写队伍名称")
                    .Text("这里可以设置队伍名称。先试着输入一个队伍名。")
                    .TargetName(nameof(HomeTeamNameInput))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                .Step("确认队伍名称")
                    .Text("点击确认后，队伍名称会写入当前比赛数据。")
                    .TargetName(nameof(HomeTeamNameConfirmButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .WaitFor(TutorialSignalIds.TeamNameConfirmed)
                .Step("设置队伍 Logo")
                    .Text("这里可以设置主队 Logo。本次导览可以直接点击下一步继续。")
                    .TargetName(nameof(HomeTeamLogoButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
            .Package(Tours.JsonImportPreset)
                .Step("导入狼队预设")
                    .Text("点击导入后，在打开的文件对话框中选择“队伍信息导入示例-Wolves.json”。")
                    .TargetName(nameof(HomeTeamJsonImportButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .PreStepAction(SetExamplesJsonPickerHintAction("SetHomeTeamJsonPickerHint", "请导入狼队信息：选择“队伍信息导入示例-Wolves.json”"))
                    .WaitFor(TutorialSignalIds.TeamJsonImportedHome)
                .Step("调整狼队上场下场")
                    .Text("导入后，在这里调整狼队成员的上场和下场状态。")
                    .TargetName(nameof(HomePlayerListPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.BottomRight)
                    .AvatarPose(TutorialAvatarPose.LeftTop)
                .Step("导入 GR 预设")
                    .Text("点击导入后，在打开的文件对话框中选择“队伍信息导入示例-GR.json”。")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(AwayTeamInfoCard)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .PreStepAction(SetExamplesJsonPickerHintAction("SetAwayTeamJsonPickerHint", "请导入 GR 信息：选择“队伍信息导入示例-GR.json”"))
                    .TargetName(nameof(AwayTeamJsonImportButton))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .WaitFor(TutorialSignalIds.TeamJsonImportedAway)
                .Step("调整 GR 上场下场")
                    .Text("导入后，在这里调整 GR 成员的上场和下场状态。")
                    .TargetName(nameof(AwayPlayerListPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.BottomRight)
                    .AvatarPose(TutorialAvatarPose.LeftTop)
            .Package(Tours.PlayerManage)
                .Step("调整队伍成员顺序")
                    .Text("这里可以调整当前上场队员的顺序，前台和 BP 流程会使用这些信息。")
                    .PreStepAction(TutorialStepActions.SmoothScrollTo(nameof(HomePlayerPositionPanel)))
                    .PreStepAction(TutorialStepActions.Delay(250))
                    .TargetName(nameof(HomePlayerPositionPanel))
                    .Interaction(ProductTourInteractionMode.AllowTargetOnly)
                    .AllowMissingTarget()
                    .AvatarPlacement(ProductTourAvatarPlacement.BottomRight)
                    .AvatarPose(TutorialAvatarPose.LeftTop)
                .Build();
    }

    private static TutorialStepAction SetExamplesJsonPickerHintAction(string name, string title) =>
        new(name, (_, _) =>
        {
            SetExamplesJsonPickerHint(title);
            return Task.CompletedTask;
        });

    private static void SetExamplesJsonPickerHint(string title)
    {
        TutorialFilePickerHints.SetNextJsonPickerHint(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Examples"),
            title);
    }
}
