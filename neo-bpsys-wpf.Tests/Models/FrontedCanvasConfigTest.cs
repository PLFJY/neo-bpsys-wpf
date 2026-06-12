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
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedCanvasConfigTest
{
    [Fact]
    public void FrontedControlConfigBase_DoesNotExposeOrSerializeBehaviorTags_AndIgnoresLegacyField()
    {
        Assert.Null(typeof(FrontedControlConfigBase).GetProperty("BehaviorTags"));

        var json = JsonSerializer.Serialize(new FrontedControlConfigBase { ControlType = "Text" });
        Assert.DoesNotContain("BehaviorTags", json, StringComparison.Ordinal);

        var legacy = JsonSerializer.Deserialize<FrontedControlConfigBase>(
            """{"ControlType":"Text","BehaviorTags":{"Camp":"Sur"}}""");
        Assert.NotNull(legacy);
        Assert.Equal("Text", legacy.ControlType);
    }

    [Fact]
    public void BehaviorGuid_JsonRoundTrip_PreservesGuid()
    {
        var behaviorGuid = Guid.NewGuid();
        var config = new FrontedCanvasConfig
        {
            CanvasWidth = 400,
            CanvasHeight = 300,
            Controls =
            {
                ["Title"] = new TextFrontedControlConfig
                {
                    ControlType = "Text",
                    Text = "Hello",
                    BehaviorGuid = behaviorGuid
                }
            }
        };

        var roundTrip = JsonSerializer.Deserialize<FrontedCanvasConfig>(JsonSerializer.Serialize(config));

        Assert.NotNull(roundTrip);
        Assert.Equal(behaviorGuid, roundTrip.Controls["Title"].BehaviorGuid);
    }

    [Fact]
    public void BehaviorGuid_MissingGuid_DeserializesAsEmpty()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 400,
              "CanvasHeight": 300,
              "Title": {
                "ControlType": "Text",
                "Text": "Hello"
              }
            }
            """);

        Assert.NotNull(config);
        Assert.Equal(Guid.Empty, config.Controls["Title"].BehaviorGuid);
    }

    [Fact]
    public void BehaviorGuid_EmptyGuid_NotSerialized()
    {
        var config = new FrontedCanvasConfig
        {
            CanvasWidth = 400,
            CanvasHeight = 300,
            Controls =
            {
                ["Title"] = new TextFrontedControlConfig
                {
                    ControlType = "Text",
                    Text = "Hello",
                    BehaviorGuid = Guid.Empty
                }
            }
        };

        var json = JsonSerializer.Serialize(config);

        Assert.DoesNotContain(nameof(FrontedControlConfigBase.BehaviorGuid), json, StringComparison.Ordinal);
    }

    [Fact]
    public void CanvasConfigRoundTripsBoModeStatesWithoutBackgroundImageVariants()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 195,
              "BackgroundImage": "Resources/scoreGlobal.png",
              "EnableBoModeStates": true,
              "BoModeStates": {
                "Bo3": {
                  "BackgroundImage": "Resources/scoreGlobalBo3.png",
                  "Controls": {
                    "Title": {
                      "ControlType": "Text",
                      "Visibility": "Hidden",
                      "Left": 1,
                      "Top": 2
                    }
                  }
                }
              }
            }
            """);

        Assert.NotNull(config);
        Assert.True(config!.EnableBoModeStates);
        Assert.Equal(
            "Resources/scoreGlobalBo3.png",
            config.BoModeStates["Bo3"].BackgroundImage);
        Assert.Equal(FrontedControlVisibility.Hidden, config.BoModeStates["Bo3"].Controls["Title"].Visibility);

        var json = JsonSerializer.Serialize(config);

        Assert.Contains("BoModeStates", json, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundImageVariants", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ScoreGlobal.Bo3", json, StringComparison.Ordinal);
    }

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
                "TextBinding": {
                  "Sources": [
                    { "Path": "CurrentGame.SurTeam.Name" }
                  ],
                  "StringFormat": "{0}%"
                },
                "Text": "Ignored when TextBinding has sources",
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
                "ImagePath": "Resources/static.png",
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
        Assert.Equal("CurrentGame.SurTeam.Name", Assert.Single(text.TextBinding!.Sources).Path);
        Assert.Equal("Ignored when TextBinding has sources", text.Text);
        Assert.Equal("{0}%", text.TextBinding.StringFormat);
        Assert.Equal("Bold", text.FontWeight);
        Assert.Equal(28, text.FontSize);
        Assert.Equal(2, text.ZIndex);

        var image = Assert.IsType<ImageFrontedControlConfig>(config.Controls["SurPick1"]);
        Assert.Equal(143, image.Left);
        Assert.Equal(160, image.Height);
        Assert.Equal("Resources/static.png", image.ImagePath);
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
                "ImagePath": "Resources/static-pick.png",
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
        Assert.Equal("Resources/static-pick.png", image.ImagePath);
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
                "FontFamily": "pack://application:,,,/Assets/Fonts/#华康POP1体W5",
                "FontWeight": "Bold",
                "Color": "#FFFFFFFF",
                "FontSize": 24,
                "ShowCampIcon": true,
                "ZIndex": 2,
                "Cells": [
                  {
                    "Id": "Game1FirstHalf",
                    "GameNumber": 1,
                    "GameKind": "Normal",
                    "HalfKind": "FirstHalf",
                    "X": 0,
                    "Y": 0,
                    "Width": 75,
                    "Height": 32,
                    "Visibility": "Hidden",
                    "FontFamily": "Arial",
                    "FontWeight": "Normal",
                    "Color": "#FF112233",
                    "FontSize": 18,
                    "ShowCampIcon": false
                  }
                ]
              }
            }
            """);

        Assert.NotNull(config);
        var row = Assert.IsType<GlobalScoreRowControlConfig>(config.Controls["MainGlobalScoreRow"]);
        Assert.Equal("GlobalScoreRow", row.ControlType);
        Assert.Equal(175, row.Left);
        Assert.Equal(93, row.Top);
        Assert.Equal(neo_bpsys_wpf.Core.Enums.TeamType.HomeTeam, row.TeamType);
        Assert.Equal("Bold", row.FontWeight);
        Assert.Equal(24, row.FontSize);
        Assert.True(row.ShowCampIcon);
        var cell = Assert.Single(row.Cells);
        Assert.Equal("Game1FirstHalf", cell.Id);
        Assert.Equal(1, cell.GameNumber);
        Assert.Equal(ScoreGameKind.Normal, cell.GameKind);
        Assert.Equal(ScoreHalfKind.FirstHalf, cell.HalfKind);
        Assert.Equal(0, cell.X);
        Assert.Equal(75, cell.Width);
        Assert.Equal(FrontedControlVisibility.Hidden, cell.Visibility);
        Assert.Equal("Arial", cell.FontFamily);
        Assert.Equal("Normal", cell.FontWeight);
        Assert.Equal("#FF112233", cell.Color);
        Assert.Equal(18, cell.FontSize);
        Assert.False(cell.ShowCampIcon);
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

        var map = Assert.IsType<MapV2DisplayControlConfig>(config.Controls["Map"]);
        Assert.Equal("MapV2Display", map.ControlType);
        Assert.Equal("ArmsFactory", map.MapKey);

        var pickedMapName = Assert.IsType<MapNameTextControlConfig>(config.Controls["PickedMapName"]);
        Assert.Equal("CurrentGame.PickedMap", pickedMapName.BindingPath);
        var bannedMapName = Assert.IsType<MapNameTextControlConfig>(config.Controls["BannedMapName"]);
        Assert.Equal("CurrentGame.BannedMap", bannedMapName.BindingPath);
    }

    [Fact]
    public void CurrentBanDisplayControlTypeIsNoLongerSupported()
    {
        var exception = Assert.Throws<FrontedLayoutConfigException>(() => JsonSerializer.Deserialize<FrontedCanvasConfig>(
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
                "Height": 35
              }
            }
            """));

        Assert.Contains("unsupported ControlType 'CurrentBanDisplay'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BanSlotDisplay")]
    [InlineData("PickingBorderOverlay")]
    public void LegacyCompatibilityControlTypesAreNoLongerSupported(string controlType)
    {
        var exception = Assert.Throws<FrontedLayoutConfigException>(() => JsonSerializer.Deserialize<FrontedCanvasConfig>(
            $$"""
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "Legacy": {
                "ControlType": "{{controlType}}"
              }
            }
            """));

        Assert.Contains($"unsupported ControlType '{controlType}'", exception.Message, StringComparison.Ordinal);
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
        Assert.Equal("Center", map.HorizontalAlignment);
        Assert.Equal("Center", map.VerticalAlignment);
        Assert.Equal(ImageSizingMode.Auto, map.SizingMode);

        for (var index = 0; index < 4; index++)
        {
            var pick = Assert.IsType<BorderedImageFrontedControlConfig>(config.Controls[$"SurPick{index}"]);
            Assert.Equal($"CurrentGame.SurPlayerList[{index}].Character.BigImage", pick.BindingPath);
            Assert.Equal(ImageSizingMode.OverflowCrop, pick.SizingMode);
            Assert.Equal("UniformToFill", pick.Stretch);
            Assert.True(pick.ClipToBounds);
            Assert.Equal("Center", pick.HorizontalAlignment);
            Assert.Equal("Top", pick.VerticalAlignment);
        }

        var hunPick = Assert.IsType<BorderedImageFrontedControlConfig>(config.Controls["HunPick"]);
        Assert.Equal("CurrentGame.HunPlayer.Character.BigImage", hunPick.BindingPath);
        Assert.Equal(ImageSizingMode.OverflowCrop, hunPick.SizingMode);
        Assert.Equal("UniformToFill", hunPick.Stretch);
        Assert.True(hunPick.ClipToBounds);
        Assert.Equal("Center", hunPick.HorizontalAlignment);
        Assert.Equal("Top", hunPick.VerticalAlignment);

        AssertTextBinding(config, "SurTeamName", "CurrentGame.SurTeam.Name");
        AssertTextBinding(config, "HunTeamName", "CurrentGame.HunTeam.Name");
        AssertTextBinding(config, "SurTeamMajorPoint", "CurrentGame.MatchScore.CurrentSurTeamMajorText");
        AssertTextBinding(config, "SurId0", "CurrentGame.SurPlayerList[0].Member.Name");
        AssertTextBinding(config, "SurId1", "CurrentGame.SurPlayerList[1].Member.Name");
        AssertTextBinding(config, "SurId2", "CurrentGame.SurPlayerList[2].Member.Name");
        AssertTextBinding(config, "SurId3", "CurrentGame.SurPlayerList[3].Member.Name");
        AssertTextBinding(config, "HunId", "CurrentGame.HunPlayer.Member.Name");

        var mapName = Assert.IsType<MapNameTextControlConfig>(config.Controls["MapName"]);
        Assert.Equal("MapNameText", mapName.ControlType);
        var progress = Assert.IsType<GameProgressTextControlConfig>(config.Controls["GameProgress"]);
        Assert.Equal("GameProgressText", progress.ControlType);

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
            Assert.Equal("{0}%", machineDecoded.TextBinding!.StringFormat);

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
    }

    [Fact]
    public void ReadsBuiltInBpOverviewAndMapV2WindowLayouts()
    {
        var bpOverViewCanvas = ReadBuiltInLayout("BpOverviewWindow");
        var mapV2Canvas = ReadBuiltInLayout("MapV2Window");

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

        var bpOverViewCanvasText = File.ReadAllText(GetBuiltInLayoutPath("BpOverviewWindow"));
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
            Assert.Equal("#FF2B483B", display.MapBorderNormalColor);
            Assert.Equal("#FF9C3E2F", display.MapBorderBannedColor);
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
            Assert.True(pick.PickingBorderAvailable);
            Assert.Equal($"SurPickingBorder{index}", pick.PickingBorderName);
            Assert.Equal("Resources/pickingBorder.png", pick.PickingBorderImagePath);
            Assert.DoesNotContain($"SurPickingBorder{index}", config.Controls.Keys);
        }

        var hunPick = AssertImageBinding(config, "HunPick", "CurrentGame.HunPlayer.PictureShown");
        Assert.Equal(ImageSizingMode.Auto, hunPick.SizingMode);
        Assert.Equal("Uniform", hunPick.Stretch);
        Assert.Equal("Center", hunPick.HorizontalAlignment);
        Assert.Equal("Center", hunPick.VerticalAlignment);
        Assert.True(hunPick.PickingBorderAvailable);
        Assert.Equal("HunPickingBorder", hunPick.PickingBorderName);
        Assert.Equal("Resources/pickingBorder.png", hunPick.PickingBorderImagePath);
        Assert.DoesNotContain("HunPickingBorder", config.Controls.Keys);

        var map = AssertImageBinding(config, "Map", "CurrentGame.PickedMapImageLarge");
        Assert.Equal(ImageSizingMode.OverflowCrop, map.SizingMode);
        Assert.Equal("UniformToFill", map.Stretch);

        Assert.IsType<MapNameTextControlConfig>(config.Controls["MapName"]);
        var gameProgress = Assert.IsType<GameProgressTextControlConfig>(config.Controls["GameProgress"]);

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
            var ban = Assert.IsType<ImageFrontedControlConfig>(config.Controls[controlName]);
            Assert.True(ban.Lockable);
            Assert.Equal(FrontedOverlayVisibilityMode.VisibleWhenFalse, ban.LockVisibleWhen);
            Assert.Contains("HeaderImageSingleColor", ban.BindingPath, StringComparison.Ordinal);
            Assert.EndsWith("BannedList[" + ExtractTrailingIndex(controlName) + "]", ban.LockVisibilityBindingPath);
        }
    }

    [Fact]
    public void BuiltInGameProgressTextLayoutsUseExpectedDisplayMode()
    {
        var cutScene = ReadBuiltInLayout("CutSceneWindow");
        var gameData = ReadBuiltInLayout("GameDataWindow");
        var widgetsOverview = ReadBuiltInLayout("BpOverviewWindow");
        var bpWindow = ReadBuiltInLayout("BpWindow");

        Assert.Equal(GameProgressTextDisplayMode.Inline,
            Assert.IsType<GameProgressTextControlConfig>(cutScene.Controls["GameProgress"]).DisplayMode);
        Assert.Equal(GameProgressTextDisplayMode.Inline,
            Assert.IsType<GameProgressTextControlConfig>(gameData.Controls["GameProgress"]).DisplayMode);
        Assert.Equal(GameProgressTextDisplayMode.Inline,
            Assert.IsType<GameProgressTextControlConfig>(widgetsOverview.Controls["GameProgress"]).DisplayMode);
        Assert.Equal(GameProgressTextDisplayMode.Inline,
            Assert.IsType<GameProgressTextControlConfig>(bpWindow.Controls["GameProgress"]).DisplayMode);
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
    public void FrontedRendererUsesGenericBo3StateBackgroundInBo3Mode()
    {
        RunOnStaThread(() =>
        {
            var sharedDataService = new Mock<ISharedDataService>();
            sharedDataService.SetupGet(service => service.IsBo3Mode).Returns(true);
            var resolver = new RecordingFrontedResourceResolver();
            var renderer = new FrontedRenderer(
                EmptyServiceProvider.Instance,
                sharedDataService.Object,
                resolver,
                new FrontedControlRegistry([]),
                NullLogger<FrontedRenderer>.Instance);

            renderer.RenderToCanvas(
                new Canvas(),
                new FrontedCanvasConfig
                {
                    Version = 3,
                    CanvasWidth = 100,
                    CanvasHeight = 100,
                    BackgroundImage = "Resources/default.png",
                    EnableBoModeStates = true,
                    BoModeStates =
                    {
                        ["Bo3"] = new FrontedCanvasStateConfig { BackgroundImage = "Resources/bo3.png" }
                    }
                },
                new FrontedRenderContext
                {
                    WindowId = "ScoreGlobalWindow",
                    WindowTypeName = "ScoreGlobalWindow",
                    CanvasName = "BaseCanvas"
                });

            Assert.Equal("Resources/bo3.png", resolver.LastResolvedImagePath);

            sharedDataService.SetupGet(service => service.IsBo3Mode).Returns(false);
            renderer.RenderToCanvas(
                new Canvas(),
                new FrontedCanvasConfig
                {
                    Version = 3,
                    CanvasWidth = 100,
                    CanvasHeight = 100,
                    BackgroundImage = "Resources/default.png",
                    EnableBoModeStates = true,
                    BoModeStates =
                    {
                        ["Bo3"] = new FrontedCanvasStateConfig { BackgroundImage = "Resources/bo3.png" }
                    }
                },
                new FrontedRenderContext
                {
                    WindowId = "ScoreGlobalWindow",
                    WindowTypeName = "ScoreGlobalWindow",
                    CanvasName = "BaseCanvas"
                });

            Assert.Equal("Resources/default.png", resolver.LastResolvedImagePath);
        });
    }

    [Fact]
    public void FrontedRendererFallsBackToDefaultBackgroundWhenBo3StateIsMissing()
    {
        RunOnStaThread(() =>
        {
            var sharedDataService = new Mock<ISharedDataService>();
            sharedDataService.SetupGet(service => service.IsBo3Mode).Returns(true);
            var resolver = new RecordingFrontedResourceResolver();
            var renderer = new FrontedRenderer(
                EmptyServiceProvider.Instance,
                sharedDataService.Object,
                resolver,
                new FrontedControlRegistry([]),
                NullLogger<FrontedRenderer>.Instance);

            renderer.RenderToCanvas(
                new Canvas(),
                new FrontedCanvasConfig
                {
                    Version = 3,
                    CanvasWidth = 100,
                    CanvasHeight = 100,
                    BackgroundImage = "Resources/default.png",
                    EnableBoModeStates = true
                },
                new FrontedRenderContext
                {
                    WindowId = "ScoreGlobalWindow",
                    WindowTypeName = "ScoreGlobalWindow",
                    CanvasName = "BaseCanvas"
                });

            Assert.Equal("Resources/default.png", resolver.LastResolvedImagePath);
        });
    }

    [Fact]
    public void ScoreGlobalWindowReloadsV3LayoutWhenBoModeChanges()
    {
        var repoRoot = FindRepositoryRoot();
        var sourcePath = Path.Combine(repoRoot, "neo-bpsys-wpf.Core", "Controls", "FrontedWindowBase.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("ISharedDataService? _sharedDataService", source, StringComparison.Ordinal);
        Assert.Contains("IsBo3ModeChanged += OnBoModeChanged", source, StringComparison.Ordinal);
        Assert.Contains("IsBo3ModeChanged -= OnBoModeChanged", source, StringComparison.Ordinal);
        Assert.Contains("MarkLayoutDirty();", source, StringComparison.Ordinal);
        Assert.Contains("_ = LoadOrReloadContentAsync();", source, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.BeginInvoke", source, StringComparison.Ordinal);
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
    public void TextFrontedControlUsesStaticTextWhenTextBindingHasNoSources()
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
    public void TextFrontedControlTextBindingTakesPriorityOverStaticText()
    {
        RunOnStaThread(() =>
        {
            var sharedDataService = new Mock<ISharedDataService>().Object;
            var control = new TextFrontedControl();
            var element = control.Create(
                "Title",
                new TextFrontedControlConfig
                {
                    TextBinding = CreateTextBinding("CurrentGame.SurTeam.Name"),
                    Text = "Static title"
                },
                CreateBuildContext(sharedDataService));

            var border = Assert.IsType<Border>(element);
            var textBlock = Assert.IsType<TextBlock>(border.Child);
            var binding = BindingOperations.GetMultiBinding(textBlock, TextBlock.TextProperty);
            Assert.NotNull(binding);
            var sourceBinding = Assert.IsType<Binding>(Assert.Single(binding.Bindings));
            Assert.Equal("CurrentGame.SurTeam.Name", sourceBinding.Path.Path);
            Assert.Same(sharedDataService, sourceBinding.Source);
            Assert.NotEqual("Static title", textBlock.Text);
        });
    }

    [Fact]
    public void TextFrontedControlAppliesStringFormatOnlyForTextBinding()
    {
        RunOnStaThread(() =>
        {
            var control = new TextFrontedControl();
            var element = control.Create(
                "DecodingProgress",
                new TextFrontedControlConfig
                {
                    TextBinding = new FrontedTextBindingExpression
                    {
                        Sources = [new FrontedBindingSourceConfig { Path = "CurrentGame.SurPlayerList[0].Data.DecodingProgress" }],
                        StringFormat = "{0}%"
                    }
                },
                CreateBuildContext(new Mock<ISharedDataService>().Object));

            var border = Assert.IsType<Border>(element);
            var textBlock = Assert.IsType<TextBlock>(border.Child);
            var binding = BindingOperations.GetMultiBinding(textBlock, TextBlock.TextProperty);
            Assert.NotNull(binding);
            Assert.Equal("{0}%", Assert.IsType<FrontedTextBindingExpression>(binding.ConverterParameter).StringFormat);

            var staticElement = control.Create(
                "Title",
                new TextFrontedControlConfig
                {
                    Text = "Static title"
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

            var root = Assert.IsType<Grid>(element);
            var image = Assert.IsType<Image>(Assert.Single(root.Children.OfType<Image>()));
            Assert.Equal(10, Canvas.GetLeft(root));
            Assert.Equal(20, Canvas.GetTop(root));
            Assert.Equal(85, root.Width);
            Assert.Equal(85, root.Height);
            Assert.Equal(3, Panel.GetZIndex(root));
            Assert.Equal(Stretch.Fill, image.Stretch);
            Assert.Equal(HorizontalAlignment.Center, image.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Top, image.VerticalAlignment);
        });
    }

    [Fact]
    public void ImageFrontedControlMarksGeneratedAnimationParts()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var element = new ImageFrontedControl().Create(
                "BanSlot",
                new ImageFrontedControlConfig
                {
                    BehaviorGuid = guid,
                    Lockable = true,
                    PickingBorderAvailable = true
                },
                CreateBuildContext());

            var root = Assert.IsType<Grid>(element);
            var lockOverlay = Assert.Single(
                root.Children.OfType<Image>(),
                item => FrontedRendererProperties.GetAnimationPartName(item) == "LockOverlay");
            var pickingBorder = Assert.Single(
                root.Children.OfType<Border>(),
                item => FrontedRendererProperties.GetAnimationPartName(item) == "PickingBorder");

            AssertAnimationPart(lockOverlay, guid, "BanSlot", "LockOverlay");
            AssertAnimationPart(pickingBorder, guid, "BanSlot", "PickingBorder");
        });
    }

    [Fact]
    public void ImageFrontedControlDoesNotCreateDisabledAnimationParts()
    {
        RunOnStaThread(() =>
        {
            var element = new ImageFrontedControl().Create(
                "BanSlot",
                new ImageFrontedControlConfig
                {
                    BehaviorGuid = Guid.NewGuid(),
                    Lockable = false,
                    PickingBorderAvailable = false
                },
                CreateBuildContext());

            var root = Assert.IsType<Grid>(element);
            Assert.DoesNotContain(
                root.Children.OfType<FrameworkElement>(),
                FrontedRendererProperties.GetIsAnimationAuxiliaryElement);
        });
    }

    [Fact]
    public void ImageFrontedControlBindsSourceToSharedDataService()
    {
        RunOnStaThread(() =>
        {
            var sharedDataService = new Mock<ISharedDataService>().Object;
            var resolver = new RecordingFrontedResourceResolver { ThrowOnResolveImage = true };
            var control = new ImageFrontedControl();
            var element = control.Create(
                "Pick",
                new ImageFrontedControlConfig
                {
                    BindingPath = "CurrentGame.PickedMapImage",
                    ImagePath = "Resources/static.png",
                    Stretch = "UniformToFill"
                },
                CreateBuildContext(sharedDataService, resourceResolver: resolver));

            var root = Assert.IsType<Grid>(element);
            var image = Assert.IsType<Image>(Assert.Single(root.Children.OfType<Image>()));
            Assert.Equal(Stretch.UniformToFill, image.Stretch);
            var binding = BindingOperations.GetBinding(image, Image.SourceProperty);
            Assert.NotNull(binding);
            Assert.Equal("CurrentGame.PickedMapImage", binding.Path.Path);
            Assert.Same(sharedDataService, binding.Source);
            Assert.Null(image.Source);
            Assert.Null(resolver.LastResolvedImagePath);
        });
    }

    [Fact]
    public void ImageFrontedControlUsesStaticImagePathWhenBindingPathIsEmpty()
    {
        RunOnStaThread(() =>
        {
            var source = new DrawingImage();
            source.Freeze();
            var resolver = new RecordingFrontedResourceResolver { ImageSource = source };
            var control = new ImageFrontedControl();

            var element = control.Create(
                "Logo",
                new ImageFrontedControlConfig
                {
                    ImagePath = "Resources/logo.png"
                },
                CreateBuildContext(resourceResolver: resolver));

            var root = Assert.IsType<Grid>(element);
            var image = Assert.IsType<Image>(Assert.Single(root.Children.OfType<Image>()));
            Assert.Same(source, image.Source);
            Assert.Null(BindingOperations.GetBinding(image, Image.SourceProperty));
            Assert.Equal("Resources/logo.png", resolver.LastResolvedImagePath);
            Assert.Equal(FrontedImagePurpose.UiElement, resolver.LastPurpose);
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

            var grid = Assert.IsType<Grid>(border.Child);
            var image = Assert.IsType<Image>(Assert.Single(grid.Children.OfType<Image>()));
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
    public void BorderedImageFrontedControlUsesStaticImagePathWhenBindingPathIsEmpty()
    {
        RunOnStaThread(() =>
        {
            var source = new DrawingImage();
            source.Freeze();
            var resolver = new RecordingFrontedResourceResolver { ImageSource = source };
            var control = new BorderedImageFrontedControl();

            var element = control.Create(
                "Pick",
                new BorderedImageFrontedControlConfig
                {
                    ImagePath = "Resources/pick.png"
                },
                CreateBuildContext(resourceResolver: resolver));

            var border = Assert.IsType<Border>(element);
            var grid = Assert.IsType<Grid>(border.Child);
            var image = Assert.IsType<Image>(Assert.Single(grid.Children.OfType<Image>()));
            Assert.Same(source, image.Source);
            Assert.Null(BindingOperations.GetBinding(image, Image.SourceProperty));
            Assert.Equal("Resources/pick.png", resolver.LastResolvedImagePath);
            Assert.Equal(FrontedImagePurpose.UiElement, resolver.LastPurpose);
        });
    }

    [Fact]
    public void BorderedImageFrontedControlReproducesLegacyBorderImageStructure()
    {
        RunOnStaThread(() =>
        {
            var sharedDataService = new Mock<ISharedDataService>().Object;
            var resolver = new RecordingFrontedResourceResolver { ThrowOnResolveImage = true };
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
                    ImagePath = "Resources/static.png",
                    SizingMode = ImageSizingMode.OverflowCrop,
                    Stretch = "UniformToFill",
                    HorizontalAlignment = "Center",
                    VerticalAlignment = "Top",
                    ClipToBounds = true,
                    CornerRadius = 8,
                    ZIndex = 3
                },
                CreateBuildContext(sharedDataService, resourceResolver: resolver));

            var border = Assert.IsType<Border>(element);
            Assert.Equal("SurPick0", border.Name);
            Assert.Equal(1, Canvas.GetLeft(border));
            Assert.Equal(115, Canvas.GetTop(border));
            Assert.Equal(346, border.Width);
            Assert.Equal(308.5, border.Height);
            Assert.Equal(3, Panel.GetZIndex(border));
            Assert.True(border.ClipToBounds);
            Assert.Equal(new CornerRadius(8), border.CornerRadius);

            var grid = Assert.IsType<Grid>(border.Child);
            var image = Assert.IsType<Image>(Assert.Single(grid.Children.OfType<Image>()));
            Assert.True(double.IsNaN(image.Width));
            Assert.True(double.IsNaN(image.Height));
            Assert.Equal(Stretch.UniformToFill, image.Stretch);
            Assert.Equal(HorizontalAlignment.Center, image.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Top, image.VerticalAlignment);

            var binding = BindingOperations.GetBinding(image, Image.SourceProperty);
            Assert.NotNull(binding);
            Assert.Equal("CurrentGame.SurPlayerList[0].Character.BigImage", binding.Path.Path);
            Assert.Same(sharedDataService, binding.Source);
            Assert.Null(image.Source);
            Assert.Null(resolver.LastResolvedImagePath);
        });
    }

    [Fact]
    public void StaticImagePathResolveFailureDoesNotCrash()
    {
        RunOnStaThread(() =>
        {
            var control = new ImageFrontedControl();
            var element = control.Create(
                "Missing",
                new ImageFrontedControlConfig
                {
                    ImagePath = "Resources/missing.png"
                },
                CreateBuildContext(resourceResolver: new RecordingFrontedResourceResolver()));

            var root = Assert.IsType<Grid>(element);
            var image = Assert.IsType<Image>(Assert.Single(root.Children.OfType<Image>()));
            Assert.Null(image.Source);
        });
    }

    [Fact]
    public void PlayerPictureShownVariantsUseCharacterImagesAndFallbackToMemberImage()
    {
        RunOnStaThread(() =>
        {
            var memberImage = new DrawingImage();
            memberImage.Freeze();
            var player = new Player(new Member(Camp.Sur) { Image = memberImage });

            Assert.Same(memberImage, player.PictureShown);
            Assert.Same(memberImage, player.PictureShownWithFullCharacter);
            Assert.Same(memberImage, player.PictureShownHeader);

            var changed = new List<string>();
            player.PropertyChanged += (_, args) => changed.Add(args.PropertyName!);
            var character = new Character("幸运儿", Camp.Sur, "幸运儿.png");

            player.Character = character;

            Assert.Same(character.HalfImage, player.PictureShown);
            Assert.Same(character.BigImage, player.PictureShownWithFullCharacter);
            Assert.Same(character.HeaderImage, player.PictureShownHeader);
            Assert.Contains(nameof(Player.PictureShown), changed);
            Assert.Contains(nameof(Player.PictureShownWithFullCharacter), changed);
            Assert.Contains(nameof(Player.PictureShownHeader), changed);

            changed.Clear();
            var nextMemberImage = new DrawingImage();
            nextMemberImage.Freeze();
            player.Character = null;
            player.Member = new Member(Camp.Sur) { Image = nextMemberImage };

            Assert.Same(nextMemberImage, player.PictureShown);
            Assert.Same(nextMemberImage, player.PictureShownWithFullCharacter);
            Assert.Same(nextMemberImage, player.PictureShownHeader);
            Assert.Contains(nameof(Player.PictureShown), changed);
            Assert.Contains(nameof(Player.PictureShownWithFullCharacter), changed);
            Assert.Contains(nameof(Player.PictureShownHeader), changed);
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
        Assert.Null(mapConfig.MapBorderNormalColor);
        Assert.Null(mapConfig.MapBorderBannedColor);

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
            Assert.Equal(Color.FromRgb(0x2B, 0x48, 0x3B), Assert.IsType<SolidColorBrush>(presenter.MapBorderNormalBrush).Color);
            Assert.Equal(Color.FromRgb(0x9C, 0x3E, 0x2F), Assert.IsType<SolidColorBrush>(presenter.MapBorderBannedBrush).Color);
        });
    }

    [Fact]
    public void MapV2DisplayBorderColorsRoundTripThroughLayoutJson()
    {
        var config = new FrontedCanvasConfig
        {
            CanvasWidth = 1440,
            CanvasHeight = 160,
            Controls =
            {
                ["Map"] = new MapV2DisplayControlConfig
                {
                    MapKey = "ArmsFactory",
                    MapBorderNormalColor = "#FF102030",
                    MapBorderBannedColor = "#FF405060"
                }
            }
        };

        var reloaded = JsonSerializer.Deserialize<FrontedCanvasConfig>(JsonSerializer.Serialize(config));
        var map = Assert.IsType<MapV2DisplayControlConfig>(reloaded!.Controls["Map"]);

        Assert.Equal("#FF102030", map.MapBorderNormalColor);
        Assert.Equal("#FF405060", map.MapBorderBannedColor);
    }

    [Fact]
    public void MapV2DisplayAppliesConfiguredBorderColorsAndUsesSpecificFallbacks()
    {
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
            settingsHostService.SetupGet(service => service.Settings).Returns(new Settings());
            var services = new ServiceCollection().AddSingleton(settingsHostService.Object).BuildServiceProvider();
            var control = new MapV2DisplayFrontedControl();

            var configured = Assert.IsAssignableFrom<Border>(control.Create(
                "Configured",
                new MapV2DisplayControlConfig
                {
                    MapKey = "ArmsFactory",
                    MapBorderNormalColor = "#FF102030",
                    MapBorderBannedColor = "#FF405060",
                    PickingBorderFillColor = "#FF708090"
                },
                CreateBuildContext(sharedDataService.Object, services)));
            var configuredPresenter = Assert.IsType<MapV2Presenter>(configured.Child);
            Assert.Equal(Color.FromRgb(0x10, 0x20, 0x30), Assert.IsType<SolidColorBrush>(configuredPresenter.MapBorderNormalBrush).Color);
            Assert.Equal(Color.FromRgb(0x40, 0x50, 0x60), Assert.IsType<SolidColorBrush>(configuredPresenter.MapBorderBannedBrush).Color);
            Assert.Equal(Color.FromRgb(0x70, 0x80, 0x90), Assert.IsType<SolidColorBrush>(configuredPresenter.PickingBorderBrush).Color);

            var invalid = Assert.IsAssignableFrom<Border>(control.Create(
                "Invalid",
                new MapV2DisplayControlConfig
                {
                    MapKey = "ArmsFactory",
                    MapBorderNormalColor = "invalid",
                    MapBorderBannedColor = "also-invalid"
                },
                CreateBuildContext(sharedDataService.Object, services)));
            var invalidPresenter = Assert.IsType<MapV2Presenter>(invalid.Child);
            Assert.Equal(Color.FromRgb(0x2B, 0x48, 0x3B), Assert.IsType<SolidColorBrush>(invalidPresenter.MapBorderNormalBrush).Color);
            Assert.Equal(Color.FromRgb(0x9C, 0x3E, 0x2F), Assert.IsType<SolidColorBrush>(invalidPresenter.MapBorderBannedBrush).Color);
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

    private static string ExtractTrailingIndex(string value)
    {
        var index = value.Length;
        while (index > 0 && char.IsDigit(value[index - 1]))
        {
            index--;
        }

        return value[index..];
    }

    private static FrontedControlBuildContext CreateBuildContext(
        ISharedDataService sharedDataService = null,
        IServiceProvider services = null,
        IFrontedResourceResolver resourceResolver = null)
    {
        return new FrontedControlBuildContext
        {
            Services = services ?? EmptyServiceProvider.Instance,
            SharedDataService = sharedDataService ?? new Mock<ISharedDataService>().Object,
            ResourceResolver = resourceResolver ?? NullFrontedResourceResolver.Instance,
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

            Assert.True(service.LoadOptions("WidgetsWindow").AllowTransparency);
            await service.SaveOptionsAsync(
                "WidgetsWindow",
                new FrontedWindowLayoutOptions
                {
                    AllowTransparency = false,
                    BackgroundColor = "#FF112233"
                },
                TestContext.Current.CancellationToken);

            var path = Path.Combine(root, "WidgetsWindow", "window.json");
            Assert.True(File.Exists(path));
            Assert.False(service.LoadOptions("WidgetsWindow").AllowTransparency);
            Assert.Equal("#FF112233", service.LoadOptions("WidgetsWindow").BackgroundColor);

            await service.ResetOptionsAsync("WidgetsWindow", TestContext.Current.CancellationToken);
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
        WpfTestThread.Run(action);
    }

    private static FrontedCanvasConfig ReadBuiltInLayout(string windowTypeName, string canvasName = "BaseCanvas")
    {
        var path = GetBuiltInLayoutPath(windowTypeName, canvasName);

        Assert.True(File.Exists(path), path);

        var config = JsonSerializer.Deserialize<FrontedWindowConfig>(File.ReadAllText(path))?.ToCanvasConfig();
        Assert.NotNull(config);
        return config;
    }

    private static string GetBuiltInLayoutPath(string windowTypeName, string canvasName = "BaseCanvas") =>
        Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts", $"{windowTypeName}.json");

    private static TextFrontedControlConfig AssertTextBinding(
        FrontedCanvasConfig config,
        string controlName,
        string bindingPath)
    {
        var control = Assert.IsType<TextFrontedControlConfig>(config.Controls[controlName]);
        Assert.Equal(bindingPath, Assert.Single(control.TextBinding!.Sources).Path);
        return control;
    }

    private static FrontedTextBindingExpression CreateTextBinding(params string[] paths) => new()
    {
        Sources = paths.Select(path => new FrontedBindingSourceConfig { Path = path }).ToList()
    };

    private static ImageFrontedControlConfig AssertImageBinding(
        FrontedCanvasConfig config,
        string controlName,
        string bindingPath)
    {
        var control = Assert.IsAssignableFrom<ImageFrontedControlConfig>(config.Controls[controlName]);
        Assert.Equal(bindingPath, control.BindingPath);
        return control;
    }

    private static void AssertAnimationPart(
        FrameworkElement element,
        Guid parentGuid,
        string parentName,
        string partName)
    {
        Assert.True(FrontedRendererProperties.GetIsGeneratedControl(element));
        Assert.True(FrontedRendererProperties.GetIsAnimationAuxiliaryElement(element));
        Assert.Equal(parentGuid, FrontedRendererProperties.GetParentBehaviorGuid(element));
        Assert.Equal(parentName, FrontedRendererProperties.GetParentRegisteredName(element));
        Assert.Equal(partName, FrontedRendererProperties.GetAnimationPartName(element));
        Assert.False(string.IsNullOrWhiteSpace(FrontedRendererProperties.GetRegisteredName(element)));
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

        public ImageSource ResolveImage(
            string path,
            FrontedImagePurpose purpose = FrontedImagePurpose.PackageResource) => null;
    }

    private sealed class RecordingFrontedResourceResolver : IFrontedResourceResolver
    {
        public ImageSource ImageSource { get; init; }

        public bool ThrowOnResolveImage { get; init; }

        public string LastResolvedImagePath { get; private set; }

        public FrontedImagePurpose? LastPurpose { get; private set; }

        public string ResolveImagePath(string path) => null;

        public ImageSource ResolveImage(
            string path,
            FrontedImagePurpose purpose = FrontedImagePurpose.PackageResource)
        {
            if (ThrowOnResolveImage)
            {
                throw new InvalidOperationException("ResolveImage should not be called.");
            }

            LastResolvedImagePath = path;
            LastPurpose = purpose;
            return ImageSource;
        }
    }
}
