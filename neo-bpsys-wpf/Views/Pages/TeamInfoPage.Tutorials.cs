using System.IO;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.Views.Pages;

public partial class TeamInfoPage
{
    /// <summary>Team info page tutorial key.</summary>
    public const string TutorialPageKey = TutorialPageKeys.TeamInfo;

    /// <summary>Team info tutorial package ids.</summary>
    public static class TutorialPackages
    {
        /// <summary>Team info basic package id.</summary>
        public const string Basic = TutorialPackageIds.TeamInfoBasic;

        /// <summary>Team name basic package id.</summary>
        public const string TeamNameBasic = TutorialPackageIds.TeamInfoTeamNameBasic;

        /// <summary>Team JSON import package id.</summary>
        public const string JsonImport = TutorialPackageIds.TeamInfoJsonImport;

        /// <summary>Preset team JSON import package id.</summary>
        public const string JsonImportPreset = TutorialPackageIds.TeamInfoJsonImportPreset;

        /// <summary>Team player management package id.</summary>
        public const string PlayerManage = TutorialPackageIds.TeamInfoPlayerManage;

        /// <summary>Team info advanced package id.</summary>
        public const string Advanced = TutorialPackageIds.TeamInfoAdvanced;
    }

    /// <summary>
    /// Registers tutorials owned by the team info page.
    /// </summary>
    /// <param name="registrar">Tutorial registrar.</param>
    public static void RegisterTutorials(ITutorialDefinitionRegistrar registrar)
    {
        registrar.RegisterSequence(TutorialPageKey,
        [
            TutorialPackages.TeamNameBasic,
            TutorialPackages.JsonImportPreset,
            TutorialPackages.PlayerManage,
            TutorialPackages.Basic,
            TutorialPackages.JsonImport,
            TutorialPackages.Advanced
        ]);

        registrar.RegisterPackage(CreateTeamNamePackage(TutorialPackages.TeamNameBasic, 1));
        registrar.RegisterPackage(CreateJsonImportPackage(TutorialPackages.JsonImportPreset, 2));
        registrar.RegisterPackage(CreatePlayerManagePackage());
        registrar.RegisterPackage(CreateTeamNamePackage(TutorialPackages.Basic, 4));
        registrar.RegisterPackage(CreateJsonImportPackage(TutorialPackages.JsonImport, 5));
        registrar.RegisterPackage(TutorialDefinitionHelpers.Package(
            TutorialPackages.Advanced,
            TutorialPageKey,
            6,
            [CreateFallbackStep(TutorialPackages.Advanced)]));
    }

    private static TutorialPackageDefinition CreateTeamNamePackage(string packageId, int sequence) =>
        TutorialDefinitionHelpers.Package(
            packageId,
            TutorialPageKey,
            sequence,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(HomeTeamNameInput),
                    "填写队伍名称",
                    "这里可以设置队伍名称。先试着输入一个队伍名。",
                    ProductTourInteractionMode.AllowTargetOnly),
                TutorialDefinitionHelpers.Step(
                    nameof(HomeTeamNameConfirmButton),
                    "确认队伍名称",
                    "点击确认后，队伍名称会写入当前比赛数据。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    TutorialSignalIds.TeamNameConfirmed),
                TutorialDefinitionHelpers.Step(
                    nameof(HomeTeamLogoButton),
                    "设置队伍 Logo",
                    "这里可以设置主队 Logo。本次导览可以直接点击下一步继续。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true)
            ]);

    private static TutorialPackageDefinition CreateJsonImportPackage(string packageId, int sequence) =>
        TutorialDefinitionHelpers.Package(
            packageId,
            TutorialPageKey,
            sequence,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(HomeTeamJsonImportButton),
                    "导入狼队预设",
                    "点击导入后，在打开的文件对话框中选择“队伍信息导入示例-Wolves.json”。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    beforeShowAsync: (_, _) =>
                    {
                        SetExamplesJsonPickerHint("请导入狼队信息：选择“队伍信息导入示例-Wolves.json”");
                        return Task.CompletedTask;
                    }),
                TutorialDefinitionHelpers.Step(
                    nameof(HomePlayerListPanel),
                    "调整狼队上场下场",
                    "导入后，在这里调整狼队成员的上场和下场状态。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    avatarPlacement: ProductTourAvatarPlacement.BottomRight,
                    avatarPose: TutorialAvatarPose.LeftTop),
                TutorialDefinitionHelpers.Step(
                    nameof(AwayTeamJsonImportButton),
                    "导入 GR 预设",
                    "点击导入后，在打开的文件对话框中选择“队伍信息导入示例-GR.json”。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    scrollAnchorName: nameof(AwayTeamInfoCard),
                    beforeShowAsync: (_, _) =>
                    {
                        SetExamplesJsonPickerHint("请导入 GR 信息：选择“队伍信息导入示例-GR.json”");
                        return Task.CompletedTask;
                    }),
                TutorialDefinitionHelpers.Step(
                    nameof(AwayPlayerListPanel),
                    "调整 GR 上场下场",
                    "导入后，在这里调整 GR 成员的上场和下场状态。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    avatarPlacement: ProductTourAvatarPlacement.BottomRight,
                    avatarPose: TutorialAvatarPose.LeftTop)
            ]);

    private static TutorialPackageDefinition CreatePlayerManagePackage() =>
        TutorialDefinitionHelpers.Package(
            TutorialPackages.PlayerManage,
            TutorialPageKey,
            3,
            [
                TutorialDefinitionHelpers.Step(
                    nameof(HomePlayerPositionPanel),
                    "调整队伍成员顺序",
                    "这里可以调整当前上场队员的顺序，前台和 BP 流程会使用这些信息。",
                    ProductTourInteractionMode.AllowTargetOnly,
                    allowMissing: true,
                    avatarPlacement: ProductTourAvatarPlacement.BottomRight,
                    avatarPose: TutorialAvatarPose.LeftTop)
            ]);

    private static ProductTourStep CreateFallbackStep(string packageId) =>
        TutorialDefinitionHelpers.Step(
            null,
            "功能教学",
            NeoBpsysTutorialTexts.GetFallbackDescription(packageId),
            ProductTourInteractionMode.BlockAll);

    private static void SetExamplesJsonPickerHint(string title)
    {
        TutorialFilePickerHints.SetNextJsonPickerHint(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Examples"),
            title);
    }
}
