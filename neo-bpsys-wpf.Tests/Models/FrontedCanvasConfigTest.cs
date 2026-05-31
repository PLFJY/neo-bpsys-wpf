using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Controls.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedCanvasConfigTest
{
    [Fact]
    public void ReadsRootLevelV3CanvasAndControls()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "BackgroundImage": "Resources/bp.png",
              "SurTeamName": {
                "ControlType": "Text",
                "Left": 580.5,
                "Top": 720,
                "Width": 120,
                "Height": null,
                "BindingPath": "CurrentGame.SurTeam.Name",
                "Text": "Ignored when BindingPath exists",
                "StringFormat": "{0}%",
                "HorizontalAlignment": "Center",
                "VerticalAlignment": "Center",
                "TextAlignment": "Center",
                "TextWrapping": "WrapWithOverflow",
                "FontFamily": "pack://application:,,,/Assets/Fonts/#Noto Sans",
                "FontWeight": "Bold",
                "Color": "#FFFFFFFF",
                "FontSize": 28,
                "ZIndex": 2
              },
              "SurPick1": {
                "ControlType": "Image",
                "Left": 143,
                "Top": 620,
                "Width": 141,
                "Height": 160,
                "BindingPath": "CurrentGame.SurPlayerList[1].PictureShown",
                "ZIndex": 1,
                "Stretch": "Fill",
                "HorizontalAlignment": "Center",
                "VerticalAlignment": "Top",
                "ClipToBounds": true,
                "CornerRadius": 8,
                "PickingBorder": true,
                "PickingBorderImagePath": "Resources/pickingBorder.png",
                "BanLockAvailable": true
              }
            }
            """);

        Assert.NotNull(config);
        Assert.Equal(3, config.Version);
        Assert.Equal(1440, config.CanvasWidth);
        Assert.Equal(810, config.CanvasHeight);
        Assert.Equal("Resources/bp.png", config.BackgroundImage);

        var text = Assert.IsType<TextFrontedControlConfig>(config.Controls["SurTeamName"]);
        Assert.Equal(580.5, text.Left);
        Assert.Equal(120, text.Width);
        Assert.Null(text.Height);
        Assert.Equal("CurrentGame.SurTeam.Name", text.BindingPath);
        Assert.Equal("Ignored when BindingPath exists", text.Text);
        Assert.Equal("{0}%", text.StringFormat);
        Assert.Equal("Bold", text.FontWeight);
        Assert.Equal(28, text.FontSize);
        Assert.Equal(2, text.ZIndex);

        var image = Assert.IsType<ImageFrontedControlConfig>(config.Controls["SurPick1"]);
        Assert.Equal(143, image.Left);
        Assert.Equal(160, image.Height);
        Assert.Equal("Fill", image.Stretch);
        Assert.Equal("Center", image.HorizontalAlignment);
        Assert.Equal("Top", image.VerticalAlignment);
        Assert.True(image.ClipToBounds);
        Assert.True(image.CornerRadius.HasValue);
        Assert.Equal(8, image.CornerRadius.Value);
        Assert.True(image.PickingBorder);
        Assert.Equal("Resources/pickingBorder.png", image.PickingBorderImagePath);
        Assert.True(image.BanLockAvailable);
    }

    [Fact]
    public void ReadsTextControlStaticText()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "Title": {
                "ControlType": "Text",
                "Left": 10,
                "Top": 20,
                "Text": "静态标题",
                "FontSize": 28
              }
            }
            """);

        Assert.NotNull(config);
        var text = Assert.IsType<TextFrontedControlConfig>(config.Controls["Title"]);
        Assert.Null(text.BindingPath);
        Assert.Equal("静态标题", text.Text);
    }

    [Fact]
    public void ReadsBuiltInScoreSurWindowLayout()
    {
        var config = ReadBuiltInLayout("ScoreSurWindow");

        Assert.NotNull(config);
        Assert.Equal(3, config.Version);
        Assert.Equal(480, config.CanvasWidth);
        Assert.Equal(152, config.CanvasHeight);
        Assert.Equal("Resources/scoreSur.png", config.BackgroundImage);

        Assert.Contains("SurTeamLogo", config.Controls.Keys);
        Assert.Contains("SurTeamName", config.Controls.Keys);
        Assert.Contains("SurTeamMajorPoint", config.Controls.Keys);
        Assert.Contains("GameScoresSur", config.Controls.Keys);

        var logo = AssertImageBinding(config, "SurTeamLogo", "CurrentGame.SurTeam.Logo");
        Assert.True(logo.CornerRadius.HasValue);
        Assert.Equal(8, logo.CornerRadius.Value);
        AssertTextBinding(config, "SurTeamName", "CurrentGame.SurTeam.Name");
        AssertTextBinding(config, "SurTeamMajorPoint", "CurrentGame.MatchScore.CurrentSurTeamMajorText");
        AssertTextBinding(config, "GameScoresSur", "CurrentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText");
    }

    [Fact]
    public void ReadsBuiltInScoreHunWindowLayout()
    {
        var config = ReadBuiltInLayout("ScoreHunWindow");

        Assert.NotNull(config);
        Assert.Equal(3, config.Version);
        Assert.Equal(480, config.CanvasWidth);
        Assert.Equal(152, config.CanvasHeight);
        Assert.Equal("Resources/scoreHun.png", config.BackgroundImage);

        Assert.Contains("HunTeamLogo", config.Controls.Keys);
        Assert.Contains("HunTeamName", config.Controls.Keys);
        Assert.Contains("HunTeamMajorPoint", config.Controls.Keys);
        Assert.Contains("GameScoresHun", config.Controls.Keys);

        var logo = AssertImageBinding(config, "HunTeamLogo", "CurrentGame.HunTeam.Logo");
        Assert.True(logo.CornerRadius.HasValue);
        Assert.Equal(8, logo.CornerRadius.Value);
        AssertTextBinding(config, "HunTeamName", "CurrentGame.HunTeam.Name");
        AssertTextBinding(config, "HunTeamMajorPoint", "CurrentGame.MatchScore.CurrentHunTeamMajorText");
        AssertTextBinding(config, "GameScoresHun", "CurrentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText");
    }

    [Fact]
    public void ReadsGlobalScoreRowControlConfig()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 195,
              "MainGlobalScoreRow": {
                "ControlType": "GlobalScoreRow",
                "Left": 175,
                "Top": 93,
                "TeamType": "HomeTeam",
                "MajorGameGap": 180,
                "HalfGameGap": 90,
                "FontFamily": "pack://application:,,,/Assets/Fonts/#华康POP1体W5",
                "FontWeight": "Bold",
                "Color": "#FFFFFFFF",
                "FontSize": 24,
                "ShowCampIcon": true,
                "ZIndex": 2
              }
            }
            """);

        Assert.NotNull(config);
        var row = Assert.IsType<GlobalScoreRowControlConfig>(config.Controls["MainGlobalScoreRow"]);
        Assert.Equal("GlobalScoreRow", row.ControlType);
        Assert.Equal(175, row.Left);
        Assert.Equal(93, row.Top);
        Assert.Equal(neo_bpsys_wpf.Core.Enums.TeamType.HomeTeam, row.TeamType);
        Assert.Equal(180, row.MajorGameGap);
        Assert.Equal(90, row.HalfGameGap);
        Assert.Equal("Bold", row.FontWeight);
        Assert.Equal(24, row.FontSize);
        Assert.True(row.ShowCampIcon);
    }

    [Fact]
    public void ReadsCutSceneBusinessControlConfigs()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "SurTalent0": {
                "ControlType": "TalentTraitDisplay",
                "Left": 164,
                "Top": 424,
                "Width": 178,
                "Height": 36,
                "DisplayKind": "SurvivorTalent",
                "PlayerIndex": 0,
                "IconSize": 38,
                "IconGap": 2,
                "HorizontalAlignment": "Right",
                "VerticalAlignment": "Center",
                "ZIndex": 2
              },
              "GameProgress": {
                "ControlType": "GameProgressText",
                "Left": 488,
                "Top": 82,
                "Width": 463,
                "Height": 30,
                "FontFamily": "pack://application:,,,/Assets/Fonts/#华康POP1体W5",
                "FontWeight": "Bold",
                "Color": "#FFFFFFFF",
                "FontSize": 22,
                "TextAlignment": "Center",
                "HorizontalAlignment": "Center",
                "VerticalAlignment": "Center",
                "UseLineBreak": true,
                "ZIndex": 1
              },
              "MapName": {
                "ControlType": "MapNameText",
                "Left": 488,
                "Top": 51,
                "Width": 463,
                "FontFamily": "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简",
                "FontWeight": "Normal",
                "Color": "#FFFFFFFF",
                "FontSize": 24,
                "TextAlignment": "Center",
                "HorizontalAlignment": "Center",
                "VerticalAlignment": "Center",
                "EmptyText": "",
                "ZIndex": 1
              }
            }
            """);

        Assert.NotNull(config);

        var talent = Assert.IsType<TalentTraitDisplayControlConfig>(config.Controls["SurTalent0"]);
        Assert.Equal("TalentTraitDisplay", talent.ControlType);
        Assert.Equal(TalentTraitDisplayKind.SurvivorTalent, talent.DisplayKind);
        Assert.Equal(0, talent.PlayerIndex);
        Assert.True(talent.HasValidSurvivorPlayerIndex());
        Assert.Equal(38, talent.IconSize);
        Assert.Equal(2, talent.IconGap);

        var progress = Assert.IsType<GameProgressTextControlConfig>(config.Controls["GameProgress"]);
        Assert.Equal("GameProgressText", progress.ControlType);
        Assert.True(progress.UseLineBreak);
        Assert.Equal("Center", progress.TextAlignment);

        var mapName = Assert.IsType<MapNameTextControlConfig>(config.Controls["MapName"]);
        Assert.Equal("MapNameText", mapName.ControlType);
        Assert.Equal(24, mapName.FontSize);
        Assert.Equal(string.Empty, mapName.EmptyText);
    }

    [Fact]
    public void ReadsBuiltInScoreGlobalWindowLayout()
    {
        var config = ReadBuiltInLayout("ScoreGlobalWindow");

        Assert.NotNull(config);
        Assert.Equal(3, config.Version);
        Assert.Equal(1440, config.CanvasWidth);
        Assert.Equal(195, config.CanvasHeight);
        Assert.Equal("Resources/scoreGlobal.png", config.BackgroundImage);

        Assert.Contains("MainTeamName", config.Controls.Keys);
        Assert.Contains("AwayTeamName", config.Controls.Keys);
        Assert.Contains("MainScoreTotal", config.Controls.Keys);
        Assert.Contains("AwayScoreTotal", config.Controls.Keys);
        Assert.Contains("MainGlobalScoreRow", config.Controls.Keys);
        Assert.Contains("AwayGlobalScoreRow", config.Controls.Keys);

        AssertTextBinding(config, "MainTeamName", "HomeTeam.Name");
        AssertTextBinding(config, "AwayTeamName", "AwayTeam.Name");
        AssertTextBinding(config, "MainScoreTotal", "CurrentGame.MatchScore.HomeTotalMinorScore");
        AssertTextBinding(config, "AwayScoreTotal", "CurrentGame.MatchScore.AwayTotalMinorScore");

        var mainRow = Assert.IsType<GlobalScoreRowControlConfig>(config.Controls["MainGlobalScoreRow"]);
        var awayRow = Assert.IsType<GlobalScoreRowControlConfig>(config.Controls["AwayGlobalScoreRow"]);
        Assert.Equal(neo_bpsys_wpf.Core.Enums.TeamType.HomeTeam, mainRow.TeamType);
        Assert.Equal(neo_bpsys_wpf.Core.Enums.TeamType.AwayTeam, awayRow.TeamType);
    }

    [Fact]
    public void ReadsLocalizedTextControlConfig()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "Header_Character": {
                "ControlType": "LocalizedText",
                "Left": 47,
                "Top": 307,
                "Width": 80,
                "LocalizationKey": "Character",
                "FallbackText": "Character",
                "HorizontalAlignment": "Center",
                "VerticalAlignment": "Center",
                "TextAlignment": "Center",
                "TextWrapping": "Wrap",
                "FontFamily": "pack://application:,,,/Assets/Fonts/#Noto Sans",
                "FontWeight": "Bold",
                "Color": "#FFFFFFFF",
                "FontSize": 16,
                "ZIndex": 2
              }
            }
            """);

        Assert.NotNull(config);
        var text = Assert.IsType<LocalizedTextControlConfig>(config.Controls["Header_Character"]);
        Assert.Equal("LocalizedText", text.ControlType);
        Assert.Equal("Character", text.LocalizationKey);
        Assert.Equal("Character", text.FallbackText);
        Assert.Equal("Wrap", text.TextWrapping);
        Assert.Equal(16, text.FontSize);
    }

    [Fact]
    public void ReadsBuiltInCutSceneWindowLayout()
    {
        var config = ReadBuiltInLayout("CutSceneWindow");

        Assert.NotNull(config);
        Assert.Equal(3, config.Version);
        Assert.Equal(1440, config.CanvasWidth);
        Assert.Equal(810, config.CanvasHeight);
        Assert.Equal("Resources/cutScene.png", config.BackgroundImage);

        var expectedControls = new[]
        {
            "SurTeamLogo",
            "HunTeamLogo",
            "SurTeamName",
            "HunTeamName",
            "SurTeamMajorPoint",
            "HunTeamMajorPoint",
            "Map",
            "MapName",
            "GameProgress",
            "SurPick0",
            "SurPick1",
            "SurPick2",
            "SurPick3",
            "HunPick",
            "SurId0",
            "SurId1",
            "SurId2",
            "SurId3",
            "HunId",
            "SurTalent0",
            "SurTalent1",
            "SurTalent2",
            "SurTalent3",
            "HunTalent",
            "Trait"
        };

        foreach (var controlName in expectedControls)
        {
            Assert.Contains(controlName, config.Controls.Keys);
        }

        AssertImageBinding(config, "SurTeamLogo", "CurrentGame.SurTeam.Logo");
        AssertImageBinding(config, "HunTeamLogo", "CurrentGame.HunTeam.Logo");
        AssertImageBinding(config, "Map", "CurrentGame.PickedMapImage");
        AssertImageBinding(config, "SurPick0", "CurrentGame.SurPlayerList[0].Character.BigImage");
        AssertImageBinding(config, "SurPick1", "CurrentGame.SurPlayerList[1].Character.BigImage");
        AssertImageBinding(config, "SurPick2", "CurrentGame.SurPlayerList[2].Character.BigImage");
        AssertImageBinding(config, "SurPick3", "CurrentGame.SurPlayerList[3].Character.BigImage");
        AssertImageBinding(config, "HunPick", "CurrentGame.HunPlayer.Character.BigImage");

        AssertTextBinding(config, "SurTeamName", "CurrentGame.SurTeam.Name");
        AssertTextBinding(config, "HunTeamName", "CurrentGame.HunTeam.Name");
        AssertTextBinding(config, "SurTeamMajorPoint", "CurrentGame.MatchScore.CurrentSurTeamMajorText");
        AssertTextBinding(config, "HunTeamMajorPoint", "CurrentGame.MatchScore.CurrentHunTeamMajorText");
        AssertTextBinding(config, "SurId0", "CurrentGame.SurPlayerList[0].Member.Name");
        AssertTextBinding(config, "SurId1", "CurrentGame.SurPlayerList[1].Member.Name");
        AssertTextBinding(config, "SurId2", "CurrentGame.SurPlayerList[2].Member.Name");
        AssertTextBinding(config, "SurId3", "CurrentGame.SurPlayerList[3].Member.Name");
        AssertTextBinding(config, "HunId", "CurrentGame.HunPlayer.Member.Name");

        var mapName = Assert.IsType<MapNameTextControlConfig>(config.Controls["MapName"]);
        Assert.Equal("MapNameText", mapName.ControlType);
        var progress = Assert.IsType<GameProgressTextControlConfig>(config.Controls["GameProgress"]);
        Assert.Equal("GameProgressText", progress.ControlType);
        Assert.False(progress.UseLineBreak);

        for (var index = 0; index < 4; index++)
        {
            var talent = Assert.IsType<TalentTraitDisplayControlConfig>(config.Controls[$"SurTalent{index}"]);
            Assert.Equal("TalentTraitDisplay", talent.ControlType);
            Assert.Equal(TalentTraitDisplayKind.SurvivorTalent, talent.DisplayKind);
            Assert.Equal(index, talent.PlayerIndex);
        }

        var hunTalent = Assert.IsType<TalentTraitDisplayControlConfig>(config.Controls["HunTalent"]);
        Assert.Equal("TalentTraitDisplay", hunTalent.ControlType);
        Assert.Equal(TalentTraitDisplayKind.HunterTalent, hunTalent.DisplayKind);

        var trait = Assert.IsType<TalentTraitDisplayControlConfig>(config.Controls["Trait"]);
        Assert.Equal("TalentTraitDisplay", trait.ControlType);
        Assert.Equal(TalentTraitDisplayKind.HunterTrait, trait.DisplayKind);
        Assert.True(trait.RespectTraitVisibility);
        Assert.Equal(56, trait.IconSize);
    }

    [Fact]
    public void ReadsBuiltInGameDataWindowLayout()
    {
        var config = ReadBuiltInLayout("GameDataWindow");

        Assert.NotNull(config);
        Assert.Equal(3, config.Version);
        Assert.Equal(1440, config.CanvasWidth);
        Assert.Equal(810, config.CanvasHeight);
        Assert.Equal("Resources/gameData_withText.png", config.BackgroundImage);

        var expectedControls = new[]
        {
            "SurTeamLogo",
            "HunTeamLogo",
            "SurTeamName",
            "HunTeamName",
            "SurTeamMajorPoint",
            "HunTeamMajorPoint",
            "GameScoresSur",
            "GameScoresHun",
            "Map",
            "MapName",
            "GameProgress",
            "Header_Character",
            "Header_ID",
            "Header_DecodingProgress",
            "Header_PalletStrikes",
            "Header_Rescues",
            "Header_Heals",
            "Header_ContainmentTime",
            "HunImage",
            "HunId",
            "Header_RemainingCiphers",
            "Header_PalletsDestroyed",
            "Header_SurvivorHits",
            "Header_TerrorShocks",
            "Header_Knockdowns",
            "HunMachineLeft",
            "HunPalletBroken",
            "HunHitTimes",
            "HunTerrorShockTimes",
            "HunDownTimes"
        };

        foreach (var controlName in expectedControls)
        {
            Assert.Contains(controlName, config.Controls.Keys);
        }

        for (var index = 0; index < 4; index++)
        {
            AssertImageBinding(
                config,
                $"Player{index}Header",
                $"CurrentGame.SurPlayerList[{index}].Character.HeaderImage");
            AssertTextBinding(config, $"SurId{index}", $"CurrentGame.SurPlayerList[{index}].Member.Name");

            var machineDecoded = AssertTextBinding(
                config,
                $"Sur{index}MachineDecoded",
                $"CurrentGame.SurPlayerList[{index}].Data.DecodingProgress");
            Assert.Equal("{0}%", machineDecoded.StringFormat);

            AssertTextBinding(
                config,
                $"Sur{index}PalletStunTimes",
                $"CurrentGame.SurPlayerList[{index}].Data.PalletStrikes");
            AssertTextBinding(
                config,
                $"Sur{index}RescueTimes",
                $"CurrentGame.SurPlayerList[{index}].Data.Rescues");
            AssertTextBinding(
                config,
                $"Sur{index}HealedTimes",
                $"CurrentGame.SurPlayerList[{index}].Data.Heals");
            AssertTextBinding(
                config,
                $"Sur{index}KiteTime",
                $"CurrentGame.SurPlayerList[{index}].Data.ContainmentTime");
        }

        AssertImageBinding(config, "SurTeamLogo", "CurrentGame.SurTeam.Logo");
        AssertImageBinding(config, "HunTeamLogo", "CurrentGame.HunTeam.Logo");
        AssertImageBinding(config, "Map", "CurrentGame.PickedMapImage");
        AssertImageBinding(config, "HunImage", "CurrentGame.HunPlayer.Character.HalfImage");

        AssertTextBinding(config, "SurTeamName", "CurrentGame.SurTeam.Name");
        AssertTextBinding(config, "HunTeamName", "CurrentGame.HunTeam.Name");
        AssertTextBinding(config, "SurTeamMajorPoint", "CurrentGame.MatchScore.CurrentSurTeamMajorText");
        AssertTextBinding(config, "HunTeamMajorPoint", "CurrentGame.MatchScore.CurrentHunTeamMajorText");
        AssertTextBinding(config, "GameScoresSur", "CurrentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText");
        AssertTextBinding(config, "GameScoresHun", "CurrentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText");
        AssertTextBinding(config, "HunId", "CurrentGame.HunPlayer.Member.Name");
        AssertTextBinding(config, "HunMachineLeft", "CurrentGame.HunPlayer.Data.RemainingCipher");
        AssertTextBinding(config, "HunPalletBroken", "CurrentGame.HunPlayer.Data.PalletsDestroyed");
        AssertTextBinding(config, "HunHitTimes", "CurrentGame.HunPlayer.Data.SurvivorHits");
        AssertTextBinding(config, "HunTerrorShockTimes", "CurrentGame.HunPlayer.Data.TerrorShocks");
        AssertTextBinding(config, "HunDownTimes", "CurrentGame.HunPlayer.Data.Knockdowns");

        var localizedHeaders = new[]
        {
            "Header_Character",
            "Header_ID",
            "Header_DecodingProgress",
            "Header_PalletStrikes",
            "Header_Rescues",
            "Header_Heals",
            "Header_ContainmentTime",
            "Header_RemainingCiphers",
            "Header_PalletsDestroyed",
            "Header_SurvivorHits",
            "Header_TerrorShocks",
            "Header_Knockdowns"
        };

        foreach (var headerName in localizedHeaders)
        {
            Assert.IsType<LocalizedTextControlConfig>(config.Controls[headerName]);
        }

        var mapName = Assert.IsType<MapNameTextControlConfig>(config.Controls["MapName"]);
        Assert.Equal("MapNameText", mapName.ControlType);
        var progress = Assert.IsType<GameProgressTextControlConfig>(config.Controls["GameProgress"]);
        Assert.Equal("GameProgressText", progress.ControlType);
        Assert.False(progress.UseLineBreak);
    }

    [Fact]
    public void GameDataWindowLayoutDoesNotReferenceLegacyTeamScoreBinding()
    {
        var layoutText = File.ReadAllText(GetBuiltInLayoutPath("GameDataWindow"));

        Assert.DoesNotContain("CurrentGame.SurTeam.Score.GameScores", layoutText);
        Assert.DoesNotContain("CurrentGame.HunTeam.Score.GameScores", layoutText);
        Assert.DoesNotContain("CurrentGame.SurTeam.Score.MajorPointsOnFront", layoutText);
        Assert.DoesNotContain("CurrentGame.HunTeam.Score.MajorPointsOnFront", layoutText);
        Assert.DoesNotContain("Team.Score", layoutText);
    }

    [Fact]
    public void CutSceneWindowLayoutDoesNotReferenceLegacyTeamScoreBinding()
    {
        var layoutText = File.ReadAllText(GetBuiltInLayoutPath("CutSceneWindow"));

        Assert.DoesNotContain("CurrentGame.SurTeam.Score.MajorPointsOnFront", layoutText);
        Assert.DoesNotContain("CurrentGame.HunTeam.Score.MajorPointsOnFront", layoutText);
        Assert.DoesNotContain("Team.Score", layoutText);
    }

    [Fact]
    public void UnknownControlTypeReportsControlNameAndType()
    {
        var exception = Assert.Throws<FrontedLayoutConfigException>(() =>
            JsonSerializer.Deserialize<FrontedCanvasConfig>(
                """
                {
                  "Version": 3,
                  "CanvasWidth": 1440,
                  "CanvasHeight": 810,
                  "UnknownControl": {
                    "ControlType": "Video",
                    "Left": 0,
                    "Top": 0,
                    "ZIndex": 0
                  }
                }
                """));

        Assert.Contains("UnknownControl", exception.Message);
        Assert.Contains("Video", exception.Message);
    }

    [Fact]
    public void NumericFieldsRejectJsonStrings()
    {
        Assert.Throws<FrontedLayoutConfigException>(() =>
            JsonSerializer.Deserialize<FrontedCanvasConfig>(
                """
                {
                  "Version": 3,
                  "CanvasWidth": "1440",
                  "CanvasHeight": 810
                }
                """));

        Assert.Throws<FrontedLayoutConfigException>(() =>
            JsonSerializer.Deserialize<FrontedCanvasConfig>(
                """
                {
                  "Version": 3,
                  "CanvasWidth": 1440,
                  "CanvasHeight": 810,
                  "SurTeamName": {
                    "ControlType": "Text",
                    "Left": "580",
                    "Top": 720,
                    "ZIndex": 2
                  }
                }
                """));
    }

    [Fact]
    public void SerializesRootLevelControlShape()
    {
        var config = new FrontedCanvasConfig
        {
            CanvasWidth = 1440,
            CanvasHeight = 810,
            BackgroundImage = "Resources/bp.png",
            Controls =
            {
                ["Title"] = new TextFrontedControlConfig
                {
                    Left = 10,
                    Top = 20,
                    ZIndex = 1,
                    Text = "Static title",
                    FontSize = 18
                }
            }
        };

        var json = JsonSerializer.Serialize(config);
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("Title", out var title));
        Assert.Equal("Text", title.GetProperty("ControlType").GetString());
        Assert.Equal("Static title", title.GetProperty("Text").GetString());
        var roundTrip = JsonSerializer.Deserialize<FrontedCanvasConfig>(json);
        Assert.NotNull(roundTrip);
        var text = Assert.IsType<TextFrontedControlConfig>(roundTrip.Controls["Title"]);
        Assert.Equal("Static title", text.Text);
        Assert.False(document.RootElement.TryGetProperty("Controls", out _));
    }

    [Fact]
    public void TextFrontedControlUsesStaticTextWhenBindingPathIsEmpty()
    {
        RunOnStaThread(() =>
        {
            var control = new TextFrontedControl();
            var element = control.Create(
                "Title",
                new TextFrontedControlConfig
                {
                    Text = "Static title"
                },
                CreateBuildContext());

            var border = Assert.IsType<Border>(element);
            var textBlock = Assert.IsType<TextBlock>(border.Child);
            Assert.Equal("Static title", textBlock.Text);
            Assert.Null(BindingOperations.GetBinding(textBlock, TextBlock.TextProperty));
        });
    }

    [Fact]
    public void TextFrontedControlBindingPathTakesPriorityOverStaticText()
    {
        RunOnStaThread(() =>
        {
            var sharedDataService = new Mock<ISharedDataService>().Object;
            var control = new TextFrontedControl();
            var element = control.Create(
                "Title",
                new TextFrontedControlConfig
                {
                    BindingPath = "CurrentGame.SurTeam.Name",
                    Text = "Static title"
                },
                CreateBuildContext(sharedDataService));

            var border = Assert.IsType<Border>(element);
            var textBlock = Assert.IsType<TextBlock>(border.Child);
            var binding = BindingOperations.GetBinding(textBlock, TextBlock.TextProperty);
            Assert.NotNull(binding);
            Assert.Equal("CurrentGame.SurTeam.Name", binding.Path.Path);
            Assert.Same(sharedDataService, binding.Source);
            Assert.NotEqual("Static title", textBlock.Text);
        });
    }

    [Fact]
    public void TextFrontedControlAppliesStringFormatOnlyForBindingPath()
    {
        RunOnStaThread(() =>
        {
            var control = new TextFrontedControl();
            var element = control.Create(
                "DecodingProgress",
                new TextFrontedControlConfig
                {
                    BindingPath = "CurrentGame.SurPlayerList[0].Data.DecodingProgress",
                    StringFormat = "{0}%"
                },
                CreateBuildContext(new Mock<ISharedDataService>().Object));

            var border = Assert.IsType<Border>(element);
            var textBlock = Assert.IsType<TextBlock>(border.Child);
            var binding = BindingOperations.GetBinding(textBlock, TextBlock.TextProperty);
            Assert.NotNull(binding);
            Assert.Equal("{0}%", binding.StringFormat);

            var staticElement = control.Create(
                "Title",
                new TextFrontedControlConfig
                {
                    Text = "Static title",
                    StringFormat = "{0}%"
                },
                CreateBuildContext());

            var staticBorder = Assert.IsType<Border>(staticElement);
            var staticTextBlock = Assert.IsType<TextBlock>(staticBorder.Child);
            Assert.Equal("Static title", staticTextBlock.Text);
            Assert.Null(BindingOperations.GetBinding(staticTextBlock, TextBlock.TextProperty));
        });
    }

    [Fact]
    public void LocalizedTextFallbackUsesFallbackWhenKeyIsMissing()
    {
        Assert.Equal(
            "Fallback header",
            LocalizedTextFrontedControl.ResolveText("Missing_GameData_Header_Key_For_Test", "Fallback header"));
    }

    [Fact]
    public void ResourceResolverMapsResourcesPrefixToBpui()
    {
        var resolver = new FrontedResourceResolver(NullLogger<FrontedResourceResolver>.Instance);

        var path = resolver.ResolveImagePath("Resources/bp.png");

        Assert.NotNull(path);
        Assert.EndsWith(Path.Combine("Resources", "bpui", "bp.png"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SettingsHostServiceConstructorDoesNotFireAndForgetLoadConfig()
    {
        var repoRoot = FindRepositoryRoot();
        var sourcePath = Path.Combine(repoRoot, "neo-bpsys-wpf", "Services", "SettingsHostService.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("_ = LoadConfig();", source);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private static FrontedControlBuildContext CreateBuildContext(ISharedDataService sharedDataService = null)
    {
        return new FrontedControlBuildContext
        {
            Services = EmptyServiceProvider.Instance,
            SharedDataService = sharedDataService ?? new Mock<ISharedDataService>().Object,
            ResourceResolver = NullFrontedResourceResolver.Instance,
            WindowId = "TestWindow",
            CanvasName = "BaseCanvas",
            Logger = NullLogger.Instance
        };
    }

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        exception?.Throw();
    }

    private static FrontedCanvasConfig ReadBuiltInLayout(string windowTypeName)
    {
        var path = GetBuiltInLayoutPath(windowTypeName);

        Assert.True(File.Exists(path), path);

        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(File.ReadAllText(path));
        Assert.NotNull(config);
        return config;
    }

    private static string GetBuiltInLayoutPath(string windowTypeName) =>
        Path.Combine(
            AppConstants.ResourcesPath,
            "FrontedLayouts",
            windowTypeName,
            "BaseCanvas.json");

    private static TextFrontedControlConfig AssertTextBinding(
        FrontedCanvasConfig config,
        string controlName,
        string bindingPath)
    {
        var control = Assert.IsType<TextFrontedControlConfig>(config.Controls[controlName]);
        Assert.Equal(bindingPath, control.BindingPath);
        return control;
    }

    private static ImageFrontedControlConfig AssertImageBinding(
        FrontedCanvasConfig config,
        string controlName,
        string bindingPath)
    {
        var control = Assert.IsType<ImageFrontedControlConfig>(config.Controls[controlName]);
        Assert.Equal(bindingPath, control.BindingPath);
        return control;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object GetService(Type serviceType) => null;
    }

    private sealed class NullFrontedResourceResolver : IFrontedResourceResolver
    {
        public static readonly NullFrontedResourceResolver Instance = new();

        public string ResolveImagePath(string path) => null;

        public ImageSource ResolveImage(string path) => null;
    }
}
