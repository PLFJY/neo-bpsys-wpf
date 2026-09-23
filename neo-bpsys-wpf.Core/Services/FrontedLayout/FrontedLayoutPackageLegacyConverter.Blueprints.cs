#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Converters;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.Legacy;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Media;
using static neo_bpsys_wpf.Core.Services.FrontedLayout.LegacyConvertMessageHelper;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 旧版前台布局包转换器的Blueprints逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageLegacyConverter
{
    private static IReadOnlyDictionary<LegacyLayoutKey, IReadOnlyList<LegacyControlBlueprint>> CreateLegacyControlBlueprints()
    {
        var result = new Dictionary<LegacyLayoutKey, IReadOnlyList<LegacyControlBlueprint>>();

        AddBlueprints(result, "BpWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Image("SurTeamLogo", "Image", "CurrentGame.SurTeam.Logo", 615, 670, 50, 50, cornerRadius: 8, stretch: "Fill"),
            Text("SurTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentSurTeamMajorText", "BpWindow.MajorPoints", 607, 776),
            Text("SurTeamName", "Text", "CurrentGame.SurTeam.Name", "BpWindow.TeamName", 580, 720, 120, null, textWrapping: "WrapWithOverflow"),
            Text("GameScoresSur", "Text", "CurrentGame.MatchScore.CurrentSurTeamMinorScoreText", "BpWindow.GameScores", 622, 746, 36, 30),
            Text("Timer", "Text", "RemainingSeconds", "BpWindow.Timer", 671, 672, 100, null, zIndex: 1),
            Text("GameScoresHun", "Text", "CurrentGame.MatchScore.CurrentHunTeamMinorScoreText", "BpWindow.GameScores", 784, 746, 36, 30),
            Text("HunTeamName", "Text", "CurrentGame.HunTeam.Name", "BpWindow.TeamName", 742, 720, 120, null, textWrapping: "WrapWithOverflow"),
            Text("HunTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentHunTeamMajorText", "BpWindow.MajorPoints", 770, 776),
            Image("HunTeamLogo", "Image", "CurrentGame.HunTeam.Logo", 776, 670, 50, 50, cornerRadius: 8, stretch: "Fill"),
            Image("HunBanCurrent0", "Image", "CurrentGame.CurrentHunBannedList[0].HeaderImageSingleColor", 11.5, 562.5, 44.5, 44.5, specialProperties: CurrentBanLock("Hun", 0)),
            Image("HunBanCurrent1", "Image", "CurrentGame.CurrentHunBannedList[1].HeaderImageSingleColor", 64, 562.5, 44.5, 44.5, specialProperties: CurrentBanLock("Hun", 1)),
            Folded("HunBanCurrentLock0", "HunBanCurrent0", "Folded into HunBanCurrent0 lock overlay metadata."),
            Folded("HunBanCurrentLock1", "HunBanCurrent1", "Folded into HunBanCurrent1 lock overlay metadata."),
            Image("SurBanCurrent0", "Image", "CurrentGame.CurrentSurBannedList[0].HeaderImageSingleColor", 1226.5, 563, 44.5, 44.5, specialProperties: CurrentBanLock("Sur", 0)),
            Image("SurBanCurrent1", "Image", "CurrentGame.CurrentSurBannedList[1].HeaderImageSingleColor", 1279, 563, 44.5, 44.5, specialProperties: CurrentBanLock("Sur", 1)),
            Image("SurBanCurrent2", "Image", "CurrentGame.CurrentSurBannedList[2].HeaderImageSingleColor", 1331.5, 563, 44.5, 44.5, specialProperties: CurrentBanLock("Sur", 2)),
            Image("SurBanCurrent3", "Image", "CurrentGame.CurrentSurBannedList[3].HeaderImageSingleColor", 1384, 563, 44.5, 44.5, specialProperties: CurrentBanLock("Sur", 3)),
            Folded("SurBanCurrentLock0", "SurBanCurrent0", "Folded into SurBanCurrent0 lock overlay metadata."),
            Folded("SurBanCurrentLock1", "SurBanCurrent1", "Folded into SurBanCurrent1 lock overlay metadata."),
            Folded("SurBanCurrentLock2", "SurBanCurrent2", "Folded into SurBanCurrent2 lock overlay metadata."),
            Folded("SurBanCurrentLock3", "SurBanCurrent3", "Folded into SurBanCurrent3 lock overlay metadata."),
            Image("SurPick0", "BorderedImage", "CurrentGame.SurPlayerList[0].PictureShown", 0, 620, 141, 160, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, specialProperties: PickingBorder()),
            Image("SurPick1", "BorderedImage", "CurrentGame.SurPlayerList[1].PictureShown", 143, 620, 141, 160, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, specialProperties: PickingBorder()),
            Image("SurPick2", "BorderedImage", "CurrentGame.SurPlayerList[2].PictureShown", 286, 620, 141, 160, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, specialProperties: PickingBorder()),
            Image("SurPick3", "BorderedImage", "CurrentGame.SurPlayerList[3].PictureShown", 428, 620, 140, 160, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, specialProperties: PickingBorder()),
            Folded("SurPickingBorder0", "SurPick0", "Folded into SurPick0 picking border metadata."),
            Folded("SurPickingBorder1", "SurPick1", "Folded into SurPick1 picking border metadata."),
            Folded("SurPickingBorder2", "SurPick2", "Folded into SurPick2 picking border metadata."),
            Folded("SurPickingBorder3", "SurPick3", "Folded into SurPick3 picking border metadata."),
            Image("Map", "BorderedImage", "CurrentGame.PickedMapImageLarge", 572, 616, 297, 194, zIndex: -1, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill"),
            Text("MapName", "MapNameText", "CurrentGame.PickedMap", "BpWindow.MapName", 572, 616, 296, 30, zIndex: 1),
            Text("GameProgress", "GameProgressText", null, "BpWindow.GameProgress", 572, 646, 296, 20, zIndex: 1),
            Image("HunGlobalBan0", "Image", "CurrentGame.HunTeam.GlobalBannedHunList[0].HeaderImageSingleColor", 1380.5, 50.5, 45, 45, specialProperties: GlobalBanLock("Hun", 0)),
            Image("HunGlobalBan1", "Image", "CurrentGame.HunTeam.GlobalBannedHunList[1].HeaderImageSingleColor", 1380.5, 151.5, 45, 45, specialProperties: GlobalBanLock("Hun", 1)),
            Image("HunGlobalBan2", "Image", "CurrentGame.HunTeam.GlobalBannedHunList[2].HeaderImageSingleColor", 1380.5, 250, 45, 45, specialProperties: GlobalBanLock("Hun", 2)),
            Folded("HunGlobalBanLock0", "HunGlobalBan0", "Folded into HunGlobalBan0 lock overlay metadata."),
            Folded("HunGlobalBanLock1", "HunGlobalBan1", "Folded into HunGlobalBan1 lock overlay metadata."),
            Folded("HunGlobalBanLock2", "HunGlobalBan2", "Folded into HunGlobalBan2 lock overlay metadata."),
            Image("SurGlobalBan0", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[0].HeaderImageSingleColor", 13, 50.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 0)),
            Image("SurGlobalBan1", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[1].HeaderImageSingleColor", 65.5, 50.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 1)),
            Image("SurGlobalBan2", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[2].HeaderImageSingleColor", 118, 50.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 2)),
            Image("SurGlobalBan3", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[3].HeaderImageSingleColor", 169, 50.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 3)),
            Image("SurGlobalBan4", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[4].HeaderImageSingleColor", 13, 150.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 4)),
            Image("SurGlobalBan5", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[5].HeaderImageSingleColor", 65.5, 150.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 5)),
            Image("SurGlobalBan6", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[6].HeaderImageSingleColor", 118, 150.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 6)),
            Image("SurGlobalBan7", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[7].HeaderImageSingleColor", 169, 150.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 7)),
            Image("SurGlobalBan8", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[8].HeaderImageSingleColor", 13, 250, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 8)),
            Image("SurGlobalBan9", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[9].HeaderImageSingleColor", 65.5, 250, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 9)),
            Image("SurGlobalBan10", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[10].HeaderImageSingleColor", 118, 250, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 10)),
            Image("SurGlobalBan11", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[11].HeaderImageSingleColor", 169, 250, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 11)),
            Folded("SurGlobalBanLock0", "SurGlobalBan0", "Folded into SurGlobalBan0 lock overlay metadata."),
            Folded("SurGlobalBanLock1", "SurGlobalBan1", "Folded into SurGlobalBan1 lock overlay metadata."),
            Folded("SurGlobalBanLock2", "SurGlobalBan2", "Folded into SurGlobalBan2 lock overlay metadata."),
            Folded("SurGlobalBanLock3", "SurGlobalBan3", "Folded into SurGlobalBan3 lock overlay metadata."),
            Folded("SurGlobalBanLock4", "SurGlobalBan4", "Folded into SurGlobalBan4 lock overlay metadata."),
            Folded("SurGlobalBanLock5", "SurGlobalBan5", "Folded into SurGlobalBan5 lock overlay metadata."),
            Folded("SurGlobalBanLock6", "SurGlobalBan6", "Folded into SurGlobalBan6 lock overlay metadata."),
            Folded("SurGlobalBanLock7", "SurGlobalBan7", "Folded into SurGlobalBan7 lock overlay metadata."),
            Folded("SurGlobalBanLock8", "SurGlobalBan8", "Folded into SurGlobalBan8 lock overlay metadata."),
            Folded("SurGlobalBanLock9", "SurGlobalBan9", "Folded into SurGlobalBan9 lock overlay metadata."),
            Folded("SurGlobalBanLock10", "SurGlobalBan10", "Folded into SurGlobalBan10 lock overlay metadata."),
            Folded("SurGlobalBanLock11", "SurGlobalBan11", "Folded into SurGlobalBan11 lock overlay metadata."),
            Image("HunPick", "BorderedImage", "CurrentGame.HunPlayer.PictureShown", 872, 620, 568, 161, sizingMode: ImageSizingMode.OverflowCrop, stretch: "Uniform", clipToBounds: true, specialProperties: PickingBorder()),
            Folded("HunPickingBorder", "HunPick", "Folded into HunPick picking border metadata."),
            Text("SurId0", "Text", "CurrentGame.SurPlayerList[0].Member.Name", "BpWindow.PlayerId", 1, 781, 139, 28),
            Text("SurId1", "Text", "CurrentGame.SurPlayerList[1].Member.Name", "BpWindow.PlayerId", 145, 781, 139, 28),
            Text("SurId2", "Text", "CurrentGame.SurPlayerList[2].Member.Name", "BpWindow.PlayerId", 287, 781, 139, 28),
            Text("SurId3", "Text", "CurrentGame.SurPlayerList[3].Member.Name", "BpWindow.PlayerId", 430, 781, 139, 28),
            Text("HunId", "Text", "CurrentGame.HunPlayer.Member.Name", "BpWindow.PlayerId", 871, 781, 569, 28)
        ]);

        AddBlueprints(result, "CutSceneWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Image("SurTeamLogo", "Image", "CurrentGame.SurTeam.Logo", 251, 14, 85, 85, cornerRadius: 8, stretch: "Fill"),
            Text("SurTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentSurTeamMajorText", "CutSceneWindow.MajorPoints", 380, 42),
            Text("SurTeamName", "Text", "CurrentGame.SurTeam.Name", "CutSceneWindow.TeamName", 10, 38, 207, null, textWrapping: "WrapWithOverflow", contentMarginRight: -14, horizontalAlignment: "Stretch"),
            Text("HunTeamName", "Text", "CurrentGame.HunTeam.Name", "CutSceneWindow.TeamName", 1223, 38, 207, null, textWrapping: "WrapWithOverflow", contentMarginLeft: -14, horizontalAlignment: "Left"),
            Text("HunTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentHunTeamMajorText", "CutSceneWindow.MajorPoints", 971, 42),
            Image("HunTeamLogo", "Image", "CurrentGame.HunTeam.Logo", 1104, 14, 84, 85, cornerRadius: 8, stretch: "Fill"),
            Image("Map", "BorderedImage", "CurrentGame.PickedMapImage", 488, 0, 463, 112, zIndex: -1, sizingMode: ImageSizingMode.FillContainer, stretch: "UniformToFill"),
            Rectangle("MapMask", "#FF000000", 487, 83, 465, 29),
            Text("MapName", "MapNameText", "CurrentGame.PickedMap", "CutSceneWindow.MapName", 488, 51, 463, null),
            Text("GameProgress", "GameProgressText", null, "CutSceneWindow.GameProgress", 488, 82, 463, 30, zIndex: 1),
            Image("SurPick0", "BorderedImage", "CurrentGame.SurPlayerList[0].Character.BigImage", 1, 115, 346, 308.5, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, verticalAlignment: "Top", specialProperties: Props(("ImageWidth", "556.5"))),
            Image("SurPick1", "BorderedImage", "CurrentGame.SurPlayerList[1].Character.BigImage", 351, 115, 346, 308.5, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, verticalAlignment: "Top", specialProperties: Props(("ImageWidth", "556.5"))),
            Image("SurPick2", "BorderedImage", "CurrentGame.SurPlayerList[2].Character.BigImage", 1, 465, 346, 306.5, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, verticalAlignment: "Top", specialProperties: Props(("ImageWidth", "556.5"))),
            Image("SurPick3", "BorderedImage", "CurrentGame.SurPlayerList[3].Character.BigImage", 350, 465, 346, 306.5, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, verticalAlignment: "Top", specialProperties: Props(("ImageWidth", "556.5"))),
            Image("HunPick", "BorderedImage", "CurrentGame.HunPlayer.Character.BigImage", 702, 114.5, 739, 635, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, verticalAlignment: "Top"),
            Text("SurId0", "Text", "CurrentGame.SurPlayerList[0].Member.Name", "CutSceneWindow.SurPlayerId", 10, 422),
            Text("SurId1", "Text", "CurrentGame.SurPlayerList[1].Member.Name", "CutSceneWindow.SurPlayerId", 353, 425, null, 32),
            Text("SurId2", "Text", "CurrentGame.SurPlayerList[2].Member.Name", "CutSceneWindow.SurPlayerId", 1, 776, null, 31),
            Text("SurId3", "Text", "CurrentGame.SurPlayerList[3].Member.Name", "CutSceneWindow.SurPlayerId", 364, 774, null, 32),
            Text("HunId", "Text", "CurrentGame.HunPlayer.Member.Name", "CutSceneWindow.HunPlayerId", 720, 755, 382, 55),
            Talent("SurTalent0", TalentTraitDisplayKind.SurvivorTalent, 0, 164, 424, 178, 36, "Right"),
            Talent("SurTalent1", TalentTraitDisplayKind.SurvivorTalent, 1, 522, 424, 172, 37, "Right"),
            Talent("SurTalent2", TalentTraitDisplayKind.SurvivorTalent, 2, 160, 774, 182, 37, "Right"),
            Talent("SurTalent3", TalentTraitDisplayKind.SurvivorTalent, 3, 514, 771, null, 37, "Right"),
            Talent("HunTalent", TalentTraitDisplayKind.HunterTalent, null, 1102, 762, 173, 43, "Left"),
            Talent("Trait", TalentTraitDisplayKind.HunterTrait, null, 1290, 753, 56, 56, "Left")
        ]);

        AddBlueprints(result, "GameDataWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Image("SurTeamLogo", "Image", "CurrentGame.SurTeam.Logo", 96, 177, 85, 85, cornerRadius: 8, stretch: "Fill"),
            Text("SurTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentSurTeamMajorText", "GameDataWindow.MajorPoints", 285, 229),
            Text("SurTeamName", "Text", "CurrentGame.SurTeam.Name", "GameDataWindow.TeamName", 186, 176, 290, null, textWrapping: "WrapWithOverflow"),
            Text("GameScoresSur", "Text", "CurrentGame.MatchScore.CurrentSurTeamMinorScoreText", "GameDataWindow.GameScores", 476, 182, 52, 81),
            Image("Map", "BorderedImage", "CurrentGame.PickedMapImage", 556, 151, 328, 132, zIndex: -1, sizingMode: ImageSizingMode.FillContainer, stretch: "UniformToFill"),
            Removed("MapMask", "The legacy map mask is part of the old Map visual and has no separate Designer v3 control."),
            Text("MapName", "MapNameText", "CurrentGame.PickedMap", "GameDataWindow.MapName", 556, 220, 328, 30, zIndex: 1),
            Folded("PickedMapName", "MapName", "Folded into the MapName business control, which renders the picked map name."),
            Text("GameProgress", "GameProgressText", null, "GameDataWindow.GameProgress", 556, 253, 328, 30, zIndex: 1),
            Text("GameScoresHun", "Text", "CurrentGame.MatchScore.CurrentHunTeamMinorScoreText", "GameDataWindow.GameScores", 919, 182, 52, 81),
            Text("HunTeamName", "Text", "CurrentGame.HunTeam.Name", "GameDataWindow.TeamName", 976, 177, 302, null, textWrapping: "WrapWithOverflow"),
            Text("HunTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentHunTeamMajorText", "GameDataWindow.MajorPoints", 1081, 236),
            Image("HunTeamLogo", "Image", "CurrentGame.HunTeam.Logo", 1278, 176, 85, 86, cornerRadius: 8, stretch: "Fill"),
            Header("Header_Character", "Character", 47, 307, 80),
            Header("Header_ID", "ID", 154, 307, 100),
            Header("Header_DecodingProgress", "DecodingProgress", 331, 307, 150),
            Header("Header_PalletStrikes", "PalletStrikes", 485, 307, 150),
            Header("Header_Rescues", "Rescues", 634, 307, 120),
            Header("Header_Heals", "Heals", 774, 307, 120),
            Header("Header_ContainmentTime", "ContainmentTime", 894, 307, 176),
            Image("Player0Header", "BorderedImage", "CurrentGame.SurPlayerList[0].PictureShownHeader", 47, 354, 50, 50, sizingMode: ImageSizingMode.Auto, stretch: "Uniform"),
            Image("Player1Header", "BorderedImage", "CurrentGame.SurPlayerList[1].PictureShownHeader", 47, 414, 50, 50, sizingMode: ImageSizingMode.Auto, stretch: "Uniform"),
            Image("Player2Header", "BorderedImage", "CurrentGame.SurPlayerList[2].PictureShownHeader", 47, 473, 50, 50, sizingMode: ImageSizingMode.Auto, stretch: "Uniform"),
            Image("Player3Header", "BorderedImage", "CurrentGame.SurPlayerList[3].PictureShownHeader", 47, 534, 50, 50, sizingMode: ImageSizingMode.Auto, stretch: "Uniform"),
            Text("SurId0", "Text", "CurrentGame.SurPlayerList[0].Member.Name", "GameDataWindow.PlayerId", 115, 354),
            Text("SurId1", "Text", "CurrentGame.SurPlayerList[1].Member.Name", "GameDataWindow.PlayerId", 115, 414),
            Text("SurId2", "Text", "CurrentGame.SurPlayerList[2].Member.Name", "GameDataWindow.PlayerId", 115, 474),
            Text("SurId3", "Text", "CurrentGame.SurPlayerList[3].Member.Name", "GameDataWindow.PlayerId", 115, 534),
            Data("Sur0MachineDecoded", "CurrentGame.SurPlayerList[0].Data.DecodingProgress", "GameDataWindow.SurData", 377, 354),
            Data("Sur1MachineDecoded", "CurrentGame.SurPlayerList[1].Data.DecodingProgress", "GameDataWindow.SurData", 377, 414),
            Data("Sur2MachineDecoded", "CurrentGame.SurPlayerList[2].Data.DecodingProgress", "GameDataWindow.SurData", 377, 474),
            Data("Sur3MachineDecoded", "CurrentGame.SurPlayerList[3].Data.DecodingProgress", "GameDataWindow.SurData", 377, 534),
            Data("Sur0PalletStunTimes", "CurrentGame.SurPlayerList[0].Data.PalletStrikes", "GameDataWindow.SurData", 531, 354),
            Data("Sur1PalletStunTimes", "CurrentGame.SurPlayerList[1].Data.PalletStrikes", "GameDataWindow.SurData", 531, 413),
            Data("Sur2PalletStunTimes", "CurrentGame.SurPlayerList[2].Data.PalletStrikes", "GameDataWindow.SurData", 531, 474),
            Data("Sur3PalletStunTimes", "CurrentGame.SurPlayerList[3].Data.PalletStrikes", "GameDataWindow.SurData", 531, 534),
            Data("Sur0RescueTimes", "CurrentGame.SurPlayerList[0].Data.Rescues", "GameDataWindow.SurData", 666, 354),
            Data("Sur1RescueTimes", "CurrentGame.SurPlayerList[1].Data.Rescues", "GameDataWindow.SurData", 666, 414),
            Data("Sur2RescueTimes", "CurrentGame.SurPlayerList[2].Data.Rescues", "GameDataWindow.SurData", 666, 474),
            Data("Sur3RescueTimes", "CurrentGame.SurPlayerList[3].Data.Rescues", "GameDataWindow.SurData", 666, 534),
            Data("Sur0HealedTimes", "CurrentGame.SurPlayerList[0].Data.Heals", "GameDataWindow.SurData", 809, 354),
            Data("Sur1HealedTimes", "CurrentGame.SurPlayerList[1].Data.Heals", "GameDataWindow.SurData", 809, 414),
            Data("Sur2HealedTimes", "CurrentGame.SurPlayerList[2].Data.Heals", "GameDataWindow.SurData", 809, 474),
            Data("Sur3HealedTimes", "CurrentGame.SurPlayerList[3].Data.Heals", "GameDataWindow.SurData", 809, 534),
            Data("Sur0KiteTime", "CurrentGame.SurPlayerList[0].Data.ContainmentTime", "GameDataWindow.SurData", 963, 354),
            Data("Sur1KiteTime", "CurrentGame.SurPlayerList[1].Data.ContainmentTime", "GameDataWindow.SurData", 963, 413),
            Data("Sur2KiteTime", "CurrentGame.SurPlayerList[2].Data.ContainmentTime", "GameDataWindow.SurData", 963, 474),
            Data("Sur3KiteTime", "CurrentGame.SurPlayerList[3].Data.ContainmentTime", "GameDataWindow.SurData", 963, 534),
            Image("HunImage", "BorderedImage", "CurrentGame.HunPlayer.PictureShownHeader", 1075, 295, 314, 96, sizingMode: ImageSizingMode.FillContainer, stretch: "UniformToFill"),
            Text("HunId", "Text", "CurrentGame.HunPlayer.Member.Name", "GameDataWindow.PlayerId", 1080, 357, null, 35),
            Header("Header_RemainingCiphers", "RemainingCiphers", 1085, 404, 160, "GameDataWindow.HunDataHeader"),
            Header("Header_PalletsDestroyed", "PalletsDestroyed", 1085, 440, 160, "GameDataWindow.HunDataHeader"),
            Header("Header_SurvivorHits", "SurvivorHits", 1085, 475, 160, "GameDataWindow.HunDataHeader"),
            Header("Header_TerrorShocks", "TerrorShocks", 1085, 511, 160, "GameDataWindow.HunDataHeader"),
            Header("Header_Knockdowns", "Knockdowns", 1085, 548, 160, "GameDataWindow.HunDataHeader"),
            Data("HunMachineLeft", "CurrentGame.HunPlayer.Data.RemainingCipher", "GameDataWindow.HunData", 1280, 405),
            Data("HunPalletBroken", "CurrentGame.HunPlayer.Data.PalletsDestroyed", "GameDataWindow.HunData", 1280, 442),
            Data("HunHitTimes", "CurrentGame.HunPlayer.Data.SurvivorHits", "GameDataWindow.HunData", 1280, 478),
            Data("HunTerrorShockTimes", "CurrentGame.HunPlayer.Data.TerrorShocks", "GameDataWindow.HunData", 1280, 514),
            Data("HunDownTimes", "CurrentGame.HunPlayer.Data.Knockdowns", "GameDataWindow.HunData", 1280, 547)
        ]);

        AddBlueprints(result, "ScoreSurWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Image("SurTeamLogo", "Image", "CurrentGame.SurTeam.Logo", 22, 18, 115, 114, cornerRadius: 8, stretch: "Fill"),
            Text("SurTeamName", "Text", "CurrentGame.SurTeam.Name", "ScoreWindow.TeamName", 153, 34, 231, null),
            Text("SurTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentSurTeamMajorText", "ScoreWindow.MajorPoints", 209, 86),
            Text("GameScoresSur", "Text", "CurrentGame.MatchScore.CurrentSurTeamMinorScoreText", "ScoreWindow.GameScores", 389, 11, 64, 130)
        ]);

        AddBlueprints(result, "ScoreHunWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Image("HunTeamLogo", "Image", "CurrentGame.HunTeam.Logo", 352, 18, 116, 114, cornerRadius: 8, stretch: "Fill"),
            Text("HunTeamName", "Text", "CurrentGame.HunTeam.Name", "ScoreWindow.TeamName", 99, 33, 231, null),
            Text("HunTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentHunTeamMajorText", "ScoreWindow.MajorPoints", 167, 85),
            Text("GameScoresHun", "Text", "CurrentGame.MatchScore.CurrentHunTeamMinorScoreText", "ScoreWindow.GameScores", 21, 10, 64, 130)
        ]);

        AddBlueprints(result, "ScoreGlobalWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Text("MainTeamName", "Text", "HomeTeam.Name", "ScoreWindow.ScoreGlobal_TeamName", 13, 96, 144, 26, targetName: "HomeTeamName"),
            Text("AwayTeamName", "Text", "AwayTeam.Name", "ScoreWindow.ScoreGlobal_TeamName", 13, 155, 144, null),
            Text("MainScoreTotal", "Text", "CurrentGame.MatchScore.HomeTotalMinorScore", "ScoreWindow.ScoreGlobal_Total", 1303, 89, 86, null, targetName: "HomeScoreTotal"),
            Text("AwayScoreTotal", "Text", "CurrentGame.MatchScore.AwayTotalMinorScore", "ScoreWindow.ScoreGlobal_Total", 1302, 147, 87, null),
            ScoreRow("HomeGlobalScoreRow", TeamType.HomeTeam, "ScoreWindow.ScoreGlobal_Data"),
            ScoreRow("AwayGlobalScoreRow", TeamType.AwayTeam, "ScoreWindow.ScoreGlobal_Data")
        ]);

        AddBlueprints(result, "WidgetsWindow", "BpOverViewCanvas",
        [
            Removed("BpOverViewCanvas", "The legacy overview Canvas is split into BpOverviewWindow/BaseCanvas."),
            Image("SurTeamLogo", "Image", "CurrentGame.SurTeam.Logo", 42, 30, 85, 85, cornerRadius: 8, stretch: "Fill"),
            Text("SurTeamNameInOverview", "Text", "CurrentGame.SurTeam.Name", "WidgetsWindow.BpOverview_TeamName", 0, 132, 166, null, textWrapping: "WrapWithOverflow"),
            Text("HunTeamNameInOverview", "Text", "CurrentGame.HunTeam.Name", "WidgetsWindow.BpOverview_TeamName", 960, 132, 166, null, textWrapping: "WrapWithOverflow"),
            Image("HunTeamLogo", "Image", "CurrentGame.HunTeam.Logo", 1000, 30, 86, 85, cornerRadius: 8, stretch: "Fill"),
            Image("HunBanCurrent0", "Image", "CurrentGame.CurrentHunBannedList[0].HeaderImageSingleColor", 644, 5, 145, 35, specialProperties: CurrentBanLock("Hun", 0)),
            Image("HunBanCurrent1", "Image", "CurrentGame.CurrentHunBannedList[1].HeaderImageSingleColor", 794, 5, 141, 35, specialProperties: CurrentBanLock("Hun", 1)),
            Folded("HunBanCurrentLock0", "HunBanCurrent0", "Folded into HunBanCurrent0 lock overlay metadata."),
            Folded("HunBanCurrentLock1", "HunBanCurrent1", "Folded into HunBanCurrent1 lock overlay metadata."),
            Image("SurBanCurrent3", "Image", "CurrentGame.CurrentSurBannedList[3].HeaderImageSingleColor", 416, 5, 68, 35, specialProperties: CurrentBanLock("Sur", 3)),
            Image("SurBanCurrent2", "Image", "CurrentGame.CurrentSurBannedList[2].HeaderImageSingleColor", 340, 5, 71, 35, specialProperties: CurrentBanLock("Sur", 2)),
            Image("SurBanCurrent1", "Image", "CurrentGame.CurrentSurBannedList[1].HeaderImageSingleColor", 265, 5, 71, 35, specialProperties: CurrentBanLock("Sur", 1)),
            Image("SurBanCurrent0", "Image", "CurrentGame.CurrentSurBannedList[0].HeaderImageSingleColor", 193, 5, 68, 35, specialProperties: CurrentBanLock("Sur", 0)),
            Folded("SurBanCurrentLock0", "SurBanCurrent0", "Folded into SurBanCurrent0 lock overlay metadata."),
            Folded("SurBanCurrentLock1", "SurBanCurrent1", "Folded into SurBanCurrent1 lock overlay metadata."),
            Folded("SurBanCurrentLock2", "SurBanCurrent2", "Folded into SurBanCurrent2 lock overlay metadata."),
            Folded("SurBanCurrentLock3", "SurBanCurrent3", "Folded into SurBanCurrent3 lock overlay metadata."),
            Image("SurPick0", "BorderedImage", "CurrentGame.SurPlayerList[0].Character.HalfImage", 193, 65, 68, 110, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true),
            Image("SurPick1", "BorderedImage", "CurrentGame.SurPlayerList[1].Character.HalfImage", 265, 65, 71, 110, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true),
            Image("SurPick2", "BorderedImage", "CurrentGame.SurPlayerList[2].Character.HalfImage", 340, 65, 72, 110, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true),
            Image("SurPick3", "BorderedImage", "CurrentGame.SurPlayerList[3].Character.HalfImage", 416, 65, 68, 110, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true),
            Text("GameProgress", "GameProgressText", null, "WidgetsWindow.BpOverview_GameProgress", 471, 0, 178, 50, zIndex: 1),
            Text("GameScoresSur", "Text", "CurrentGame.MatchScore.CurrentSurTeamMinorScoreText", "WidgetsWindow.BpOverview_GameScores", 495, 94, 52, 62),
            Text("RatioChar", "Text", null, "WidgetsWindow.BpOverview_GameScores", 552, 89, 25, 62, staticText: ":"),
            Text("GameScoresHun", "Text", "CurrentGame.MatchScore.CurrentHunTeamMinorScoreText", "WidgetsWindow.BpOverview_GameScores", 583, 94, 52, 62),
            Image("HunPick", "BorderedImage", "CurrentGame.HunPlayer.Character.HalfImage", 644, 45, 291, 130, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true)
        ]);

        AddBlueprints(result, "WidgetsWindow", "MapV2Canvas",
        [
            Removed("MapV2Canvas", "The legacy MapV2 Canvas is split into MapV2Window/BaseCanvas."),
            MapV2("Arms_Factory", "ArmsFactory", 50.5),
            MapV2("The_Red_Church", "TheRedChurch", 204),
            MapV2("Sacred_Heart_Hospital", "SacredHeartHospital", 359),
            MapV2("Leo_s_Memory", "LeosMemory", 514),
            MapV2("Moonlit_River_Park", "MoonlitRiverPark", 669),
            MapV2("Lakeside_Village", "LakesideVillage", 824),
            MapV2("Eversleeping_Town", "EversleepingTown", 979),
            MapV2("Chinatown", "ChinaTown", 1134),
            MapV2("Darkwoods", "Darkwoods", 1289)
        ]);

        AddBlueprints(result, "WidgetsWindow", "MapBpCanvas",
        [
            Unsupported("MapBpCanvas", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("PickedMap", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("PickedMapName", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("PickWord", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("SurTeamName", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("VS_Word", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("HunTeamName", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("BannedMap", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("BannedMapName", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("BanWord", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped.")
        ]);

        return result;
    }

    private static IReadOnlyDictionary<string, LegacyScoreGlobalCellBlueprint> CreateLegacyScoreGlobalCellBlueprints()
    {
        var result = new Dictionary<string, LegacyScoreGlobalCellBlueprint>(StringComparer.Ordinal);
        foreach (var team in new[] { "Home", "Away" })
        {
            foreach (var game in Enumerable.Range(1, 5))
            {
                AddLegacyScoreGlobalCell(result, team, game, "FirstHalf", isOvertime: false);
                AddLegacyScoreGlobalCell(result, team, game, "SecondHalf", isOvertime: false);
                AddLegacyScoreGlobalCell(result, team, game, "FirstHalf", isOvertime: true);
                AddLegacyScoreGlobalCell(result, team, game, "SecondHalf", isOvertime: true);
            }
        }

        return result;
    }

    private static void AddLegacyScoreGlobalCell(
        IDictionary<string, LegacyScoreGlobalCellBlueprint> result,
        string team,
        int game,
        string half,
        bool isOvertime)
    {
        var name = $"{team}TeamGame{game}{(isOvertime ? "Overtime" : string.Empty)}{half}";
        result.Add(name, new LegacyScoreGlobalCellBlueprint(team, game, half, isOvertime));

        // 较早的全局比分窗口使用 Main/Away 表示主客队，并以 Extra 表示加时。
        // 这些名称只作为明确的 legacy 特例解析，仍复用既有的比分行聚合逻辑。
        var legacyTeam = string.Equals(team, "Home", StringComparison.Ordinal) ? "MainTeam" : "Away";
        var legacyName = $"{legacyTeam}Game{game}{(isOvertime ? "Extra" : string.Empty)}{half}";
        result.Add(legacyName, new LegacyScoreGlobalCellBlueprint(team, game, half, isOvertime));
    }

    private static void AddBlueprints(
        IDictionary<LegacyLayoutKey, IReadOnlyList<LegacyControlBlueprint>> result,
        string window,
        string canvas,
        IReadOnlyList<LegacyControlBlueprint> blueprints)
    {
        result[new LegacyLayoutKey(window, canvas)] = blueprints
            .Select(blueprint => blueprint with
            {
                SourceWindow = window,
                SourceCanvas = canvas,
                TargetWindow = GetTargetWindowForBlueprint(window, canvas)
            })
            .ToArray();
    }

    private static string? GetTargetWindowForBlueprint(string window, string canvas) =>
        (window, canvas) switch
        {
            ("WidgetsWindow", "BpOverViewCanvas") => "BpOverviewWindow",
            ("WidgetsWindow", "MapV2Canvas") => "MapV2Window",
            ("WidgetsWindow", "MapBpCanvas") => null,
            (_, "BaseCanvas") => window,
            _ => null
        };

    private static LegacyControlBlueprint Text(
        string legacyName,
        string controlType,
        string? textBinding,
        string textStyleSourceKey,
        double? left,
        double? top,
        double? width = null,
        double? height = null,
        string? targetName = null,
        int zIndex = 0,
        string? staticText = null,
        string? textWrapping = null,
        string? horizontalAlignment = null,
        string? verticalAlignment = null,
        string? textAlignment = null,
        double contentMarginLeft = 0,
        double contentMarginTop = 0,
        double contentMarginRight = 0,
        double contentMarginBottom = 0)
    {
        var style = GetTextStyleDefaults(textStyleSourceKey);
        return new LegacyControlBlueprint
        {
            LegacyName = legacyName,
            TargetName = targetName ?? legacyName,
            TargetControlType = controlType,
            TextBinding = textBinding,
            StaticText = staticText,
            FontFamily = style.FontFamily,
            FontSize = style.FontSize,
            FontWeight = style.FontWeight,
            Color = style.Color,
            HorizontalAlignment = horizontalAlignment ?? style.HorizontalAlignment ?? "Center",
            VerticalAlignment = verticalAlignment ?? style.VerticalAlignment ?? "Center",
            TextAlignment = textAlignment ?? style.TextAlignment ?? "Center",
            TextWrapping = textWrapping ?? style.TextWrapping,
            ContentMarginLeft = contentMarginLeft,
            ContentMarginTop = contentMarginTop,
            ContentMarginRight = contentMarginRight,
            ContentMarginBottom = contentMarginBottom,
            ZIndex = zIndex,
            DefaultLeft = left,
            DefaultTop = top,
            DefaultWidth = width,
            DefaultHeight = height,
            TextStyleSourceKey = textStyleSourceKey,
            Status = LegacyControlBlueprintStatus.Mapped
        };
    }

    private static LegacyControlBlueprint Header(
        string legacyName,
        string staticText,
        double? left,
        double? top,
        double? width,
        string textStyleSourceKey = "GameDataWindow.SurDataHeader") =>
        Text(legacyName, "Text", null, textStyleSourceKey, left, top, width, null, staticText: staticText);

    private static LegacyControlBlueprint Data(
        string legacyName,
        string textBinding,
        string textStyleSourceKey,
        double? left,
        double? top) =>
        Text(legacyName, "Text", textBinding, textStyleSourceKey, left, top);

    private static LegacyControlBlueprint Image(
        string legacyName,
        string controlType,
        string? bindingPath,
        double? left,
        double? top,
        double? width,
        double? height,
        string? targetName = null,
        int zIndex = 0,
        ImageSizingMode? sizingMode = null,
        string? stretch = "Uniform",
        bool clipToBounds = false,
        double? cornerRadius = null,
        string? horizontalAlignment = "Center",
        string? verticalAlignment = "Center",
        string? resourceSourceKey = null,
        IReadOnlyDictionary<string, string>? specialProperties = null) =>
        new()
        {
            LegacyName = legacyName,
            TargetName = targetName ?? legacyName,
            TargetControlType = controlType,
            ImageBindingPath = bindingPath,
            SizingMode = sizingMode,
            Stretch = stretch,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = verticalAlignment,
            ClipToBounds = clipToBounds,
            CornerRadius = cornerRadius,
            ZIndex = zIndex,
            DefaultLeft = left,
            DefaultTop = top,
            DefaultWidth = width,
            DefaultHeight = height,
            ResourceSourceKey = resourceSourceKey
                                ?? (specialProperties is not null
                                    && specialProperties.TryGetValue("ResourceSourceKey", out var value)
                                        ? value
                                        : null),
            SpecialProperties = specialProperties?.ToDictionary(StringComparer.Ordinal) ?? [],
            Status = LegacyControlBlueprintStatus.Mapped
        };

    private static LegacyControlBlueprint Rectangle(
        string legacyName,
        string fillColor,
        double? left,
        double? top,
        double? width,
        double? height,
        int zIndex = 0) =>
        new()
        {
            LegacyName = legacyName,
            TargetName = legacyName,
            TargetControlType = "Rectangle",
            Color = fillColor,
            ZIndex = zIndex,
            DefaultLeft = left,
            DefaultTop = top,
            DefaultWidth = width,
            DefaultHeight = height,
            Status = LegacyControlBlueprintStatus.Mapped
        };

    private static LegacyControlBlueprint Talent(
        string legacyName,
        TalentTraitDisplayKind displayKind,
        int? playerIndex,
        double? left,
        double? top,
        double? width,
        double? height,
        string horizontalAlignment) =>
        new()
        {
            LegacyName = legacyName,
            TargetName = legacyName,
            TargetControlType = "TalentTraitDisplay",
            DefaultLeft = left,
            DefaultTop = top,
            DefaultWidth = width,
            DefaultHeight = height,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = "Center",
            SpecialProperties = playerIndex.HasValue
                ? Props(("DisplayKind", displayKind.ToString()), ("PlayerIndex", playerIndex.Value.ToString()))
                : Props(("DisplayKind", displayKind.ToString())),
            Status = LegacyControlBlueprintStatus.Mapped
        };

    private static LegacyControlBlueprint ScoreRow(
        string targetName,
        TeamType teamType,
        string textStyleSourceKey)
    {
        var style = GetTextStyleDefaults(textStyleSourceKey);
        return new LegacyControlBlueprint
        {
            LegacyName = targetName,
            TargetName = targetName,
            TargetControlType = "GlobalScoreRow",
            FontFamily = style.FontFamily,
            FontWeight = style.FontWeight,
            Color = style.Color,
            FontSize = style.FontSize,
            DefaultWidth = 1,
            DefaultHeight = 1,
            TextStyleSourceKey = textStyleSourceKey,
            SpecialProperties = Props(("TeamType", teamType.ToString())),
            Status = LegacyControlBlueprintStatus.Mapped
        };
    }

    private static LegacyControlBlueprint MapV2(string legacyName, string mapKey, double left) =>
        new()
        {
            LegacyName = legacyName,
            TargetName = legacyName,
            TargetControlType = "MapV2Display",
            DefaultLeft = left,
            DefaultTop = 0,
            DefaultWidth = string.Equals(legacyName, "Arms_Factory", StringComparison.Ordinal) ? 149 : 151,
            DefaultHeight = 160,
            SpecialProperties = Props(
                ("MapKey", mapKey),
                ("MapNameFontFamily", "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简"),
                ("MapNameFontWeight", "Normal"),
                ("MapNameColor", "#FFFFFFFF"),
                ("MapNameFontSize", "14"),
                ("TeamNameFontFamily", "pack://application:,,,/Assets/Fonts/#Noto Sans"),
                ("TeamNameFontWeight", "Normal"),
                ("TeamNameColor", "#FFFFFFFF"),
                ("TeamNameFontSize", "18"),
                ("CampNameFontFamily", "pack://application:,,,/Assets/Fonts/#Noto Sans"),
                ("CampNameFontWeight", "Normal"),
                ("CampNameColor", "#FFFFFFFF"),
                ("CampNameFontSize", "20"),
                ("MapBorderNormalColor", "#FF2B483B"),
                ("MapBorderBannedColor", "#FF9C3E2F"),
                ("PickingBorderImageResourceSourceKey", "MapBpV2PickingBorderImage"),
                ("PickingBorderColorResourceSourceKey", "MapBpV2PickingBorderColor")),
            Status = LegacyControlBlueprintStatus.Mapped
        };

    private static LegacyControlBlueprint Folded(string legacyName, string targetName, string reason) =>
        new()
        {
            LegacyName = legacyName,
            TargetName = targetName,
            Status = LegacyControlBlueprintStatus.Folded,
            UnsupportedReason = reason
        };

    private static LegacyControlBlueprint Removed(string legacyName, string reason) =>
        new()
        {
            LegacyName = legacyName,
            Status = LegacyControlBlueprintStatus.RemovedWithReason,
            UnsupportedReason = reason
        };

    private static LegacyControlBlueprint Unsupported(string legacyName, string reason) =>
        new()
        {
            LegacyName = legacyName,
            Status = LegacyControlBlueprintStatus.Unsupported,
            UnsupportedReason = reason
        };

    private static Dictionary<string, string> Props(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private static Dictionary<string, string> CurrentBanLock(string camp, int index) =>
        Props(
            ("Lockable", "true"),
            ("LockVisibleWhen", FrontedOverlayVisibilityMode.VisibleWhenFalse.ToString()),
            ("LockVisibilityBindingPath", $"CanCurrent{camp}BannedList[{index}]"),
            ("LockImagePath", "Resources/CurrentBanLock.png"),
            ("ResourceSourceKey", "CurrentBanLockImage"));

    private static Dictionary<string, string> GlobalBanLock(string camp, int index) =>
        Props(
            ("Lockable", "true"),
            ("LockVisibleWhen", FrontedOverlayVisibilityMode.VisibleWhenFalse.ToString()),
            ("LockVisibilityBindingPath", $"CanGlobal{camp}BannedList[{index}]"),
            ("LockImagePath", "Resources/GlobalBanLock.png"),
            ("ResourceSourceKey", "GlobalBanLockImage"));

    private static Dictionary<string, string> PickingBorder() =>
        Props(
            ("PickingBorderAvailable", "true"),
            ("ResourceSourceKey", "PickingBorderImage"),
            ("PickingBorderFillColorResourceSourceKey", "PickingBorderColor"));

    private static FrontedControlConfigBase CreateDefaultControl(LegacyControlBlueprint blueprint)
    {
        return blueprint.TargetControlType switch
        {
            "Text" => CreateDefaultText(blueprint),
            "MapNameText" => CreateDefaultMapNameText(blueprint),
            "GameProgressText" => CreateDefaultGameProgressText(blueprint),
            "Image" => CreateDefaultImage(blueprint),
            "BorderedImage" => CreateDefaultBorderedImage(blueprint),
            "Rectangle" => CreateDefaultRectangle(blueprint),
            "TalentTraitDisplay" => CreateDefaultTalentTrait(blueprint),
            "GlobalScoreRow" => CreateDefaultGlobalScoreRow(blueprint),
            "MapV2Display" => CreateDefaultMapV2Display(blueprint),
            _ => new FrontedControlConfigBase { ControlType = blueprint.TargetControlType }
        };
    }

    private static TextFrontedControlConfig CreateDefaultText(LegacyControlBlueprint blueprint)
    {
        return new TextFrontedControlConfig
        {
            Text = blueprint.StaticText,
            TextBinding = CreateTextBinding(blueprint.TextBinding),
            BindingPath = blueprint.BindingPath,
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            TextAlignment = blueprint.TextAlignment,
            TextWrapping = blueprint.TextWrapping,
            ContentMarginLeft = blueprint.ContentMarginLeft,
            ContentMarginTop = blueprint.ContentMarginTop,
            ContentMarginRight = blueprint.ContentMarginRight,
            ContentMarginBottom = blueprint.ContentMarginBottom,
            FontFamily = blueprint.FontFamily,
            FontWeight = blueprint.FontWeight,
            Color = blueprint.Color,
            FontSize = blueprint.FontSize.GetValueOrDefault(),
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
    }

    private static MapNameTextControlConfig CreateDefaultMapNameText(LegacyControlBlueprint blueprint)
    {
        return new MapNameTextControlConfig
        {
            BindingPath = blueprint.TextBinding ?? blueprint.BindingPath,
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            TextAlignment = blueprint.TextAlignment,
            FontFamily = blueprint.FontFamily,
            FontWeight = blueprint.FontWeight,
            Color = blueprint.Color,
            FontSize = blueprint.FontSize.GetValueOrDefault(),
            EmptyText = blueprint.StaticText,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
    }

    private static GameProgressTextControlConfig CreateDefaultGameProgressText(LegacyControlBlueprint blueprint)
    {
        return new GameProgressTextControlConfig
        {
            BindingPath = blueprint.BindingPath,
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            TextAlignment = blueprint.TextAlignment,
            FontFamily = blueprint.FontFamily,
            FontWeight = blueprint.FontWeight,
            Color = blueprint.Color,
            FontSize = blueprint.FontSize.GetValueOrDefault(),
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
    }

    private static ImageFrontedControlConfig CreateDefaultImage(LegacyControlBlueprint blueprint)
    {
        var image = new ImageFrontedControlConfig
        {
            BindingPath = blueprint.ImageBindingPath ?? blueprint.BindingPath,
            ImagePath = blueprint.ImagePath,
            SizingMode = blueprint.SizingMode ?? ImageSizingMode.FillContainer,
            Stretch = blueprint.Stretch,
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            ClipToBounds = blueprint.ClipToBounds,
            CornerRadius = blueprint.CornerRadius,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
        ApplyImageSpecialProperties(image, blueprint);
        return image;
    }

    private static BorderedImageFrontedControlConfig CreateDefaultBorderedImage(LegacyControlBlueprint blueprint)
    {
        var image = new BorderedImageFrontedControlConfig
        {
            BindingPath = blueprint.ImageBindingPath ?? blueprint.BindingPath,
            ImagePath = blueprint.ImagePath,
            SizingMode = blueprint.SizingMode ?? ImageSizingMode.OverflowCrop,
            Stretch = blueprint.Stretch,
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            ClipToBounds = blueprint.ClipToBounds,
            CornerRadius = blueprint.CornerRadius,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
        ApplyImageSpecialProperties(image, blueprint);
        return image;
    }

    private static RectangleFrontedControlConfig CreateDefaultRectangle(LegacyControlBlueprint blueprint)
    {
        return new RectangleFrontedControlConfig
        {
            FillColor = blueprint.Color,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
    }

    private static TalentTraitDisplayControlConfig CreateDefaultTalentTrait(LegacyControlBlueprint blueprint)
    {
        var control = new TalentTraitDisplayControlConfig
        {
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };

        if (blueprint.SpecialProperties.TryGetValue("DisplayKind", out var displayKind)
            && Enum.TryParse<TalentTraitDisplayKind>(displayKind, out var parsedKind))
        {
            control.DisplayKind = parsedKind;
        }

        if (blueprint.SpecialProperties.TryGetValue("PlayerIndex", out var indexText)
            && int.TryParse(indexText, out var index))
        {
            control.PlayerIndex = index;
        }

        return control;
    }

    private static GlobalScoreRowControlConfig CreateDefaultGlobalScoreRow(LegacyControlBlueprint blueprint)
    {
        var teamType = TeamType.HomeTeam;
        if (blueprint.SpecialProperties.TryGetValue("TeamType", out var teamTypeText)
            && Enum.TryParse<TeamType>(teamTypeText, out var parsedTeamType))
        {
            teamType = parsedTeamType;
        }

        return new GlobalScoreRowControlConfig
        {
            TeamType = teamType,
            FontFamily = blueprint.FontFamily,
            FontWeight = blueprint.FontWeight,
            Color = blueprint.Color,
            FontSize = blueprint.FontSize.GetValueOrDefault(24D),
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
    }

    private static MapV2DisplayControlConfig CreateDefaultMapV2Display(LegacyControlBlueprint blueprint)
    {
        return new MapV2DisplayControlConfig
        {
            MapKey = blueprint.SpecialProperties.GetValueOrDefault("MapKey") ?? string.Empty,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight,
            MapNameFontFamily = blueprint.SpecialProperties.GetValueOrDefault("MapNameFontFamily"),
            MapNameFontWeight = blueprint.SpecialProperties.GetValueOrDefault("MapNameFontWeight"),
            MapNameColor = blueprint.SpecialProperties.GetValueOrDefault("MapNameColor"),
            MapNameFontSize = ReadDoubleSpecialProperty(blueprint, "MapNameFontSize"),
            TeamNameFontFamily = blueprint.SpecialProperties.GetValueOrDefault("TeamNameFontFamily"),
            TeamNameFontWeight = blueprint.SpecialProperties.GetValueOrDefault("TeamNameFontWeight"),
            TeamNameColor = blueprint.SpecialProperties.GetValueOrDefault("TeamNameColor"),
            TeamNameFontSize = ReadDoubleSpecialProperty(blueprint, "TeamNameFontSize"),
            CampNameFontFamily = blueprint.SpecialProperties.GetValueOrDefault("CampNameFontFamily"),
            CampNameFontWeight = blueprint.SpecialProperties.GetValueOrDefault("CampNameFontWeight"),
            CampNameColor = blueprint.SpecialProperties.GetValueOrDefault("CampNameColor"),
            CampNameFontSize = ReadDoubleSpecialProperty(blueprint, "CampNameFontSize"),
            MapBorderNormalColor = blueprint.SpecialProperties.GetValueOrDefault("MapBorderNormalColor"),
            MapBorderBannedColor = blueprint.SpecialProperties.GetValueOrDefault("MapBorderBannedColor"),
            PickingBorderImagePath = blueprint.SpecialProperties.GetValueOrDefault("PickingBorderImagePath"),
            PickingBorderFillColor = blueprint.SpecialProperties.GetValueOrDefault("PickingBorderFillColor")
        };
    }

    private static void ApplyBlueprintDefaults(
        LegacyControlBlueprint blueprint,
        FrontedControlConfigBase control)
    {
        if (string.IsNullOrWhiteSpace(control.ControlType))
        {
            control.ControlType = blueprint.TargetControlType;
        }

        if (blueprint.DefaultLeft.HasValue)
        {
            control.Left = blueprint.DefaultLeft.Value;
        }

        if (blueprint.DefaultTop.HasValue)
        {
            control.Top = blueprint.DefaultTop.Value;
        }

        control.ZIndex = blueprint.ZIndex;
    }

    private static void ApplyImageSpecialProperties(ImageFrontedControlConfig image, LegacyControlBlueprint blueprint)
    {
        if (ReadBoolSpecialProperty(blueprint, "Lockable"))
        {
            image.Lockable = true;
        }

        if (blueprint.SpecialProperties.TryGetValue("LockImagePath", out var lockImagePath))
        {
            image.LockImagePath = lockImagePath;
        }

        if (blueprint.SpecialProperties.TryGetValue("LockVisibilityBindingPath", out var lockVisibilityBindingPath))
        {
            image.LockVisibilityBindingPath = lockVisibilityBindingPath;
        }

        if (blueprint.SpecialProperties.TryGetValue("LockVisibleWhen", out var lockVisibleWhen)
            && Enum.TryParse<FrontedOverlayVisibilityMode>(lockVisibleWhen, out var parsedVisibleWhen))
        {
            image.LockVisibleWhen = parsedVisibleWhen;
        }

        if (blueprint.SpecialProperties.TryGetValue("PickingBorderAvailable", out var pickingBorderAvailable)
            && bool.TryParse(pickingBorderAvailable, out var parsedPickingBorderAvailable))
        {
            image.PickingBorderAvailable = parsedPickingBorderAvailable;
        }

        if (blueprint.SpecialProperties.TryGetValue("PickingBorderImagePath", out var pickingBorderImagePath))
        {
            image.PickingBorderImagePath = pickingBorderImagePath;
        }

        if (blueprint.SpecialProperties.TryGetValue("PickingBorderFillColor", out var pickingBorderFillColor))
        {
            image.PickingBorderFillColor = pickingBorderFillColor;
        }

        if (image is BorderedImageFrontedControlConfig bordered)
        {
            bordered.ImageWidth = ReadNullableDoubleSpecialProperty(blueprint, "ImageWidth");
            bordered.ImageHeight = ReadNullableDoubleSpecialProperty(blueprint, "ImageHeight");
        }
    }

}

#pragma warning restore CS1591
