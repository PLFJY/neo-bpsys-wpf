using Microsoft.Extensions.Logging.Abstractions;
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
        Assert.True(image.PickingBorder);
        Assert.Equal("Resources/pickingBorder.png", image.PickingBorderImagePath);
        Assert.True(image.BanLockAvailable);
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
}
