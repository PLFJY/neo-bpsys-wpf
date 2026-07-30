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
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedCanvasConfigTest
{
    [Fact]
    public void FrontedControlConfigBase_DoesNotExposeOrSerializeAnimationParts()
    {
        var config = new FrontedCanvasConfig
        {
            Controls =
            {
                ["SurPick0"] = new ImageFrontedControlConfig
                {
                    BehaviorGuid = Guid.NewGuid()
                }
            }
        };

        Assert.Null(typeof(FrontedControlConfigBase).GetProperty("AnimationParts"));
        Assert.DoesNotContain("AnimationParts", JsonSerializer.Serialize(config), StringComparison.Ordinal);
    }

    [Fact]
    public void BehaviorDocument_AnimationPartsJsonRoundTrip_PreservesGenericConfiguration()
    {
        var document = new FrontedBehaviorDocument
        {
            ControlBehaviorSets =
            [
                new ControlBehaviorSet
                {
                    BehaviorGuid = Guid.NewGuid(),
                    DisplayName = "SurPick0",
                    AnimationParts =
                    [
                        new FrontedAnimationPartConfig
                        {
                            Name = "wipeBar",
                            Kind = FrontedAnimationPartKind.Border,
                            Layer = FrontedAnimationPartLayer.BelowContent,
                            WidthText = "100%",
                            Height = 4,
                            Fill = "#FFFFFFFF",
                            Visibility = "Hidden"
                        }
                    ]
                }
            ]
        };

        var roundTrip = JsonSerializer.Deserialize<FrontedBehaviorDocument>(JsonSerializer.Serialize(document));
        var AnimationPart = Assert.Single(roundTrip!.ControlBehaviorSets.Single().AnimationParts);
        Assert.Equal("wipeBar", AnimationPart.Name);
        Assert.Equal(FrontedAnimationPartKind.Border, AnimationPart.Kind);
        Assert.Equal(FrontedAnimationPartLayer.BelowContent, AnimationPart.Layer);
    }

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

        var text = Assert.IsType<TextFrontedControlConfig>(config.Controls["SurTeamName"]);
        Assert.Equal("CurrentGame.SurTeam.Name", Assert.Single(text.TextBinding!.Sources).Path);
        Assert.Equal("Ignored when TextBinding has sources", text.Text);
        Assert.Equal("{0}%", text.TextBinding.StringFormat);

        var image = Assert.IsType<ImageFrontedControlConfig>(config.Controls["SurPick1"]);
        Assert.Equal("Resources/static.png", image.ImagePath);
        Assert.Equal(ImageSizingMode.OverflowCrop, image.SizingMode);
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
        Assert.Equal(ImageSizingMode.OverflowCrop, image.SizingMode);
    }

    /// <summary>
    /// 未声明覆盖层拉伸字段时，应默认跟随主图片的拉伸方式。
    /// </summary>
    [Fact]
    public void ImageOverlayStretchDefaultsFollowPrimaryImage()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "Logo": {
                "ControlType": "Image",
                "Left": 10,
                "Top": 20
              },
              "Pick": {
                "ControlType": "BorderedImage",
                "Left": 30,
                "Top": 40
              }
            }
            """);

        Assert.NotNull(config);
        foreach (var image in new ImageFrontedControlConfig[]
                 {
                     Assert.IsType<ImageFrontedControlConfig>(config.Controls["Logo"]),
                     Assert.IsType<BorderedImageFrontedControlConfig>(config.Controls["Pick"])
                 })
        {
            Assert.False(image.UseIndependentLockStretch);
            Assert.Equal("UniformToFill", image.LockStretch);
            Assert.False(image.UseIndependentPickingBorderStretch);
            Assert.Equal("UniformToFill", image.PickingBorderStretch);
        }
    }

    /// <summary>
    /// 图片覆盖层的独立拉伸开关和枚举值应按 JSON 契约读取。
    /// </summary>
    [Fact]
    public void ReadsImageOverlayStretchSettings()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "Logo": {
                "ControlType": "Image",
                "Left": 10,
                "Top": 20,
                "UseIndependentLockStretch": false,
                "LockStretch": "UniformToFill",
                "UseIndependentPickingBorderStretch": true,
                "PickingBorderStretch": "None"
              }
            }
            """);

        Assert.NotNull(config);
        var image = Assert.IsType<ImageFrontedControlConfig>(config.Controls["Logo"]);
        Assert.False(image.UseIndependentLockStretch);
        Assert.Equal("UniformToFill", image.LockStretch);
        Assert.True(image.UseIndependentPickingBorderStretch);
        Assert.Equal("None", image.PickingBorderStretch);
    }

    /// <summary>
    /// Picking Border 的运行时名称由控件名自动生成，不应作为布局配置字段暴露。
    /// </summary>
    [Fact]
    public void ImageConfigDoesNotExposePickingBorderName()
    {
        Assert.Null(typeof(ImageFrontedControlConfig).GetProperty("PickingBorderName"));
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
                "CampIconColor": "Black",
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
                    "ShowCampIcon": false,
                    "CampIconColor": "White"
                  }
                ]
              }
            }
            """);

        Assert.NotNull(config);
        var row = Assert.IsType<GlobalScoreRowControlConfig>(config.Controls["MainGlobalScoreRow"]);
        Assert.Equal("GlobalScoreRow", row.ControlType);
        Assert.Equal(neo_bpsys_wpf.Core.Enums.TeamType.HomeTeam, row.TeamType);
        Assert.True(row.ShowCampIcon);
        Assert.Equal(GlobalScoreCampIconColor.Black, row.CampIconColor);
        var cell = Assert.Single(row.Cells);
        Assert.Equal("Game1FirstHalf", cell.Id);
        Assert.Equal(1, cell.GameNumber);
        Assert.Equal(ScoreGameKind.Normal, cell.GameKind);
        Assert.Equal(ScoreHalfKind.FirstHalf, cell.HalfKind);
        Assert.Equal(0, cell.X);
        Assert.False(cell.ShowCampIcon);
        Assert.Equal(GlobalScoreCampIconColor.White, cell.CampIconColor);
    }

    [Fact]
    public void GlobalScorePresenterFillsCampIconColorFromSourceAlpha()
    {
        WpfTestThread.Run(() =>
        {
            var source = CreateSinglePixelBitmap(alpha: 128);
            var presenter = new GlobalScorePresenter
            {
                CampIconColor = GlobalScoreCampIconColor.Black
            };
            presenter.Resources["scoreGlobal_surIcon"] = source;
            presenter.Resources["scoreGlobal_hunIcon"] = source;

            presenter.OnApplyTemplate();

            AssertPixel(presenter.TintedSurIcon, expectedRgb: 0, expectedAlpha: 128);

            presenter.CampIconColor = GlobalScoreCampIconColor.White;

            AssertPixel(presenter.TintedHunIcon, expectedRgb: 255, expectedAlpha: 128);
        });
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
                "Color": "#FF41A8FF",
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
        Assert.Equal("#FF41A8FF", talent.Color);

        var progress = Assert.IsType<GameProgressTextControlConfig>(config.Controls["GameProgress"]);
        Assert.Equal("GameProgressText", progress.ControlType);

        var mapName = Assert.IsType<MapNameTextControlConfig>(config.Controls["MapName"]);
        Assert.Equal("MapNameText", mapName.ControlType);
        Assert.Equal(string.Empty, mapName.EmptyText);
    }

    [Fact]
    public void TalentTraitDisplayDefaultsToWhiteColorOverlay()
    {
        var config = new TalentTraitDisplayControlConfig();

        Assert.Equal("#FFFFFFFF", config.Color);
    }

    private static BitmapSource CreateSinglePixelBitmap(byte alpha)
    {
        var pixels = new byte[] { 30, 20, 10, alpha };
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void AssertPixel(ImageSource source, byte expectedRgb, byte expectedAlpha)
    {
        var bitmap = Assert.IsAssignableFrom<BitmapSource>(source);
        var pixels = new byte[4];
        bitmap.CopyPixels(pixels, 4, 0);
        Assert.Equal(expectedRgb, pixels[0]);
        Assert.Equal(expectedRgb, pixels[1]);
        Assert.Equal(expectedRgb, pixels[2]);
        Assert.Equal(expectedAlpha, pixels[3]);
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
                CreateTestRegistry(),
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

            var host = Assert.IsType<FrontedV3ControlHost>(canvas.Children[0]);
            Assert.Same(host, window.FindName("GeneratedText"));

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
                new FrontedV3ControlRegistry([]),
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
                new FrontedV3ControlRegistry([]),
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
            var config = new TextFrontedControlConfig
            {
                Text = "Static title"
            };
            var control = new TextFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "Title"));

            var border = Assert.IsType<Border>(control.Content);
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
            var config = new TextFrontedControlConfig
            {
                TextBinding = CreateTextBinding("CurrentGame.SurTeam.Name"),
                Text = "Static title"
            };
            var control = new TextFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "Title", sharedDataService));

            var border = Assert.IsType<Border>(control.Content);
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
            var sharedDataService = new Mock<ISharedDataService>().Object;
            var config = new TextFrontedControlConfig
            {
                TextBinding = new FrontedTextBindingExpression
                {
                    Sources = [new FrontedBindingSourceConfig { Path = "CurrentGame.SurPlayerList[0].Data.DecodingProgress" }],
                    StringFormat = "{0}%"
                }
            };
            var control = new TextFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "DecodingProgress", sharedDataService));

            var border = Assert.IsType<Border>(control.Content);
            var textBlock = Assert.IsType<TextBlock>(border.Child);
            var binding = BindingOperations.GetMultiBinding(textBlock, TextBlock.TextProperty);
            Assert.NotNull(binding);
            Assert.Equal("{0}%", Assert.IsType<FrontedTextBindingExpression>(binding.ConverterParameter).StringFormat);

            var staticConfig = new TextFrontedControlConfig
            {
                Text = "Static title"
            };
            var staticControl = new TextFrontedControl();
            staticControl.InitializeFrontedV3(CreateV3Context(staticConfig, "Title"));

            var staticBorder = Assert.IsType<Border>(staticControl.Content);
            var staticTextBlock = Assert.IsType<TextBlock>(staticBorder.Child);
            Assert.Equal("Static title", staticTextBlock.Text);
            Assert.Null(BindingOperations.GetBinding(staticTextBlock, TextBlock.TextProperty));
        });
    }

    [Fact]
    public void ImageAndBorderedImageControlTypesAreStable()
    {
        Assert.Equal("Image", typeof(ImageFrontedControl).GetCustomAttribute<FrontedV3ControlAttribute>()!.ControlId);
        Assert.Equal("BorderedImage", typeof(BorderedImageFrontedControl).GetCustomAttribute<FrontedV3ControlAttribute>()!.ControlId);
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
                CreateTestRegistry(),
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

            var host = Assert.IsType<FrontedV3ControlHost>(Assert.Single(canvas.Children));
            Assert.IsType<TextFrontedControl>(host.Control);
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
                CreateTestRegistry(),
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
            var config = new ImageFrontedControlConfig
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
            };
            var control = new ImageFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "Logo"));

            var root = Assert.IsType<Grid>(control.Content);
            var image = FindPrimaryImage(root);
        });
    }

    [Fact]
    public void FrontedRendererRendersAnimationPartInsideParentControl()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var renderer = new FrontedRenderer(
                EmptyServiceProvider.Instance,
                new Mock<ISharedDataService>().Object,
                NullFrontedResourceResolver.Instance,
                CreateTestRegistry(),
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
                        ["SurPick0"] = new TextFrontedControlConfig
                        {
                            BehaviorGuid = guid,
                            Text = "Pick",
                            Width = 200,
                            Height = 80,
                        }
                    }
                },
                new FrontedRenderContext { WindowId = "BpWindow", CanvasName = "BaseCanvas" });

            new FrontedBehaviorAnimationPartRenderer(NullFrontedResourceResolver.Instance).ApplyAnimationParts(
                canvas,
                new FrontedBehaviorDocument
                {
                    ControlBehaviorSets =
                    [
                        new ControlBehaviorSet
                        {
                            BehaviorGuid = guid,
                            DisplayName = "SurPick0",
                            AnimationParts =
                            [
                                new FrontedAnimationPartConfig
                                {
                                    Name = "wipeBar",
                                    Kind = FrontedAnimationPartKind.Rectangle,
                                    Layer = FrontedAnimationPartLayer.AboveContent,
                                    Width = 4,
                                    HeightText = "100%",
                                    Fill = "#FFFFFFFF"
                                }
                            ]
                        }
                    ]
                });

            var parent = Assert.IsType<Grid>(Assert.Single(canvas.Children));
            var part = FindDescendants<FrameworkElement>(parent)
                .Single(item => FrontedRendererProperties.GetAnimationPartName(item) == "wipeBar");
            Assert.Same(parent, FrontedRendererProperties.GetAnimationPartParent(part));
            Assert.Equal("SurPick0__wipeBar", FrontedRendererProperties.GetRegisteredName(part));
            Assert.DoesNotContain(canvas.Children.OfType<FrameworkElement>(), item => ReferenceEquals(item, part));

            var resolved = new FrontedAnimationTargetResolver().Resolve(
                FrontedAnimationTargetReference.Parse($"part:{guid}:wipeBar"),
                new FrontedAnimationExecutionContext { Root = canvas, SelfBehaviorGuid = guid });
            Assert.Same(part, resolved!.Element);
        });
    }

    [Fact]
    public void AnimationPartRenderer_AppliesGlowEffect()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var canvas = RenderTextControlWithBehaviorGuid(guid);

            new FrontedBehaviorAnimationPartRenderer(NullFrontedResourceResolver.Instance).ApplyAnimationParts(
                canvas,
                CreateAnimationPartDocument(
                    guid,
                    new FrontedAnimationPartConfig
                    {
                        Name = "Swipe",
                        Kind = FrontedAnimationPartKind.Rectangle,
                        Fill = "#FFFFFFFF",
                        Effect = new FrontedVisualEffectConfig
                        {
                            Kind = FrontedVisualEffectKind.Glow,
                            Color = "#67E8F9",
                            Opacity = 1,
                            BlurRadius = 18,
                            ShadowDepth = 12
                        }
                    }));

            var parent = Assert.IsType<Grid>(Assert.Single(canvas.Children));
            var part = FindDescendants<FrameworkElement>(parent)
                .Single(item => FrontedRendererProperties.GetAnimationPartName(item) == "Swipe");
            var effect = Assert.IsType<DropShadowEffect>(part.Effect);
            Assert.Equal(0, effect.ShadowDepth);
        });
    }

    [Fact]
    public void AnimationPartRenderer_NoneEffectClearsEffect()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var canvas = RenderTextControlWithBehaviorGuid(guid);

            new FrontedBehaviorAnimationPartRenderer(NullFrontedResourceResolver.Instance).ApplyAnimationParts(
                canvas,
                CreateAnimationPartDocument(
                    guid,
                    new FrontedAnimationPartConfig
                    {
                        Name = "Swipe",
                        Kind = FrontedAnimationPartKind.Rectangle,
                        Effect = new FrontedVisualEffectConfig
                        {
                            Kind = FrontedVisualEffectKind.None
                        }
                    }));

            var parent = Assert.IsType<Grid>(Assert.Single(canvas.Children));
            var part = FindDescendants<FrameworkElement>(parent)
                .Single(item => FrontedRendererProperties.GetAnimationPartName(item) == "Swipe");
            Assert.Null(part.Effect);
        });
    }

    [Fact]
    public void ImageFrontedControlMarksGeneratedAnimationParts()
    {
        RunOnStaThread(() =>
        {
            var guid = Guid.NewGuid();
            var config = new ImageFrontedControlConfig
            {
                BehaviorGuid = guid,
                Lockable = true,
                PickingBorderAvailable = true
            };
            var control = new ImageFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "BanSlot"));

            var root = Assert.IsType<Grid>(control.Content);
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
            var config = new ImageFrontedControlConfig
            {
                BehaviorGuid = Guid.NewGuid(),
                Lockable = false,
                PickingBorderAvailable = false
            };
            var control = new ImageFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "BanSlot"));

            var root = Assert.IsType<Grid>(control.Content);
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
            var config = new ImageFrontedControlConfig
            {
                BindingPath = "CurrentGame.PickedMapImage",
                ImagePath = "Resources/static.png",
                Stretch = "UniformToFill"
            };
            var control = new ImageFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "Pick", sharedDataService, resourceResolver: resolver));

            var root = Assert.IsType<Grid>(control.Content);
            var image = FindPrimaryImage(root);
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
            var config = new ImageFrontedControlConfig
            {
                ImagePath = "Resources/logo.png"
            };
            var control = new ImageFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "Logo", resourceResolver: resolver));

            var root = Assert.IsType<Grid>(control.Content);
            var image = FindPrimaryImage(root);
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
            var config = new BorderedImageFrontedControlConfig
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
            };
            var control = new BorderedImageFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "Header"));

            var border = Assert.IsType<Border>(control.Content);

            var grid = Assert.IsType<Grid>(border.Child);
            var image = FindPrimaryImage(grid);
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
            var config = new BorderedImageFrontedControlConfig
            {
                ImagePath = "Resources/pick.png"
            };
            var control = new BorderedImageFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "Pick", resourceResolver: resolver));

            var border = Assert.IsType<Border>(control.Content);
            var grid = Assert.IsType<Grid>(border.Child);
            var image = FindPrimaryImage(grid);
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
            var config = new BorderedImageFrontedControlConfig
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
            };
            var control = new BorderedImageFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "SurPick0", sharedDataService, resourceResolver: resolver));

            var border = Assert.IsType<Border>(control.Content);
            Assert.Equal("SurPick0", border.Name);

            var grid = Assert.IsType<Grid>(border.Child);
            var image = FindPrimaryImage(grid);

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
            var config = new ImageFrontedControlConfig
            {
                ImagePath = "Resources/missing.png"
            };
            var control = new ImageFrontedControl();
            control.InitializeFrontedV3(CreateV3Context(config, "Missing", resourceResolver: new RecordingFrontedResourceResolver()));

            var root = Assert.IsType<Grid>(control.Content);
            var image = FindPrimaryImage(root);
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
            control.InitializeFrontedV3(CreateV3Context(mapConfig, "Arms_Factory", sharedDataService.Object, serviceProvider));

            var border = Assert.IsAssignableFrom<Border>(control.Content);
            var presenter = Assert.IsType<MapV2Presenter>(border.Child);

            var widthBinding = BindingOperations.GetBinding(presenter, FrameworkElement.WidthProperty);
            var heightBinding = BindingOperations.GetBinding(presenter, FrameworkElement.HeightProperty);
            Assert.NotNull(widthBinding);
            Assert.NotNull(heightBinding);
            Assert.Same(border, widthBinding.Source);
            Assert.Same(border, heightBinding.Source);
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

            var configuredConfig = new MapV2DisplayControlConfig
            {
                MapKey = "ArmsFactory",
                MapBorderNormalColor = "#FF102030",
                MapBorderBannedColor = "#FF405060",
                PickingBorderFillColor = "#FF708090"
            };
            control.InitializeFrontedV3(CreateV3Context(configuredConfig, "Configured", sharedDataService.Object, services));
            var configured = Assert.IsAssignableFrom<Border>(control.Content);
            var configuredPresenter = Assert.IsType<MapV2Presenter>(configured.Child);

            var invalidConfig = new MapV2DisplayControlConfig
            {
                MapKey = "ArmsFactory",
                MapBorderNormalColor = "invalid",
                MapBorderBannedColor = "also-invalid"
            };
            control.InitializeFrontedV3(CreateV3Context(invalidConfig, "Invalid", sharedDataService.Object, services));
            var invalid = Assert.IsAssignableFrom<Border>(control.Content);
            var invalidPresenter = Assert.IsType<MapV2Presenter>(invalid.Child);
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

    private static Canvas RenderTextControlWithBehaviorGuid(Guid guid)
    {
        var renderer = new FrontedRenderer(
            EmptyServiceProvider.Instance,
            new Mock<ISharedDataService>().Object,
            NullFrontedResourceResolver.Instance,
            CreateTestRegistry(),
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
                    ["SurPick0"] = new TextFrontedControlConfig
                    {
                        BehaviorGuid = guid,
                        Text = "Pick",
                        Width = 200,
                        Height = 80
                    }
                }
            },
            new FrontedRenderContext { WindowId = "BpWindow", CanvasName = "BaseCanvas" });

        return canvas;
    }

    private static FrontedBehaviorDocument CreateAnimationPartDocument(
        Guid guid,
        FrontedAnimationPartConfig part) =>
        new()
        {
            ControlBehaviorSets =
            [
                new ControlBehaviorSet
                {
                    BehaviorGuid = guid,
                    DisplayName = "SurPick0",
                    AnimationParts = [part]
                }
            ]
        };

    private static FrontedV3ControlContext CreateV3Context(
        FrontedControlConfigBase config,
        string controlName = null,
        ISharedDataService sharedDataService = null,
        IServiceProvider services = null,
        IFrontedResourceResolver resourceResolver = null)
    {
        return new FrontedV3ControlContext
        {
            Services = services ?? EmptyServiceProvider.Instance,
            SharedDataService = sharedDataService ?? new Mock<ISharedDataService>().Object,
            ResourceResolver = resourceResolver ?? NullFrontedResourceResolver.Instance,
            WindowId = "TestWindow",
            CanvasName = "BaseCanvas",
            Config = config,
            ControlName = controlName,
            Logger = NullLogger.Instance
        };
    }

    private static FrontedV3ControlRegistry CreateTestRegistry()
    {
        return new FrontedV3ControlRegistry([new FrontedV3ControlRegistration
        {
            CanonicalControlType = "Text",
            LocalControlId = "Text",
            PackageId = "builtin",
            IsBuiltIn = true,
            ControlType = typeof(TextFrontedControl),
            ConfigType = typeof(TextFrontedControlConfig),
            Properties = Array.Empty<FrontedV3PropertyDefinition>(),
            CreateDefaultConfig = () => new TextFrontedControlConfig()
        }]);
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

    private static string GetBuiltInLayoutPath(string windowTypeName, string canvasName = "BaseCanvas") =>
        Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts", $"{windowTypeName}.json");

    private static FrontedTextBindingExpression CreateTextBinding(params string[] paths) => new()
    {
        Sources = paths.Select(path => new FrontedBindingSourceConfig { Path = path }).ToList()
    };

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

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static Image FindPrimaryImage(DependencyObject root)
    {
        var primaryContent = Assert.Single(
            FindDescendants<FrameworkElement>(root),
            FrontedRendererProperties.GetIsPrimaryContentElement);
        return Assert.IsType<Image>(Assert.Single(FindDescendants<Image>(primaryContent)));
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
