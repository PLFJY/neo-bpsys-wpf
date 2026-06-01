using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Controls.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Xunit;
using WpfUiImage = Wpf.Ui.Controls.Image;

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
                "SizingMode": "OverflowCrop",
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
        Assert.Equal(ImageSizingMode.OverflowCrop, image.SizingMode);
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

    [Theory]
    [InlineData("Auto", ImageSizingMode.Auto)]
    [InlineData("FillContainer", ImageSizingMode.FillContainer)]
    [InlineData("OverflowCrop", ImageSizingMode.OverflowCrop)]
    public void ReadsImageSizingModeStringValues(string jsonValue, ImageSizingMode expected)
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            $$"""
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "Logo": {
                "ControlType": "Image",
                "Left": 10,
                "Top": 20,
                "SizingMode": "{{jsonValue}}"
              }
            }
            """);

        Assert.NotNull(config);
        var image = Assert.IsType<ImageFrontedControlConfig>(config.Controls["Logo"]);
        Assert.Equal(expected, image.SizingMode);
    }

    [Fact]
    public void ReadsBorderedImageControl()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "Pick": {
                "ControlType": "BorderedImage",
                "Left": 10,
                "Top": 20,
                "Width": 120,
                "Height": 160,
                "BindingPath": "CurrentGame.SurPlayerList[0].PictureShown",
                "ImageWidth": 96,
                "ImageHeight": 128,
                "SizingMode": "OverflowCrop",
                "Stretch": "UniformToFill"
              }
            }
            """);

        Assert.NotNull(config);
        var image = Assert.IsType<BorderedImageFrontedControlConfig>(config.Controls["Pick"]);
        Assert.Equal("BorderedImage", image.ControlType);
        Assert.Equal("CurrentGame.SurPlayerList[0].PictureShown", image.BindingPath);
        Assert.Equal(96, image.ImageWidth);
        Assert.Equal(128, image.ImageHeight);
        Assert.Equal(ImageSizingMode.OverflowCrop, image.SizingMode);
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
        Assert.Equal(ImageSizingMode.FillContainer, logo.SizingMode);
        Assert.Equal("Fill", logo.Stretch);
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
        Assert.Equal(ImageSizingMode.FillContainer, logo.SizingMode);
        Assert.Equal("Fill", logo.Stretch);
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
    public void ReadsWidgetsWindowBusinessControlConfigs()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1132,
              "CanvasHeight": 182,
              "Ban": {
                "ControlType": "CurrentBanDisplay",
                "Left": 193,
                "Top": 5,
                "Width": 68,
                "Height": 35,
                "Camp": "Sur",
                "Index": 2,
                "ShowLockOverlay": true,
                "Stretch": "Uniform"
              },
              "Map": {
                "ControlType": "MapV2Display",
                "Left": 50.5,
                "Top": 0,
                "Width": 149,
                "Height": 160,
                "MapKey": "ArmsFactory"
              },
              "PickedMapName": {
                "ControlType": "MapNameText",
                "Left": 38,
                "Top": 149,
                "Width": 232,
                "BindingPath": "CurrentGame.PickedMap"
              },
              "BannedMapName": {
                "ControlType": "MapNameText",
                "Left": 38,
                "Top": 475,
                "Width": 232,
                "BindingPath": "CurrentGame.BannedMap"
              }
            }
            """);

        Assert.NotNull(config);

        var ban = Assert.IsType<CurrentBanDisplayControlConfig>(config.Controls["Ban"]);
        Assert.Equal("CurrentBanDisplay", ban.ControlType);
        Assert.Equal(neo_bpsys_wpf.Core.Enums.Camp.Sur, ban.Camp);
        Assert.Equal(2, ban.Index);
        Assert.True(ban.ShowLockOverlay);
        Assert.Equal("Uniform", ban.Stretch);

        var map = Assert.IsType<MapV2DisplayControlConfig>(config.Controls["Map"]);
        Assert.Equal("MapV2Display", map.ControlType);
        Assert.Equal("ArmsFactory", map.MapKey);

        var pickedMapName = Assert.IsType<MapNameTextControlConfig>(config.Controls["PickedMapName"]);
        Assert.Equal("CurrentGame.PickedMap", pickedMapName.BindingPath);
        var bannedMapName = Assert.IsType<MapNameTextControlConfig>(config.Controls["BannedMapName"]);
        Assert.Equal("CurrentGame.BannedMap", bannedMapName.BindingPath);
    }

    [Fact]
    public void ReadsBpWindowBusinessControlConfigs()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "CurrentSurBan": {
                "ControlType": "BanSlotDisplay",
                "SlotKind": "Current",
                "Camp": "Sur",
                "Index": 2,
                "ShowLockOverlay": true,
                "SizingMode": "FillContainer",
                "Stretch": "Uniform",
                "LockZIndexOffset": 1
              },
              "CurrentHunBan": {
                "ControlType": "BanSlotDisplay",
                "SlotKind": "Current",
                "Camp": "Hun",
                "Index": 1
              },
              "GlobalSurBan": {
                "ControlType": "BanSlotDisplay",
                "SlotKind": "Global",
                "Camp": "Sur",
                "Index": 11
              },
              "GlobalHunBan": {
                "ControlType": "BanSlotDisplay",
                "SlotKind": "Global",
                "Camp": "Hun",
                "Index": 2
              },
              "SurPickingBorder0": {
                "ControlType": "PickingBorderOverlay",
                "TargetControlName": "SurPick0",
                "Left": 0,
                "Top": 620,
                "Width": 141,
                "Height": 160,
                "ZIndex": 2,
                "InitiallyHidden": true
              }
            }
            """);

        Assert.NotNull(config);

        var currentSurBan = Assert.IsType<BanSlotDisplayControlConfig>(config.Controls["CurrentSurBan"]);
        Assert.Equal(BanSlotKind.Current, currentSurBan.SlotKind);
        Assert.Equal(neo_bpsys_wpf.Core.Enums.Camp.Sur, currentSurBan.Camp);
        Assert.Equal(2, currentSurBan.Index);
        Assert.True(currentSurBan.ShowLockOverlay);
        Assert.Equal(ImageSizingMode.FillContainer, currentSurBan.SizingMode);
        Assert.Equal("Uniform", currentSurBan.Stretch);

        var currentHunBan = Assert.IsType<BanSlotDisplayControlConfig>(config.Controls["CurrentHunBan"]);
        Assert.Equal(BanSlotKind.Current, currentHunBan.SlotKind);
        Assert.Equal(neo_bpsys_wpf.Core.Enums.Camp.Hun, currentHunBan.Camp);

        var globalSurBan = Assert.IsType<BanSlotDisplayControlConfig>(config.Controls["GlobalSurBan"]);
        Assert.Equal(BanSlotKind.Global, globalSurBan.SlotKind);
        Assert.Equal(neo_bpsys_wpf.Core.Enums.Camp.Sur, globalSurBan.Camp);

        var globalHunBan = Assert.IsType<BanSlotDisplayControlConfig>(config.Controls["GlobalHunBan"]);
        Assert.Equal(BanSlotKind.Global, globalHunBan.SlotKind);
        Assert.Equal(neo_bpsys_wpf.Core.Enums.Camp.Hun, globalHunBan.Camp);

        var pickingBorder = Assert.IsType<PickingBorderOverlayControlConfig>(config.Controls["SurPickingBorder0"]);
        Assert.Equal("PickingBorderOverlay", pickingBorder.ControlType);
        Assert.Equal("SurPick0", pickingBorder.TargetControlName);
        Assert.True(pickingBorder.InitiallyHidden);
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

        Assert.Contains("HomeTeamName", config.Controls.Keys);
        Assert.Contains("AwayTeamName", config.Controls.Keys);
        Assert.Contains("HomeScoreTotal", config.Controls.Keys);
        Assert.Contains("AwayScoreTotal", config.Controls.Keys);
        Assert.Contains("HomeGlobalScoreRow", config.Controls.Keys);
        Assert.Contains("AwayGlobalScoreRow", config.Controls.Keys);

        AssertTextBinding(config, "HomeTeamName", "HomeTeam.Name");
        AssertTextBinding(config, "AwayTeamName", "AwayTeam.Name");
        AssertTextBinding(config, "HomeScoreTotal", "CurrentGame.MatchScore.HomeTotalMinorScore");
        AssertTextBinding(config, "AwayScoreTotal", "CurrentGame.MatchScore.AwayTotalMinorScore");

        var mainRow = Assert.IsType<GlobalScoreRowControlConfig>(config.Controls["HomeGlobalScoreRow"]);
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

        Assert.Equal("Image", AssertImageBinding(config, "SurTeamLogo", "CurrentGame.SurTeam.Logo").ControlType);
        Assert.Equal("Image", AssertImageBinding(config, "HunTeamLogo", "CurrentGame.HunTeam.Logo").ControlType);
        var map = Assert.IsType<BorderedImageFrontedControlConfig>(config.Controls["Map"]);
        Assert.Equal("BorderedImage", map.ControlType);
        Assert.Equal("CurrentGame.PickedMapImage", map.BindingPath);
        Assert.Equal(488, map.Left);
        Assert.Equal(0, map.Top);
        Assert.Equal(463, map.Width);
        Assert.Equal(112, map.Height);
        Assert.Equal(-1, map.ZIndex);
        Assert.Equal("Center", map.HorizontalAlignment);
        Assert.Equal("Center", map.VerticalAlignment);
        Assert.Equal(ImageSizingMode.Auto, map.SizingMode);

        var expectedSurPickGeometry = new[]
        {
            (Left: 1D, Top: 115D, Width: 346D, Height: 308.5D),
            (Left: 351D, Top: 115D, Width: 346D, Height: 308.5D),
            (Left: 1D, Top: 465D, Width: 346D, Height: 306.5D),
            (Left: 350D, Top: 465D, Width: 346D, Height: 306.5D)
        };

        for (var index = 0; index < 4; index++)
        {
            var pick = Assert.IsType<BorderedImageFrontedControlConfig>(config.Controls[$"SurPick{index}"]);
            Assert.Equal($"CurrentGame.SurPlayerList[{index}].Character.BigImage", pick.BindingPath);
            Assert.Equal(expectedSurPickGeometry[index].Left, pick.Left);
            Assert.Equal(expectedSurPickGeometry[index].Top, pick.Top);
            Assert.Equal(expectedSurPickGeometry[index].Width, pick.Width);
            Assert.Equal(expectedSurPickGeometry[index].Height, pick.Height);
            Assert.Equal(ImageSizingMode.OverflowCrop, pick.SizingMode);
            Assert.Equal("UniformToFill", pick.Stretch);
            Assert.True(pick.ClipToBounds);
            Assert.Equal("Center", pick.HorizontalAlignment);
            Assert.Equal("Top", pick.VerticalAlignment);
            Assert.Equal(556.5, pick.ImageWidth);
            Assert.Null(pick.ImageHeight);
        }

        var hunPick = Assert.IsType<BorderedImageFrontedControlConfig>(config.Controls["HunPick"]);
        Assert.Equal("CurrentGame.HunPlayer.Character.BigImage", hunPick.BindingPath);
        Assert.Equal(702, hunPick.Left);
        Assert.Equal(114.5, hunPick.Top);
        Assert.Equal(739, hunPick.Width);
        Assert.Equal(635, hunPick.Height);
        Assert.Equal(ImageSizingMode.OverflowCrop, hunPick.SizingMode);
        Assert.Equal("UniformToFill", hunPick.Stretch);
        Assert.True(hunPick.ClipToBounds);
        Assert.Equal("Center", hunPick.HorizontalAlignment);
        Assert.Equal("Top", hunPick.VerticalAlignment);
        Assert.Null(hunPick.ImageWidth);
        Assert.Null(hunPick.ImageHeight);

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
        Assert.Equal("Resources/gameData.png", config.BackgroundImage);

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
            var header = AssertImageBinding(
                config,
                $"Player{index}Header",
                $"CurrentGame.SurPlayerList[{index}].Character.HeaderImage");
            Assert.Equal(ImageSizingMode.Auto, header.SizingMode);
            Assert.Null(header.Stretch);
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

        Assert.Equal(
            ImageSizingMode.FillContainer,
            AssertImageBinding(config, "SurTeamLogo", "CurrentGame.SurTeam.Logo").SizingMode);
        Assert.Equal(
            ImageSizingMode.FillContainer,
            AssertImageBinding(config, "HunTeamLogo", "CurrentGame.HunTeam.Logo").SizingMode);
        var map = AssertImageBinding(config, "Map", "CurrentGame.PickedMapImage");
        Assert.Equal(ImageSizingMode.OverflowCrop, map.SizingMode);
        Assert.Equal("UniformToFill", map.Stretch);
        var hunImage = AssertImageBinding(config, "HunImage", "CurrentGame.HunPlayer.Character.HalfImage");
        Assert.Equal(ImageSizingMode.OverflowCrop, hunImage.SizingMode);
        Assert.Equal("UniformToFill", hunImage.Stretch);

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
    public void ReadsBuiltInWidgetsWindowLayouts()
    {
        var mapBpCanvas = ReadBuiltInLayout("WidgetsWindow", "MapBpCanvas");
        var bpOverViewCanvas = ReadBuiltInLayout("WidgetsWindow", "BpOverViewCanvas");
        var mapV2Canvas = ReadBuiltInLayout("WidgetsWindow", "MapV2Canvas");

        Assert.Equal(308, mapBpCanvas.CanvasWidth);
        Assert.Equal(554, mapBpCanvas.CanvasHeight);
        Assert.Equal("Resources/mapBp.png", mapBpCanvas.BackgroundImage);
        foreach (var controlName in new[]
                 {
                     "PickedMap",
                     "PickedMapName",
                     "PickWord",
                     "SurTeamName",
                     "VS_Word",
                     "HunTeamName",
                     "BannedMap",
                     "BannedMapName",
                     "BanWord"
                 })
        {
            Assert.Contains(controlName, mapBpCanvas.Controls.Keys);
        }

        var pickedMapName = Assert.IsType<MapNameTextControlConfig>(mapBpCanvas.Controls["PickedMapName"]);
        Assert.Equal("CurrentGame.PickedMap", pickedMapName.BindingPath);
        var bannedMapName = Assert.IsType<MapNameTextControlConfig>(mapBpCanvas.Controls["BannedMapName"]);
        Assert.Equal("CurrentGame.BannedMap", bannedMapName.BindingPath);
        foreach (var controlName in new[] { "PickedMap", "BannedMap" })
        {
            var mapImage = Assert.IsType<ImageFrontedControlConfig>(mapBpCanvas.Controls[controlName]);
            Assert.Equal("Image", mapImage.ControlType);
            Assert.Equal(290, mapImage.Width);
            Assert.Equal(138, mapImage.Height);
            Assert.Equal("UniformToFill", mapImage.Stretch);
            Assert.False(mapImage.ClipToBounds);
            Assert.Equal(8, mapImage.CornerRadius);
        }

        Assert.Equal(1132, bpOverViewCanvas.CanvasWidth);
        Assert.Equal(182, bpOverViewCanvas.CanvasHeight);
        Assert.Equal("Resources/bpOverview.png", bpOverViewCanvas.BackgroundImage);
        foreach (var controlName in new[]
                 {
                     "SurTeamLogo",
                     "HunTeamLogo",
                     "SurTeamNameInOverview",
                     "HunTeamNameInOverview",
                     "HunBanCurrent0",
                     "HunBanCurrent1",
                     "SurBanCurrent0",
                     "SurBanCurrent1",
                     "SurBanCurrent2",
                     "SurBanCurrent3",
                     "SurPick0",
                     "SurPick1",
                     "SurPick2",
                     "SurPick3",
                     "GameProgress",
                     "GameScoresSur",
                     "RatioChar",
                     "GameScoresHun",
                     "HunPick"
                 })
        {
            Assert.Contains(controlName, bpOverViewCanvas.Controls.Keys);
        }

        var gameProgress = Assert.IsType<GameProgressTextControlConfig>(bpOverViewCanvas.Controls["GameProgress"]);
        Assert.Equal("GameProgressText", gameProgress.ControlType);
        Assert.True(gameProgress.UseLineBreak);
        Assert.Equal(
            ImageSizingMode.FillContainer,
            Assert.IsType<ImageFrontedControlConfig>(bpOverViewCanvas.Controls["SurTeamLogo"]).SizingMode);
        Assert.Equal(
            ImageSizingMode.FillContainer,
            Assert.IsType<ImageFrontedControlConfig>(bpOverViewCanvas.Controls["HunTeamLogo"]).SizingMode);
        foreach (var controlName in new[] { "SurPick0", "SurPick1", "SurPick2", "SurPick3", "HunPick" })
        {
            var pick = Assert.IsType<BorderedImageFrontedControlConfig>(bpOverViewCanvas.Controls[controlName]);
            Assert.Equal(ImageSizingMode.OverflowCrop, pick.SizingMode);
            Assert.Equal("UniformToFill", pick.Stretch);
            Assert.True(pick.ClipToBounds);
        }

        AssertTextBinding(bpOverViewCanvas, "GameScoresSur", "CurrentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText");
        AssertTextBinding(bpOverViewCanvas, "GameScoresHun", "CurrentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText");

        var bpOverViewCanvasText = File.ReadAllText(GetBuiltInLayoutPath("WidgetsWindow", "BpOverViewCanvas"));
        Assert.DoesNotContain("Team.Score", bpOverViewCanvasText);
        Assert.DoesNotContain("CurrentGame.SurTeam.Score", bpOverViewCanvasText);
        Assert.DoesNotContain("CurrentGame.HunTeam.Score", bpOverViewCanvasText);

        Assert.Equal(1440, mapV2Canvas.CanvasWidth);
        Assert.Equal(160, mapV2Canvas.CanvasHeight);
        Assert.Equal("Resources/mapBpV2.png", mapV2Canvas.BackgroundImage);

        var expectedMapKeys = new Dictionary<string, string>
        {
            ["Arms_Factory"] = "ArmsFactory",
            ["The_Red_Church"] = "TheRedChurch",
            ["Sacred_Heart_Hospital"] = "SacredHeartHospital",
            ["Leo_s_Memory"] = "LeosMemory",
            ["Moonlit_River_Park"] = "MoonlitRiverPark",
            ["Lakeside_Village"] = "LakesideVillage",
            ["Eversleeping_Town"] = "EversleepingTown",
            ["Chinatown"] = "ChinaTown",
            ["Darkwoods"] = "Darkwoods"
        };

        foreach (var (controlName, mapKey) in expectedMapKeys)
        {
            var display = Assert.IsType<MapV2DisplayControlConfig>(mapV2Canvas.Controls[controlName]);
            Assert.Equal("MapV2Display", display.ControlType);
            Assert.Equal(mapKey, display.MapKey);
        }
    }

    [Fact]
    public void ReadsBuiltInBpWindowLayout()
    {
        var config = ReadBuiltInLayout("BpWindow");

        Assert.NotNull(config);
        Assert.Equal(3, config.Version);
        Assert.Equal(1440, config.CanvasWidth);
        Assert.Equal(810, config.CanvasHeight);
        Assert.Equal("Resources/bp.png", config.BackgroundImage);

        foreach (var controlName in new[]
                 {
                     "SurTeamLogo",
                     "HunTeamLogo",
                     "SurTeamName",
                     "HunTeamName",
                     "SurTeamMajorPoint",
                     "HunTeamMajorPoint",
                     "GameScoresSur",
                     "GameScoresHun",
                     "Timer",
                     "HunBanCurrent0",
                     "HunBanCurrent1",
                     "SurBanCurrent0",
                     "SurBanCurrent1",
                     "SurBanCurrent2",
                     "SurBanCurrent3",
                     "SurPick0",
                     "SurPick1",
                     "SurPick2",
                     "SurPick3",
                     "SurPickingBorder0",
                     "SurPickingBorder1",
                     "SurPickingBorder2",
                     "SurPickingBorder3",
                     "Map",
                     "MapName",
                     "GameProgress",
                     "HunGlobalBan0",
                     "HunGlobalBan1",
                     "HunGlobalBan2",
                     "SurGlobalBan0",
                     "SurGlobalBan1",
                     "SurGlobalBan2",
                     "SurGlobalBan3",
                     "SurGlobalBan4",
                     "SurGlobalBan5",
                     "SurGlobalBan6",
                     "SurGlobalBan7",
                     "SurGlobalBan8",
                     "SurGlobalBan9",
                     "SurGlobalBan10",
                     "SurGlobalBan11",
                     "HunPick",
                     "HunPickingBorder",
                     "SurId0",
                     "SurId1",
                     "SurId2",
                     "SurId3",
                     "HunId"
                 })
        {
            Assert.Contains(controlName, config.Controls.Keys);
        }

        AssertTextBinding(config, "SurTeamMajorPoint", "CurrentGame.MatchScore.CurrentSurTeamMajorText");
        AssertTextBinding(config, "HunTeamMajorPoint", "CurrentGame.MatchScore.CurrentHunTeamMajorText");
        AssertTextBinding(config, "GameScoresSur", "CurrentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText");
        AssertTextBinding(config, "GameScoresHun", "CurrentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText");
        AssertTextBinding(config, "Timer", "RemainingSeconds");

        var surLogo = AssertImageBinding(config, "SurTeamLogo", "CurrentGame.SurTeam.Logo");
        Assert.Equal(ImageSizingMode.FillContainer, surLogo.SizingMode);
        Assert.Equal("Fill", surLogo.Stretch);
        Assert.Equal(8, surLogo.CornerRadius);
        var hunLogo = AssertImageBinding(config, "HunTeamLogo", "CurrentGame.HunTeam.Logo");
        Assert.Equal(ImageSizingMode.FillContainer, hunLogo.SizingMode);
        Assert.Equal("Fill", hunLogo.Stretch);
        Assert.Equal(8, hunLogo.CornerRadius);

        for (var index = 0; index < 4; index++)
        {
            var pick = AssertImageBinding(
                config,
                $"SurPick{index}",
                $"CurrentGame.SurPlayerList[{index}].PictureShown");
            Assert.Equal(ImageSizingMode.OverflowCrop, pick.SizingMode);
            Assert.Equal("UniformToFill", pick.Stretch);
            Assert.True(pick.ClipToBounds);

            var border = Assert.IsType<PickingBorderOverlayControlConfig>(config.Controls[$"SurPickingBorder{index}"]);
            Assert.Equal($"SurPick{index}", border.TargetControlName);
            Assert.True(border.InitiallyHidden);
        }

        var hunPick = AssertImageBinding(config, "HunPick", "CurrentGame.HunPlayer.PictureShown");
        Assert.Equal(ImageSizingMode.Auto, hunPick.SizingMode);
        Assert.Equal("Uniform", hunPick.Stretch);
        Assert.Equal("Center", hunPick.HorizontalAlignment);
        Assert.Equal("Center", hunPick.VerticalAlignment);
        var hunBorder = Assert.IsType<PickingBorderOverlayControlConfig>(config.Controls["HunPickingBorder"]);
        Assert.Equal("HunPick", hunBorder.TargetControlName);

        var map = AssertImageBinding(config, "Map", "CurrentGame.PickedMapImageLarge");
        Assert.Equal(ImageSizingMode.OverflowCrop, map.SizingMode);
        Assert.Equal("UniformToFill", map.Stretch);

        Assert.IsType<MapNameTextControlConfig>(config.Controls["MapName"]);
        var gameProgress = Assert.IsType<GameProgressTextControlConfig>(config.Controls["GameProgress"]);
        Assert.False(gameProgress.UseLineBreak);

        foreach (var controlName in new[]
                 {
                     "HunBanCurrent0",
                     "HunBanCurrent1",
                     "SurBanCurrent0",
                     "SurBanCurrent1",
                     "SurBanCurrent2",
                     "SurBanCurrent3",
                     "HunGlobalBan0",
                     "HunGlobalBan1",
                     "HunGlobalBan2",
                     "SurGlobalBan0",
                     "SurGlobalBan1",
                     "SurGlobalBan2",
                     "SurGlobalBan3",
                     "SurGlobalBan4",
                     "SurGlobalBan5",
                     "SurGlobalBan6",
                     "SurGlobalBan7",
                     "SurGlobalBan8",
                     "SurGlobalBan9",
                     "SurGlobalBan10",
                     "SurGlobalBan11"
                 })
        {
            Assert.IsType<BanSlotDisplayControlConfig>(config.Controls[controlName]);
        }
    }

    [Fact]
    public void BuiltInGameProgressTextLayoutsUseExpectedLineBreakMode()
    {
        var cutScene = ReadBuiltInLayout("CutSceneWindow");
        var gameData = ReadBuiltInLayout("GameDataWindow");
        var widgetsOverview = ReadBuiltInLayout("WidgetsWindow", "BpOverViewCanvas");
        var bpWindow = ReadBuiltInLayout("BpWindow");

        Assert.False(Assert.IsType<GameProgressTextControlConfig>(cutScene.Controls["GameProgress"]).UseLineBreak);
        Assert.False(Assert.IsType<GameProgressTextControlConfig>(gameData.Controls["GameProgress"]).UseLineBreak);
        Assert.True(Assert.IsType<GameProgressTextControlConfig>(widgetsOverview.Controls["GameProgress"]).UseLineBreak);
        Assert.False(Assert.IsType<GameProgressTextControlConfig>(bpWindow.Controls["GameProgress"]).UseLineBreak);
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
    public void BpWindowLayoutDoesNotReferenceLegacyTeamScoreBinding()
    {
        var layoutText = File.ReadAllText(GetBuiltInLayoutPath("BpWindow"));

        Assert.DoesNotContain("CurrentGame.SurTeam.Score.GameScores", layoutText);
        Assert.DoesNotContain("CurrentGame.HunTeam.Score.GameScores", layoutText);
        Assert.DoesNotContain("CurrentGame.SurTeam.Score.MajorPointsOnFront", layoutText);
        Assert.DoesNotContain("CurrentGame.HunTeam.Score.MajorPointsOnFront", layoutText);
        Assert.DoesNotContain("Team.Score", layoutText);
    }

    [Fact]
    public void FrontedRendererRegistersGeneratedNamesForWindowFindName()
    {
        RunOnStaThread(() =>
        {
            var sharedDataService = new Mock<ISharedDataService>();
            var renderer = new FrontedRenderer(
                EmptyServiceProvider.Instance,
                sharedDataService.Object,
                NullFrontedResourceResolver.Instance,
                new FrontedControlRegistry([new TextFrontedControl()]),
                NullLogger<FrontedRenderer>.Instance);

            var window = new Window();
            var canvas = new Canvas { Name = "BaseCanvas" };
            window.Content = canvas;

            renderer.RenderToCanvas(
                canvas,
                new FrontedCanvasConfig
                {
                    Version = 3,
                    CanvasWidth = 100,
                    CanvasHeight = 100,
                    Controls =
                    {
                        ["GeneratedText"] = new TextFrontedControlConfig
                        {
                            Text = "Generated",
                            Left = 1,
                            Top = 2
                        }
                    }
                },
                new FrontedRenderContext
                {
                    WindowId = "TestWindow",
                    CanvasName = "BaseCanvas"
                });

            Assert.Same(canvas.Children[0], window.FindName("GeneratedText"));

            renderer.RenderToCanvas(
                canvas,
                new FrontedCanvasConfig
                {
                    Version = 3,
                    CanvasWidth = 100,
                    CanvasHeight = 100
                },
                new FrontedRenderContext
                {
                    WindowId = "TestWindow",
                    CanvasName = "BaseCanvas"
                });

            Assert.Null(window.FindName("GeneratedText"));
        });
    }

    [Fact]
    public void FrontedRendererSyncsPickingBorderOverlayToImageAndBorderedImageGeometry()
    {
        RunOnStaThread(() =>
        {
            var settingsHostService = new Mock<ISettingsHostService>();
            settingsHostService
                .SetupGet(service => service.Settings)
                .Returns(new Settings());
            var serviceProvider = new ServiceCollection()
                .AddSingleton(settingsHostService.Object)
                .BuildServiceProvider();

            var renderer = new FrontedRenderer(
                serviceProvider,
                new Mock<ISharedDataService>().Object,
                NullFrontedResourceResolver.Instance,
                new FrontedControlRegistry(
                [
                    new ImageFrontedControl(),
                    new BorderedImageFrontedControl(),
                    new PickingBorderOverlayFrontedControl()
                ]),
                NullLogger<FrontedRenderer>.Instance);

            var canvas = new Canvas { Name = "BaseCanvas" };
            renderer.RenderToCanvas(
                canvas,
                new FrontedCanvasConfig
                {
                    Version = 3,
                    CanvasWidth = 400,
                    CanvasHeight = 300,
                    Controls =
                    {
                        ["DirectImage"] = new ImageFrontedControlConfig
                        {
                            Left = 10,
                            Top = 20,
                            Width = 120,
                            Height = 80
                        },
                        ["DirectOverlay"] = new PickingBorderOverlayControlConfig
                        {
                            TargetControlName = "DirectImage"
                        },
                        ["FramedImage"] = new BorderedImageFrontedControlConfig
                        {
                            Left = 150,
                            Top = 30,
                            Width = 90,
                            Height = 70
                        },
                        ["FramedOverlay"] = new PickingBorderOverlayControlConfig
                        {
                            TargetControlName = "FramedImage"
                        }
                    }
                },
                new FrontedRenderContext
                {
                    WindowId = "TestWindow",
                    CanvasName = "BaseCanvas"
                });

            var directOverlay = Assert.IsAssignableFrom<Border>(canvas.Children[1]);
            Assert.Equal(10, Canvas.GetLeft(directOverlay));
            Assert.Equal(20, Canvas.GetTop(directOverlay));
            Assert.Equal(120, directOverlay.Width);
            Assert.Equal(80, directOverlay.Height);

            var framedOverlay = Assert.IsAssignableFrom<Border>(canvas.Children[3]);
            Assert.Equal(150, Canvas.GetLeft(framedOverlay));
            Assert.Equal(30, Canvas.GetTop(framedOverlay));
            Assert.Equal(90, framedOverlay.Width);
            Assert.Equal(70, framedOverlay.Height);
        });
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
    public void ParsesPluginControlType()
    {
        var parsed = FrontedPluginControlType.Parse("plugin:top.plfjy.example.fronted/TeamCard");

        Assert.Equal("top.plfjy.example.fronted", parsed.PackageId);
        Assert.Equal("TeamCard", parsed.ControlTypeName);
        Assert.Equal("plugin:top.plfjy.example.fronted/TeamCard", parsed.ToString());
        Assert.False(FrontedPluginControlType.IsPluginControlType("Text"));
    }

    [Theory]
    [InlineData("plugin:TeamCard")]
    [InlineData("plugin:/TeamCard")]
    [InlineData("plugin:top.plfjy.example.fronted/")]
    [InlineData("plugin:top plfjy/TeamCard")]
    [InlineData("plugin:top.plfjy/Team Card")]
    public void RejectsInvalidPluginControlType(string controlType)
    {
        Assert.False(FrontedPluginControlType.TryParse(controlType, out _));
        Assert.Throws<FrontedLayoutConfigException>(() => FrontedPluginControlType.Parse(controlType));
    }

    [Fact]
    public void FrontedPluginDependencySerializesAndDeserializes()
    {
        var dependency = new FrontedPluginDependency
        {
            PackageId = "top.plfjy.example.fronted",
            MinVersion = "1.0.0",
            DisplayName = "Example Fronted Controls",
            MarketplaceId = "top.plfjy.example.fronted",
            Controls = ["plugin:top.plfjy.example.fronted/TeamCard"],
            RequiredBy = ["CutSceneWindow/BaseCanvas"]
        };

        var json = JsonSerializer.Serialize(dependency);
        var roundTrip = JsonSerializer.Deserialize<FrontedPluginDependency>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(dependency.PackageId, roundTrip.PackageId);
        Assert.Equal(dependency.Controls, roundTrip.Controls);
        Assert.Equal(dependency.RequiredBy, roundTrip.RequiredBy);
    }

    [Fact]
    public void RequiredPluginsIsReservedAndRoundTrips()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "RequiredPlugins": [
                {
                  "PackageId": "top.plfjy.example.fronted",
                  "MinVersion": "1.0.0",
                  "DisplayName": "Example Fronted Controls",
                  "Controls": [
                    "plugin:top.plfjy.example.fronted/TeamCard"
                  ]
                }
              ],
              "Title": {
                "ControlType": "Text",
                "Left": 10,
                "Top": 20,
                "Text": "Title"
              }
            }
            """);

        Assert.NotNull(config);
        Assert.Single(config.RequiredPlugins);
        Assert.Equal("top.plfjy.example.fronted", config.RequiredPlugins[0].PackageId);
        Assert.False(config.Controls.ContainsKey("RequiredPlugins"));

        var json = JsonSerializer.Serialize(config);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("RequiredPlugins", out var requiredPlugins));
        Assert.Equal(JsonValueKind.Array, requiredPlugins.ValueKind);

        var roundTrip = JsonSerializer.Deserialize<FrontedCanvasConfig>(json);
        Assert.NotNull(roundTrip);
        Assert.Single(roundTrip.RequiredPlugins);
        Assert.True(roundTrip.Controls.ContainsKey("Title"));
    }

    [Fact]
    public void FrontedCanvasConfigWithoutRequiredPluginsStillWorksAndOmitsEmptyRequiredPlugins()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810
            }
            """);

        Assert.NotNull(config);
        Assert.Empty(config.RequiredPlugins);

        var json = JsonSerializer.Serialize(config);
        Assert.DoesNotContain("RequiredPlugins", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginControlJsonDeserializesAsGenericConfigAndPreservesExtensionData()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "TeamCard1": {
                "ControlType": "plugin:top.plfjy.example.fronted/TeamCard",
                "Left": 100,
                "Top": 100,
                "Width": 260,
                "Height": 96,
                "TeamNameBindingPath": "CurrentGame.HomeTeam.Name",
                "AccentColor": "#FFFFFFFF"
              }
            }
            """);

        Assert.NotNull(config);
        var plugin = Assert.IsType<PluginFrontedControlConfig>(config.Controls["TeamCard1"]);
        Assert.Equal("top.plfjy.example.fronted", plugin.PackageId);
        Assert.Equal("TeamCard", plugin.ControlTypeName);
        Assert.Equal("CurrentGame.HomeTeam.Name", plugin.ExtensionData["TeamNameBindingPath"].GetString());

        var json = JsonSerializer.Serialize(config);
        Assert.Contains("TeamNameBindingPath", json, StringComparison.Ordinal);
        Assert.Contains("AccentColor", json, StringComparison.Ordinal);

        var roundTrip = JsonSerializer.Deserialize<FrontedCanvasConfig>(json);
        Assert.NotNull(roundTrip);
        var roundTripPlugin = Assert.IsType<PluginFrontedControlConfig>(roundTrip.Controls["TeamCard1"]);
        Assert.Equal("#FFFFFFFF", roundTripPlugin.ExtensionData["AccentColor"].GetString());
    }

    [Fact]
    public void UnknownBuiltInLikeControlTypeStillFails()
    {
        Assert.Throws<FrontedLayoutConfigException>(() =>
            JsonSerializer.Deserialize<FrontedCanvasConfig>(
                """
                {
                  "Version": 3,
                  "CanvasWidth": 1440,
                  "CanvasHeight": 810,
                  "TeamCard1": {
                    "ControlType": "TeamCard",
                    "Left": 100,
                    "Top": 100
                  }
                }
                """));
    }

    [Fact]
    public void InvalidPluginControlTypeInLayoutFailsClearly()
    {
        var exception = Assert.Throws<FrontedLayoutConfigException>(() =>
            JsonSerializer.Deserialize<FrontedCanvasConfig>(
                """
                {
                  "Version": 3,
                  "CanvasWidth": 1440,
                  "CanvasHeight": 810,
                  "TeamCard1": {
                    "ControlType": "plugin:TeamCard",
                    "Left": 100,
                    "Top": 100
                  }
                }
                """));

        Assert.Contains("invalid plugin ControlType", exception.Message);
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
    public void ImageAndBorderedImageControlTypesAreStable()
    {
        Assert.Equal("Image", new ImageFrontedControl().ControlType);
        Assert.Equal("BorderedImage", new BorderedImageFrontedControl().ControlType);
    }

    [Fact]
    public void FrontedControlRegistryResolvesBuiltInControls()
    {
        var registry = new FrontedControlRegistry([new TextFrontedControl()]);

        Assert.NotNull(registry.GetControl("Text"));
        Assert.Empty(registry.GetPluginDescriptors());
    }

    [Fact]
    public void PluginContributorRegistersDescriptorAndFactory()
    {
        var registry = new FrontedControlRegistry(
            [new TextFrontedControl()],
            [new TestPluginControlContributor()],
            NullLogger<FrontedControlRegistry>.Instance);

        var descriptor = registry.GetPluginDescriptor("plugin:top.plfjy.example.fronted/TeamCard");
        Assert.NotNull(descriptor);
        Assert.True(registry.IsPluginControlRegistered("plugin:top.plfjy.example.fronted/TeamCard"));
        Assert.Equal(typeof(TestPluginControlConfig), descriptor.ConfigType);
        Assert.NotNull(registry.GetControl("plugin:top.plfjy.example.fronted/TeamCard"));
        Assert.Contains(registry.GetControls(), control => control.ControlType == "Text");
        Assert.Contains(
            registry.GetControls(),
            control => control.ControlType == "plugin:top.plfjy.example.fronted/TeamCard");
    }

    [Fact]
    public void DuplicatePluginControlTypeFails()
    {
        Assert.Throws<FrontedLayoutConfigException>(() =>
            new FrontedControlRegistry(
                [new TextFrontedControl()],
                [new TestPluginControlContributor(), new TestPluginControlContributor()],
                NullLogger<FrontedControlRegistry>.Instance));
    }

    [Fact]
    public void PluginDescriptorCannotShadowExistingControlType()
    {
        Assert.Throws<FrontedLayoutConfigException>(() =>
            new FrontedControlRegistry(
                [new FakeFrontedControl("plugin:top.plfjy.example.fronted/TeamCard")],
                [new TestPluginControlContributor()],
                NullLogger<FrontedControlRegistry>.Instance));
    }

    [Fact]
    public void DuplicateBuiltInControlTypeFails()
    {
        Assert.Throws<FrontedLayoutConfigException>(() =>
            new FrontedControlRegistry(
                [new FakeFrontedControl("Text"), new FakeFrontedControl("Text")],
                [],
                NullLogger<FrontedControlRegistry>.Instance));
    }

    [Fact]
    public void PluginAdapterConvertsGenericConfigAndCallsCreateControl()
    {
        RunOnStaThread(() =>
        {
            var contributor = new TestPluginControlContributor();
            var registry = new FrontedControlRegistry(
                [new TextFrontedControl()],
                [contributor],
                NullLogger<FrontedControlRegistry>.Instance);

            var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
                """
                {
                  "Version": 3,
                  "CanvasWidth": 1440,
                  "CanvasHeight": 810,
                  "TeamCard1": {
                    "ControlType": "plugin:top.plfjy.example.fronted/TeamCard",
                    "Left": 100,
                    "Top": 120,
                    "Width": 260,
                    "Height": 96,
                    "TeamNameBindingPath": "CurrentGame.HomeTeam.Name",
                    "Count": 5
                  }
                }
                """);

            Assert.NotNull(config);
            var factory = Assert.IsType<FrontedPluginControlAdapter<TestPluginControlConfig>>(
                registry.GetControl("plugin:top.plfjy.example.fronted/TeamCard"));
            var element = factory.Create(
                "TeamCard1",
                config.Controls["TeamCard1"],
                CreateBuildContext());

            var border = Assert.IsType<Border>(element);
            Assert.Equal("TeamCard1", border.Name);
            Assert.Equal("CurrentGame.HomeTeam.Name", contributor.LastConfig?.TeamNameBindingPath);
            Assert.Equal(5, contributor.LastConfig?.Count);
            Assert.Equal("TeamCard1", contributor.LastName);
        });
    }

    [Fact]
    public void PluginAdapterInvalidGenericConfigConversionThrowsClearException()
    {
        RunOnStaThread(() =>
        {
            var registry = new FrontedControlRegistry(
                [new TextFrontedControl()],
                [new TestPluginControlContributor()],
                NullLogger<FrontedControlRegistry>.Instance);

            var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
                """
                {
                  "Version": 3,
                  "CanvasWidth": 1440,
                  "CanvasHeight": 810,
                  "TeamCard1": {
                    "ControlType": "plugin:top.plfjy.example.fronted/TeamCard",
                    "Left": 100,
                    "Top": 120,
                    "Count": "not-a-number"
                  }
                }
                """);

            Assert.NotNull(config);
            var factory = registry.GetControl("plugin:top.plfjy.example.fronted/TeamCard");
            Assert.NotNull(factory);
            var exception = Assert.Throws<FrontedLayoutConfigException>(() =>
                factory.Create("TeamCard1", config.Controls["TeamCard1"], CreateBuildContext()));

            Assert.Contains("could not be converted", exception.Message);
            Assert.Contains(nameof(TestPluginControlConfig), exception.Message);
        });
    }

    [Fact]
    public void FrontedRendererSkipsMissingPluginControlAndStillRendersBuiltInControls()
    {
        RunOnStaThread(() =>
        {
            var renderer = new FrontedRenderer(
                EmptyServiceProvider.Instance,
                new Mock<ISharedDataService>().Object,
                NullFrontedResourceResolver.Instance,
                new FrontedControlRegistry([new TextFrontedControl()]),
                NullLogger<FrontedRenderer>.Instance);
            var canvas = new Canvas();

            renderer.RenderToCanvas(
                canvas,
                new FrontedCanvasConfig
                {
                    CanvasWidth = 1440,
                    CanvasHeight = 810,
                    Controls =
                    {
                        ["MissingPlugin"] = new PluginFrontedControlConfig
                        {
                            ControlType = "plugin:top.plfjy.missing/TeamCard",
                            Left = 0,
                            Top = 0
                        },
                        ["Title"] = new TextFrontedControlConfig
                        {
                            Text = "Title",
                            Left = 10,
                            Top = 10
                        }
                    }
                },
                new FrontedRenderContext
                {
                    WindowId = "TestWindow",
                    CanvasName = "BaseCanvas"
                });

            Assert.Single(canvas.Children);
            Assert.IsType<Border>(canvas.Children[0]);
        });
    }

    [Fact]
    public void FrontedRendererStillThrowsForUnknownBuiltInControl()
    {
        RunOnStaThread(() =>
        {
            var renderer = new FrontedRenderer(
                EmptyServiceProvider.Instance,
                new Mock<ISharedDataService>().Object,
                NullFrontedResourceResolver.Instance,
                new FrontedControlRegistry([new TextFrontedControl()]),
                NullLogger<FrontedRenderer>.Instance);

            Assert.Throws<FrontedLayoutConfigException>(() =>
                renderer.RenderToCanvas(
                    new Canvas(),
                    new FrontedCanvasConfig
                    {
                        CanvasWidth = 1440,
                        CanvasHeight = 810,
                        Controls =
                        {
                            ["TeamCard"] = new FrontedControlConfigBase
                            {
                                ControlType = "TeamCard",
                                Left = 0,
                                Top = 0
                            }
                        }
                    },
                    new FrontedRenderContext
                    {
                        WindowId = "TestWindow",
                        CanvasName = "BaseCanvas"
                    }));
        });
    }

    [Fact]
    public void ImageFrontedControlCreatesDirectImageRoot()
    {
        RunOnStaThread(() =>
        {
            var control = new ImageFrontedControl();
            var element = control.Create(
                "Logo",
                new ImageFrontedControlConfig
                {
                    Left = 10,
                    Top = 20,
                    Width = 85,
                    Height = 85,
                    SizingMode = ImageSizingMode.FillContainer,
                    Stretch = "Fill",
                    HorizontalAlignment = "Center",
                    VerticalAlignment = "Top",
                    ZIndex = 3
                },
                CreateBuildContext());

            var image = Assert.IsType<WpfUiImage>(element);
            Assert.Equal(10, Canvas.GetLeft(image));
            Assert.Equal(20, Canvas.GetTop(image));
            Assert.Equal(85, image.Width);
            Assert.Equal(85, image.Height);
            Assert.Equal(3, Panel.GetZIndex(image));
            Assert.Equal(Stretch.Fill, image.Stretch);
            Assert.Equal(HorizontalAlignment.Center, image.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Top, image.VerticalAlignment);
        });
    }

    [Fact]
    public void ImageFrontedControlBindsSourceToSharedDataService()
    {
        RunOnStaThread(() =>
        {
            var sharedDataService = new Mock<ISharedDataService>().Object;
            var control = new ImageFrontedControl();
            var element = control.Create(
                "Pick",
                new ImageFrontedControlConfig
                {
                    BindingPath = "CurrentGame.PickedMapImage",
                    Stretch = "UniformToFill"
                },
                CreateBuildContext(sharedDataService));

            var image = Assert.IsType<WpfUiImage>(element);
            Assert.Equal(Stretch.UniformToFill, image.Stretch);
            var binding = BindingOperations.GetBinding(image, WpfUiImage.SourceProperty);
            Assert.NotNull(binding);
            Assert.Equal("CurrentGame.PickedMapImage", binding.Path.Path);
            Assert.Same(sharedDataService, binding.Source);
        });
    }

    [Fact]
    public void BorderedImageFrontedControlUsesOuterBorderAndDoesNotBindInnerSize()
    {
        RunOnStaThread(() =>
        {
            var control = new BorderedImageFrontedControl();
            var element = control.Create(
                "Header",
                new BorderedImageFrontedControlConfig
                {
                    Left = 10,
                    Top = 20,
                    Width = 120,
                    Height = 80,
                    ImageWidth = 64,
                    ImageHeight = 48,
                    ZIndex = 5,
                    SizingMode = ImageSizingMode.FillContainer,
                    Stretch = "UniformToFill"
                },
                CreateBuildContext());

            var border = Assert.IsType<Border>(element);
            Assert.Equal(10, Canvas.GetLeft(border));
            Assert.Equal(20, Canvas.GetTop(border));
            Assert.Equal(120, border.Width);
            Assert.Equal(80, border.Height);
            Assert.Equal(5, Panel.GetZIndex(border));

            var image = Assert.IsType<Image>(border.Child);
            Assert.Equal(64, image.Width);
            Assert.Equal(48, image.Height);
            Assert.Equal(Stretch.UniformToFill, image.Stretch);
            Assert.Equal(HorizontalAlignment.Stretch, image.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Stretch, image.VerticalAlignment);
            Assert.Null(BindingOperations.GetBinding(image, FrameworkElement.WidthProperty));
            Assert.Null(BindingOperations.GetBinding(image, FrameworkElement.HeightProperty));
        });
    }

    [Fact]
    public void BorderedImageFrontedControlReproducesLegacyBorderImageStructure()
    {
        RunOnStaThread(() =>
        {
            var sharedDataService = new Mock<ISharedDataService>().Object;
            var control = new BorderedImageFrontedControl();
            var element = control.Create(
                "SurPick0",
                new BorderedImageFrontedControlConfig
                {
                    Left = 1,
                    Top = 115,
                    Width = 346,
                    Height = 308.5,
                    BindingPath = "CurrentGame.SurPlayerList[0].Character.BigImage",
                    SizingMode = ImageSizingMode.OverflowCrop,
                    Stretch = "UniformToFill",
                    HorizontalAlignment = "Center",
                    VerticalAlignment = "Top",
                    ClipToBounds = true,
                    CornerRadius = 8,
                    ZIndex = 3
                },
                CreateBuildContext(sharedDataService));

            var border = Assert.IsType<Border>(element);
            Assert.Equal("SurPick0", border.Name);
            Assert.Equal(1, Canvas.GetLeft(border));
            Assert.Equal(115, Canvas.GetTop(border));
            Assert.Equal(346, border.Width);
            Assert.Equal(308.5, border.Height);
            Assert.Equal(3, Panel.GetZIndex(border));
            Assert.True(border.ClipToBounds);
            Assert.Equal(new CornerRadius(8), border.CornerRadius);

            var image = Assert.IsType<Image>(border.Child);
            Assert.True(double.IsNaN(image.Width));
            Assert.True(double.IsNaN(image.Height));
            Assert.Equal(Stretch.UniformToFill, image.Stretch);
            Assert.Equal(HorizontalAlignment.Center, image.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Top, image.VerticalAlignment);

            var binding = BindingOperations.GetBinding(image, Image.SourceProperty);
            Assert.NotNull(binding);
            Assert.Equal("CurrentGame.SurPlayerList[0].Character.BigImage", binding.Path.Path);
            Assert.Same(sharedDataService, binding.Source);
        });
    }

    [Fact]
    public void MapV2DisplayDeserializesMapKeyAndPresenterFillsOuterHost()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 160,
              "Arms_Factory": {
                "ControlType": "MapV2Display",
                "Left": 50.5,
                "Top": 0,
                "Width": 149,
                "Height": 160,
                "MapKey": "ArmsFactory"
              }
            }
            """);

        Assert.NotNull(config);
        var mapConfig = Assert.IsType<MapV2DisplayControlConfig>(config.Controls["Arms_Factory"]);
        Assert.Equal("ArmsFactory", mapConfig.MapKey);

        RunOnStaThread(() =>
        {
            var sharedDataService = new Mock<ISharedDataService>();
            sharedDataService
                .SetupGet(service => service.CurrentGame)
                .Returns(new Game(
                    new Team(Camp.Sur, TeamType.HomeTeam),
                    new Team(Camp.Hun, TeamType.AwayTeam),
                    GameProgress.Game1FirstHalf));

            var settingsHostService = new Mock<ISettingsHostService>();
            settingsHostService
                .SetupGet(service => service.Settings)
                .Returns(new Settings());

            var serviceProvider = new ServiceCollection()
                .AddSingleton(settingsHostService.Object)
                .BuildServiceProvider();

            var control = new MapV2DisplayFrontedControl();
            var element = control.Create(
                "Arms_Factory",
                mapConfig,
                CreateBuildContext(sharedDataService.Object, serviceProvider));

            var border = Assert.IsAssignableFrom<Border>(element);
            var presenter = Assert.IsType<MapV2Presenter>(border.Child);
            Assert.Equal(HorizontalAlignment.Stretch, presenter.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Stretch, presenter.VerticalAlignment);

            var widthBinding = BindingOperations.GetBinding(presenter, FrameworkElement.WidthProperty);
            var heightBinding = BindingOperations.GetBinding(presenter, FrameworkElement.HeightProperty);
            Assert.NotNull(widthBinding);
            Assert.NotNull(heightBinding);
            Assert.Equal(nameof(Border.ActualWidth), widthBinding.Path.Path);
            Assert.Equal(nameof(Border.ActualHeight), heightBinding.Path.Path);
            Assert.Same(border, widthBinding.Source);
            Assert.Same(border, heightBinding.Source);
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

    private static FrontedControlBuildContext CreateBuildContext(
        ISharedDataService sharedDataService = null,
        IServiceProvider services = null)
    {
        return new FrontedControlBuildContext
        {
            Services = services ?? EmptyServiceProvider.Instance,
            SharedDataService = sharedDataService ?? new Mock<ISharedDataService>().Object,
            ResourceResolver = NullFrontedResourceResolver.Instance,
            WindowId = "TestWindow",
            CanvasName = "BaseCanvas",
            Logger = NullLogger.Instance
        };
    }

    [Fact]
    public void FrontedLocalResourceStoreCopiesImageAndReturnsLocalBpuiUri()
    {
        var root = CreateTempFolder();
        try
        {
            var source = Path.Combine(root, "bad name .. image.png");
            WriteTinyPng(source);
            var store = new FrontedLocalResourceStore(Path.Combine(root, "local", "resources", "images"));

            var uri = store.StoreImage(source);

            Assert.StartsWith("bpui://local/resources/images/bad-name-image-", uri);
            Assert.DoesNotContain(Path.GetPathRoot(root)!, uri);
            var fileName = uri["bpui://local/resources/images/".Length..];
            Assert.True(File.Exists(Path.Combine(root, "local", "resources", "images", fileName)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FrontedLocalResourceStoreRejectsUnsupportedExtension()
    {
        var root = CreateTempFolder();
        try
        {
            var source = Path.Combine(root, "image.svg");
            File.WriteAllText(source, "<svg />");
            var store = new FrontedLocalResourceStore(Path.Combine(root, "images"));

            Assert.Throws<NotSupportedException>(() => store.StoreImage(source));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FrontedResourceResolverResolvesBpuiLocalAndPackageResources()
    {
        var packageRoot = AppConstants.FrontedLayoutPackagesPath;
        var localFolder = Path.Combine(packageRoot, "local", "resources", "images");
        var packageFolder = Path.Combine(packageRoot, "package-id", "resources", "images");
        Directory.CreateDirectory(localFolder);
        Directory.CreateDirectory(packageFolder);
        var localFile = Path.Combine(localFolder, "foo.png");
        var packageFile = Path.Combine(packageFolder, "foo.png");
        File.WriteAllBytes(localFile, [1]);
        File.WriteAllBytes(packageFile, [2]);

        try
        {
            var resolver = new FrontedResourceResolver(NullLogger<FrontedResourceResolver>.Instance);

            Assert.Equal(localFile, resolver.ResolveImagePath("bpui://local/resources/images/foo.png"));
            Assert.Equal(packageFile, resolver.ResolveImagePath("bpui://package-id/resources/images/foo.png"));
            Assert.Null(resolver.ResolveImagePath("bpui://package-id/resources/../foo.png"));
            Assert.Null(resolver.ResolveImagePath("bpui://bad%2fid/resources/images/foo.png"));
            Assert.Null(resolver.ResolveImagePath("bpui://package-id/resources/images/missing.png"));
        }
        finally
        {
            File.Delete(localFile);
            File.Delete(packageFile);
        }
    }

    [Fact]
    public async Task FrontedWindowLayoutOptionsServiceSavesLoadsAndResetsWindowJson()
    {
        var root = CreateTempFolder();
        try
        {
            var service = new FrontedWindowLayoutOptionsService(root);

            Assert.False(service.LoadOptions("WidgetsWindow").AllowTransparency);
            await service.SaveOptionsAsync(
                "WidgetsWindow",
                new FrontedWindowLayoutOptions { AllowTransparency = true });

            var path = Path.Combine(root, "WidgetsWindow", "window.json");
            Assert.True(File.Exists(path));
            Assert.True(service.LoadOptions("WidgetsWindow").AllowTransparency);

            await service.ResetOptionsAsync("WidgetsWindow");
            Assert.False(File.Exists(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FrontedCanvasConfigDoesNotSerializeAllowTransparency()
    {
        var json = JsonSerializer.Serialize(new FrontedCanvasConfig
        {
            CanvasWidth = 100,
            CanvasHeight = 100
        });

        Assert.DoesNotContain("AllowTransparency", json, StringComparison.Ordinal);
    }

    private static string CreateTempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteTinyPng(string path)
    {
        File.WriteAllBytes(
            path,
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="));
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

    private static FrontedCanvasConfig ReadBuiltInLayout(string windowTypeName, string canvasName = "BaseCanvas")
    {
        var path = GetBuiltInLayoutPath(windowTypeName, canvasName);

        Assert.True(File.Exists(path), path);

        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(File.ReadAllText(path));
        Assert.NotNull(config);
        return config;
    }

    private static string GetBuiltInLayoutPath(string windowTypeName, string canvasName = "BaseCanvas") =>
        Path.Combine(
            AppConstants.ResourcesPath,
            "FrontedLayouts",
            windowTypeName,
            $"{canvasName}.json");

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
        var control = Assert.IsAssignableFrom<ImageFrontedControlConfig>(config.Controls[controlName]);
        Assert.Equal(bindingPath, control.BindingPath);
        return control;
    }

    private sealed class TestPluginControlConfig : FrontedControlConfigBase
    {
        public string TeamNameBindingPath { get; set; }

        public int Count { get; set; }
    }

    private sealed class TestPluginControlContributor : IFrontedControlPluginContributor
    {
        public string LastName { get; private set; }

        public TestPluginControlConfig LastConfig { get; private set; }

        public void RegisterFrontedControls(IFrontedControlPluginRegistry registry)
        {
            registry.Register(new FrontedPluginControlDescriptor<TestPluginControlConfig>
            {
                PackageId = "top.plfjy.example.fronted",
                ControlTypeName = "TeamCard",
                ConfigType = typeof(TestPluginControlConfig),
                DisplayNameKey = "TeamCard",
                Properties =
                [
                    new FrontedPluginPropertyDescriptor
                    {
                        PropertyName = nameof(TestPluginControlConfig.TeamNameBindingPath)
                    }
                ],
                CreateControl = (name, config, _) =>
                {
                    LastName = name;
                    LastConfig = config;
                    return new Border
                    {
                        Name = name,
                        Width = config.Width ?? 0,
                        Height = config.Height ?? 0
                    };
                }
            });
        }
    }

    private sealed class FakeFrontedControl(string controlType) : IFrontedControl
    {
        public string ControlType { get; } = controlType;

        public Type ConfigType => typeof(FrontedControlConfigBase);

        public FrameworkElement Create(
            string name,
            FrontedControlConfigBase config,
            FrontedControlBuildContext context)
        {
            return new Border { Name = name };
        }
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
