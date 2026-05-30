using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.IO;
using System.Text.Json;
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
        Assert.Equal("Bold", text.FontWeight);
        Assert.Equal(28, text.FontSize);
        Assert.Equal(2, text.ZIndex);

        var image = Assert.IsType<ImageFrontedControlConfig>(config.Controls["SurPick1"]);
        Assert.Equal(143, image.Left);
        Assert.Equal(160, image.Height);
        Assert.True(image.CornerRadius.HasValue);
        Assert.Equal(8, image.CornerRadius.Value);
        Assert.True(image.PickingBorder);
        Assert.Equal("Resources/pickingBorder.png", image.PickingBorderImagePath);
        Assert.True(image.BanLockAvailable);
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
                    FontSize = 18
                }
            }
        };

        var json = JsonSerializer.Serialize(config);
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("Title", out var title));
        Assert.Equal("Text", title.GetProperty("ControlType").GetString());
        Assert.False(document.RootElement.TryGetProperty("Controls", out _));
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

    private static FrontedCanvasConfig ReadBuiltInLayout(string windowTypeName)
    {
        var path = Path.Combine(
            AppConstants.ResourcesPath,
            "FrontedLayouts",
            windowTypeName,
            "BaseCanvas.json");

        Assert.True(File.Exists(path), path);

        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(File.ReadAllText(path));
        Assert.NotNull(config);
        return config;
    }

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
}
