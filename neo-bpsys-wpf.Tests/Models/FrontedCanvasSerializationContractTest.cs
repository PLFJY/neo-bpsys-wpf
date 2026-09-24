using System;
using System.Text.Json;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

/// <summary>
/// Verifies the durable JSON contract for v3 fronted layouts and behavior documents.
/// </summary>
public sealed class FrontedCanvasSerializationContractTest
{
    /// <summary>
    /// Verifies that generic animation-part configuration survives behavior-document persistence.
    /// </summary>
    [Fact]
    public void BehaviorDocumentRoundTripPreservesAnimationParts()
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

        var part = Assert.Single(roundTrip!.ControlBehaviorSets[0].AnimationParts);
        Assert.Equal("wipeBar", part.Name);
        Assert.Equal(FrontedAnimationPartKind.Border, part.Kind);
        Assert.Equal(FrontedAnimationPartLayer.BelowContent, part.Layer);
    }

    /// <summary>
    /// Verifies that behavior identity persists and its empty legacy-compatible default is omitted.
    /// </summary>
    [Fact]
    public void BehaviorGuidRoundTripsAndEmptyValueIsOmitted()
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
                    Text = "Hello",
                    BehaviorGuid = behaviorGuid
                },
                ["LegacyTitle"] = new TextFrontedControlConfig { BehaviorGuid = Guid.Empty }
            }
        };

        var json = JsonSerializer.Serialize(config);
        var roundTrip = JsonSerializer.Deserialize<FrontedCanvasConfig>(json);

        Assert.Equal(behaviorGuid, roundTrip!.Controls["Title"].BehaviorGuid);
        Assert.Equal(Guid.Empty, roundTrip.Controls["LegacyTitle"].BehaviorGuid);
        Assert.Equal(1, json.Split(nameof(FrontedControlConfigBase.BehaviorGuid)).Length - 1);
    }

    /// <summary>
    /// Verifies that BO-mode overrides retain their stable root-level shape.
    /// </summary>
    [Fact]
    public void BoModeStatesRoundTripAtCanvasRoot()
    {
        const string json =
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
                    "Title": { "ControlType": "Text", "Visibility": "Hidden" }
                  }
                }
              }
            }
            """;

        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(json)!;
        var serialized = JsonSerializer.Serialize(config);

        Assert.True(config.EnableBoModeStates);
        Assert.Equal("Resources/scoreGlobalBo3.png", config.BoModeStates["Bo3"].BackgroundImage);
        Assert.Equal(FrontedControlVisibility.Hidden, config.BoModeStates["Bo3"].Controls["Title"].Visibility);
        Assert.Contains("BoModeStates", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("ScoreGlobal.Bo3", serialized, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies representative built-in controls in the root-level v3 JSON shape.
    /// </summary>
    [Fact]
    public void RootLevelControlsDeserializeToTheirConcreteTypes()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "Title": {
                "ControlType": "Text",
                "Text": "Static title",
                "TextBinding": {
                  "Sources": [{ "Path": "CurrentGame.SurTeam.Name" }],
                  "StringFormat": "{0}%"
                }
              },
              "Pick": {
                "ControlType": "BorderedImage",
                "BindingPath": "CurrentGame.SurPlayerList[0].PictureShown",
                "ImagePath": "Resources/static-pick.png",
                "SizingMode": "OverflowCrop"
              }
            }
            """)!;

        var text = Assert.IsType<TextFrontedControlConfig>(config.Controls["Title"]);
        Assert.Equal("CurrentGame.SurTeam.Name", Assert.Single(text.TextBinding!.Sources).Path);
        Assert.Equal("{0}%", text.TextBinding.StringFormat);
        var image = Assert.IsType<BorderedImageFrontedControlConfig>(config.Controls["Pick"]);
        Assert.Equal(ImageSizingMode.OverflowCrop, image.SizingMode);
        Assert.Equal("Resources/static-pick.png", image.ImagePath);
    }

    /// <summary>
    /// Verifies stable string values for the image sizing contract.
    /// </summary>
    /// <param name="jsonValue">The persisted value.</param>
    /// <param name="expected">The expected model value.</param>
    [Theory]
    [InlineData("Auto", ImageSizingMode.Auto)]
    [InlineData("FillContainer", ImageSizingMode.FillContainer)]
    [InlineData("OverflowCrop", ImageSizingMode.OverflowCrop)]
    public void ImageSizingModeStringValuesRemainReadable(string jsonValue, ImageSizingMode expected)
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            $$"""
            {
              "Version": 3,
              "CanvasWidth": 100,
              "CanvasHeight": 100,
              "Logo": { "ControlType": "Image", "SizingMode": "{{jsonValue}}" }
            }
            """)!;

        Assert.Equal(expected, Assert.IsType<ImageFrontedControlConfig>(config.Controls["Logo"]).SizingMode);
    }

    /// <summary>
    /// Verifies canonical plugin control identity parsing.
    /// </summary>
    [Fact]
    public void PluginControlIdentityRoundTrips()
    {
        var parsed = FrontedPluginControlType.Parse("plugin:top.plfjy.example.fronted/TeamCard");

        Assert.Equal("top.plfjy.example.fronted", parsed.PackageId);
        Assert.Equal("TeamCard", parsed.ControlTypeName);
        Assert.Equal("plugin:top.plfjy.example.fronted/TeamCard", parsed.ToString());
    }

    /// <summary>
    /// Verifies representative malformed plugin identities are rejected.
    /// </summary>
    /// <param name="controlType">The malformed identity.</param>
    [Theory]
    [InlineData("plugin:TeamCard")]
    [InlineData("plugin:/TeamCard")]
    [InlineData("plugin:top.plfjy.example.fronted/")]
    [InlineData("plugin:top plfjy/TeamCard")]
    public void MalformedPluginControlIdentityIsRejected(string controlType)
    {
        Assert.False(FrontedPluginControlType.TryParse(controlType, out _));
        Assert.Throws<FrontedLayoutConfigException>(() => FrontedPluginControlType.Parse(controlType));
    }

    /// <summary>
    /// Verifies required plugin metadata remains separate from the control dictionary.
    /// </summary>
    [Fact]
    public void RequiredPluginsRoundTripAsReservedRootMetadata()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "RequiredPlugins": [{
                "PackageId": "top.plfjy.example.fronted",
                "MinVersion": "1.0.0",
                "Controls": ["plugin:top.plfjy.example.fronted/TeamCard"]
              }],
              "Title": { "ControlType": "Text", "Text": "Title" }
            }
            """)!;

        var serialized = JsonSerializer.Serialize(config);
        var roundTrip = JsonSerializer.Deserialize<FrontedCanvasConfig>(serialized)!;

        Assert.Equal("top.plfjy.example.fronted", Assert.Single(roundTrip.RequiredPlugins).PackageId);
        Assert.False(roundTrip.Controls.ContainsKey("RequiredPlugins"));
        Assert.True(roundTrip.Controls.ContainsKey("Title"));
    }

    /// <summary>
    /// Verifies plugin-defined fields survive an import/export round trip.
    /// </summary>
    [Fact]
    public void PluginControlExtensionDataRoundTrips()
    {
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "TeamCard1": {
                "ControlType": "plugin:top.plfjy.example.fronted/TeamCard",
                "TeamNameBindingPath": "CurrentGame.HomeTeam.Name",
                "AccentColor": "#FFFFFFFF"
              }
            }
            """)!;

        var serialized = JsonSerializer.Serialize(config);
        var roundTrip = JsonSerializer.Deserialize<FrontedCanvasConfig>(serialized)!;
        var plugin = Assert.IsType<PluginFrontedControlConfig>(roundTrip.Controls["TeamCard1"]);

        Assert.Equal("top.plfjy.example.fronted", plugin.PackageId);
        Assert.Equal("CurrentGame.HomeTeam.Name", plugin.ExtensionData["TeamNameBindingPath"].GetString());
        Assert.Equal("#FFFFFFFF", plugin.ExtensionData["AccentColor"].GetString());
    }

    /// <summary>
    /// Verifies unknown built-in-like control types still fail instead of being guessed as plugins.
    /// </summary>
    [Fact]
    public void UnknownBuiltInControlIsRejectedWithItsIdentity()
    {
        var exception = Assert.Throws<FrontedLayoutConfigException>(() =>
            JsonSerializer.Deserialize<FrontedCanvasConfig>(
                """
                {
                  "Version": 3,
                  "CanvasWidth": 100,
                  "CanvasHeight": 100,
                  "UnknownControl": { "ControlType": "Video" }
                }
                """));

        Assert.Contains("UnknownControl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Video", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies numeric layout fields do not silently accept JSON strings.
    /// </summary>
    [Fact]
    public void NumericFieldsRejectJsonStrings()
    {
        Assert.Throws<FrontedLayoutConfigException>(() =>
            JsonSerializer.Deserialize<FrontedCanvasConfig>(
                """
                { "Version": 3, "CanvasWidth": "1440", "CanvasHeight": 810 }
                """));
    }

    /// <summary>
    /// Verifies controls serialize at the canvas root rather than under a new wrapper field.
    /// </summary>
    [Fact]
    public void ControlsSerializeAtCanvasRoot()
    {
        var config = new FrontedCanvasConfig
        {
            CanvasWidth = 1440,
            CanvasHeight = 810,
            Controls = { ["Title"] = new TextFrontedControlConfig { Text = "Static title" } }
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(config));

        Assert.Equal("Text", document.RootElement.GetProperty("Title").GetProperty("ControlType").GetString());
        Assert.False(document.RootElement.TryGetProperty("Controls", out _));
    }
}
