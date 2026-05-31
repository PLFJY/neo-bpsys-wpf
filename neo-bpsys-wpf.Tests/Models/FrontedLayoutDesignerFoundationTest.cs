#nullable enable

using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedLayoutDesignerFoundationTest
{
    [Fact]
    public void FromConfigCreatesDesignItemsUsingDictionaryKeysAsName()
    {
        var config = new FrontedCanvasConfig
        {
            CanvasWidth = 100,
            CanvasHeight = 50,
            Controls =
            {
                ["Title"] = new TextFrontedControlConfig { Text = "BP display tool" },
                ["SurPick0"] = new ImageFrontedControlConfig { BindingPath = "CurrentGame.SurPlayerList[0].PictureShown" }
            }
        };

        var document = new FrontedLayoutDesignConverter().FromConfig(
            "BpWindow",
            "BaseCanvas",
            config,
            new FrontedLayoutRuntimeContractCatalog());

        Assert.Equal(["Title", "SurPick0"], document.Controls.Select(item => item.Name));
        Assert.False(document.Controls.Single(item => item.Name == "Title").IsRuntimeCritical);
        Assert.True(document.Controls.Single(item => item.Name == "SurPick0").IsRuntimeCritical);
    }

    [Fact]
    public void ToConfigWritesDesignItemNameBackAsDictionaryKeyWithoutAddingNameToConfig()
    {
        var document = new FrontedCanvasDesignDocument
        {
            WindowTypeName = "TestWindow",
            CanvasName = "BaseCanvas",
            CanvasConfig = new FrontedCanvasConfig
            {
                Version = 3,
                CanvasWidth = 1440,
                CanvasHeight = 810,
                BackgroundImage = "Resources/bp.png"
            },
            Controls =
            [
                new FrontedControlDesignItem
                {
                    Name = "StaticTitle",
                    Config = new TextFrontedControlConfig
                    {
                        Text = "Static title",
                        Left = 10,
                        Top = 20
                    }
                }
            ]
        };

        var config = new FrontedLayoutDesignConverter().ToConfig(document);
        var json = JsonSerializer.Serialize(config);
        using var jsonDocument = JsonDocument.Parse(json);

        Assert.Equal(3, config.Version);
        Assert.Equal(1440, config.CanvasWidth);
        Assert.Equal(810, config.CanvasHeight);
        Assert.Equal("Resources/bp.png", config.BackgroundImage);
        Assert.True(config.Controls.ContainsKey("StaticTitle"));
        Assert.False(jsonDocument.RootElement.GetProperty("StaticTitle").TryGetProperty("Name", out _));
        Assert.False(jsonDocument.RootElement.TryGetProperty("Controls", out _));
    }

    [Fact]
    public void RuntimeContractCatalogMarksBpWindowBaseCanvasCriticalNames()
    {
        var catalog = new FrontedLayoutRuntimeContractCatalog();

        Assert.True(catalog.IsRuntimeCritical("BpWindow", "BaseCanvas", "SurPick0"));
        Assert.True(catalog.IsRuntimeCritical("BpWindow", "BaseCanvas", "HunPickingBorder"));
        Assert.False(catalog.IsRuntimeCritical("ScoreSurWindow", "BaseCanvas", "SurPick0"));
    }

    [Fact]
    public void ValidatorAcceptsValidExistingBpWindowBaseCanvasLayout()
    {
        var config = ReadBuiltInLayout("BpWindow");
        var validator = CreateValidator();

        var messages = validator.Validate("BpWindow", "BaseCanvas", config);

        Assert.DoesNotContain(messages, message => message.Severity == FrontedLayoutValidationSeverity.Error);
    }

    [Fact]
    public void ValidatorErrorsOnDuplicateAndInvalidControlNamesAtDesignItemLevel()
    {
        var document = CreateDocument(
            [
                new FrontedControlDesignItem { Name = "Title", Config = new TextFrontedControlConfig { Text = "A" } },
                new FrontedControlDesignItem { Name = "Title", Config = new TextFrontedControlConfig { Text = "B" } },
                new FrontedControlDesignItem { Name = "Bad.Name", Config = new TextFrontedControlConfig { Text = "C" } }
            ]);

        var messages = CreateValidator().Validate(document);

        Assert.Contains(messages, message => message.Code == "ControlNameDuplicate");
        Assert.Contains(messages, message => message.Code == "ControlNameInvalid" && message.ControlName == "Bad.Name");
    }

    [Fact]
    public void ValidatorErrorsOnUnknownControlType()
    {
        var document = CreateDocument(
            [
                new FrontedControlDesignItem
                {
                    Name = "Video1",
                    Config = new FrontedControlConfigBase { ControlType = "Video" }
                }
            ]);

        var messages = CreateValidator().Validate(document);

        Assert.Contains(messages, message => message.Code == "ControlTypeUnknown" && message.ControlName == "Video1");
    }

    [Fact]
    public void ValidatorErrorsWhenPickingBorderOverlayTargetControlNameIsMissing()
    {
        var document = CreateDocument(
            [
                new FrontedControlDesignItem
                {
                    Name = "SurPickingBorder0",
                    Config = new PickingBorderOverlayControlConfig
                    {
                        TargetControlName = "SurPick0",
                        Width = 141,
                        Height = 160
                    }
                }
            ]);

        var messages = CreateValidator().Validate(document);

        Assert.Contains(
            messages,
            message => message.Code == "ReferenceTargetMissing"
                       && message.ControlName == "SurPickingBorder0");
    }

    [Fact]
    public void ReferenceScannerFindsPickingBorderOverlayTargetControlName()
    {
        var controls = new[]
        {
            new FrontedControlDesignItem { Name = "SurPick0", Config = new ImageFrontedControlConfig() },
            new FrontedControlDesignItem
            {
                Name = "SurPickingBorder0",
                Config = new PickingBorderOverlayControlConfig { TargetControlName = "SurPick0" }
            }
        };

        var references = new FrontedLayoutReferenceScanner().GetReferences(controls);

        var reference = Assert.Single(references);
        Assert.Equal("SurPickingBorder0", reference.SourceControlName);
        Assert.Equal(nameof(PickingBorderOverlayControlConfig.TargetControlName), reference.PropertyName);
        Assert.Equal("SurPick0", reference.TargetControlName);
    }

    [Fact]
    public void ApplyRenameReferencesUpdatesTargetControlName()
    {
        var pickingBorderConfig = new PickingBorderOverlayControlConfig { TargetControlName = "SurPick0" };
        var scanner = new FrontedLayoutReferenceScanner(
            [
                new FrontedControlDesignItem { Name = "SurPick0", Config = new ImageFrontedControlConfig() },
                new FrontedControlDesignItem { Name = "SurPickingBorder0", Config = pickingBorderConfig }
            ]);

        scanner.ApplyRenameReferences("SurPick0", "SurPickA");

        Assert.Equal("SurPickA", pickingBorderConfig.TargetControlName);
    }

    [Fact]
    public void ValidatorErrorsIfBpWindowBaseCanvasIsMissingRuntimeCriticalNames()
    {
        var config = ReadBuiltInLayout("BpWindow");
        config.Controls.Remove("SurPick0");
        config.Controls.Remove("HunPickingBorder");

        var messages = CreateValidator().Validate("BpWindow", "BaseCanvas", config);

        Assert.Contains(
            messages,
            message => message.Code == "RuntimeCriticalRenameOrDelete" && message.ControlName == "SurPick0");
        Assert.Contains(
            messages,
            message => message.Code == "RuntimeCriticalRenameOrDelete" && message.ControlName == "HunPickingBorder");
    }

    [Fact]
    public void CanvasValidationErrorsWhenVersionIsNotThree()
    {
        var document = CreateDocument([]);
        document.CanvasConfig.Version = 2;

        var messages = CreateValidator().Validate(document);

        Assert.Contains(messages, message => message.Code == "CanvasVersionInvalid");
    }

    [Theory]
    [InlineData(0, 810, "CanvasWidthInvalid")]
    [InlineData(1440, 0, "CanvasHeightInvalid")]
    [InlineData(-1, 810, "CanvasWidthInvalid")]
    [InlineData(1440, -1, "CanvasHeightInvalid")]
    public void CanvasValidationErrorsWhenCanvasSizeIsInvalid(
        double canvasWidth,
        double canvasHeight,
        string expectedCode)
    {
        var document = CreateDocument([]);
        document.CanvasConfig.CanvasWidth = canvasWidth;
        document.CanvasConfig.CanvasHeight = canvasHeight;

        var messages = CreateValidator().Validate(document);

        Assert.Contains(messages, message => message.Code == expectedCode);
    }

    [Fact]
    public void ConverterRejectsDuplicateRootLevelJsonKeys()
    {
        var exception = Assert.Throws<FrontedLayoutConfigException>(() =>
            JsonSerializer.Deserialize<FrontedCanvasConfig>(
                """
                {
                  "Version": 3,
                  "CanvasWidth": 1440,
                  "CanvasHeight": 810,
                  "Title": {
                    "ControlType": "Text",
                    "Text": "A"
                  },
                  "Title": {
                    "ControlType": "Text",
                    "Text": "B"
                  }
                }
                """));

        Assert.Contains("Duplicate root-level property 'Title'", exception.Message);
    }

    private static FrontedCanvasDesignDocument CreateDocument(IList<FrontedControlDesignItem> controls)
    {
        return new FrontedCanvasDesignDocument
        {
            WindowTypeName = "TestWindow",
            CanvasName = "BaseCanvas",
            CanvasConfig = new FrontedCanvasConfig
            {
                Version = 3,
                CanvasWidth = 1440,
                CanvasHeight = 810
            },
            Controls = new(controls)
        };
    }

    private static FrontedLayoutValidator CreateValidator()
    {
        return new FrontedLayoutValidator(
            new KnownFrontedControlRegistry(),
            runtimeContracts: new FrontedLayoutRuntimeContractCatalog(),
            referenceScanner: new FrontedLayoutReferenceScanner());
    }

    private static FrontedCanvasConfig ReadBuiltInLayout(string windowTypeName, string canvasName = "BaseCanvas")
    {
        var path = Path.Combine(
            AppConstants.ResourcesPath,
            "FrontedLayouts",
            windowTypeName,
            $"{canvasName}.json");

        Assert.True(File.Exists(path), path);
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(File.ReadAllText(path));
        Assert.NotNull(config);
        return config;
    }

    private sealed class KnownFrontedControlRegistry : IFrontedControlRegistry
    {
        private static readonly IReadOnlyCollection<IFrontedControl> Controls =
        [
            new KnownFrontedControl("Text", typeof(TextFrontedControlConfig)),
            new KnownFrontedControl("LocalizedText", typeof(LocalizedTextControlConfig)),
            new KnownFrontedControl("Image", typeof(ImageFrontedControlConfig)),
            new KnownFrontedControl("GlobalScoreRow", typeof(GlobalScoreRowControlConfig)),
            new KnownFrontedControl("TalentTraitDisplay", typeof(TalentTraitDisplayControlConfig)),
            new KnownFrontedControl("GameProgressText", typeof(GameProgressTextControlConfig)),
            new KnownFrontedControl("MapNameText", typeof(MapNameTextControlConfig)),
            new KnownFrontedControl("CurrentBanDisplay", typeof(CurrentBanDisplayControlConfig)),
            new KnownFrontedControl("BanSlotDisplay", typeof(BanSlotDisplayControlConfig)),
            new KnownFrontedControl("PickingBorderOverlay", typeof(PickingBorderOverlayControlConfig)),
            new KnownFrontedControl("MapV2Display", typeof(MapV2DisplayControlConfig))
        ];

        public IFrontedControl? GetControl(string controlType)
        {
            return Controls.FirstOrDefault(control => control.ControlType == controlType);
        }

        public IReadOnlyCollection<IFrontedControl> GetControls()
        {
            return Controls;
        }
    }

    private sealed class KnownFrontedControl(string controlType, Type configType) : IFrontedControl
    {
        public string ControlType { get; } = controlType;

        public Type ConfigType { get; } = configType;

        public FrameworkElement Create(
            string name,
            FrontedControlConfigBase config,
            FrontedControlBuildContext context)
        {
            throw new NotSupportedException();
        }
    }
}
