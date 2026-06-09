#nullable enable

using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedWindowConfigTest
{
    [Fact]
    public void DefaultsUseWindowCentricSchema()
    {
        var config = new FrontedWindowConfig();

        Assert.Equal(3, config.Version);
        Assert.Equal(1440, config.WindowSettings.WindowWidth);
        Assert.Equal(810, config.WindowSettings.WindowHeight);
        Assert.Equal("#00000000", config.WindowSettings.BackgroundColor);
        Assert.Equal(Stretch.Fill, config.WindowSettings.ViewboxStretch);
        Assert.Equal(1440, config.CanvasSettings.CanvasWidth);
        Assert.Equal(810, config.CanvasSettings.CanvasHeight);
        Assert.Null(config.CanvasSettings.BackgroundImage);
        Assert.False(config.CanvasSettings.EnableBoModeStates);
        Assert.Empty(config.CanvasSettings.BoModeStates);
        Assert.Empty(config.ControlLayout.RequiredPlugins);
        Assert.Empty(config.ControlLayout.Controls);
    }

    [Fact]
    public void RoundTripWritesViewboxStretchAsString()
    {
        var config = new FrontedWindowConfig
        {
            WindowSettings =
            {
                WindowWidth = 1280,
                WindowHeight = 720,
                AllowsTransparency = true,
                BackgroundColor = "#11223344",
                Topmost = true,
                ViewboxStretch = Stretch.Uniform
            },
            CanvasSettings =
            {
                CanvasWidth = 1920,
                CanvasHeight = 1080,
                BackgroundImage = "Resources/bg.png"
            }
        };

        var json = JsonSerializer.Serialize(config);
        var root = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("Uniform", root["WindowSettings"]!["ViewboxStretch"]!.GetValue<string>());
        Assert.Equal("#11223344", root["WindowSettings"]!["BackgroundColor"]!.GetValue<string>());
        Assert.False(root["CanvasSettings"]!.AsObject().ContainsKey("BackgroundColor"));

        var roundTrip = JsonSerializer.Deserialize<FrontedWindowConfig>(json)!;
        Assert.Equal(Stretch.Uniform, roundTrip.WindowSettings.ViewboxStretch);
        Assert.Equal(1920, roundTrip.CanvasSettings.CanvasWidth);
        Assert.Equal("Resources/bg.png", roundTrip.CanvasSettings.BackgroundImage);
    }

    [Fact]
    public void SyncWindowSizeToCanvasCopiesPositiveFiniteCanvasSize()
    {
        var config = new FrontedWindowConfig
        {
            WindowSettings =
            {
                WindowWidth = 1,
                WindowHeight = 2
            },
            CanvasSettings =
            {
                CanvasWidth = 1536,
                CanvasHeight = 864
            }
        };

        config.SyncWindowSizeToCanvas();

        Assert.Equal(1536, config.WindowSettings.WindowWidth);
        Assert.Equal(864, config.WindowSettings.WindowHeight);
    }

    [Fact]
    public async Task UserStoreSaveAndLoadKeepWindowSizeFollowingCanvas()
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-bpsys-window-config-{Guid.NewGuid():N}");
        try
        {
            var store = new FrontedUserLayoutStore(root);
            var config = new FrontedWindowConfig
            {
                WindowSettings =
                {
                    WindowWidth = 1,
                    WindowHeight = 2
                },
                CanvasSettings =
                {
                    CanvasWidth = 1600,
                    CanvasHeight = 900
                }
            };

            await store.SaveAsync("BpWindow", config);

            var json = await File.ReadAllTextAsync(store.GetLayoutPath("BpWindow"));
            var saved = JsonSerializer.Deserialize<FrontedWindowConfig>(json)!;
            Assert.Equal(1600, saved.WindowSettings.WindowWidth);
            Assert.Equal(900, saved.WindowSettings.WindowHeight);

            var loaded = await store.LoadAsync("BpWindow");
            Assert.NotNull(loaded);
            Assert.Equal(1600, loaded.WindowSettings.WindowWidth);
            Assert.Equal(900, loaded.WindowSettings.WindowHeight);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
