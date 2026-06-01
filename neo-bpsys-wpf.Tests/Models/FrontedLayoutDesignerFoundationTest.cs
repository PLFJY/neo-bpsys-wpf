#nullable enable

using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedLayoutDesignerFoundationTest
{
    public static IEnumerable<object[]> CatalogEntries()
    {
        return new FrontedDesignerLayoutCatalog()
            .GetEntries()
            .Select(entry => new object[] { entry });
    }

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
    public void FromConfigClassifiesPickingBorderOverlayAsReadonlyLinkedOverlay()
    {
        var config = new FrontedCanvasConfig
        {
            CanvasWidth = 100,
            CanvasHeight = 50,
            Controls =
            {
                ["SurPick0"] = new ImageFrontedControlConfig { Width = 141, Height = 160 },
                ["SurPickingBorder0"] = new PickingBorderOverlayControlConfig
                {
                    TargetControlName = "SurPick0",
                    Width = 141,
                    Height = 160
                }
            }
        };

        var document = new FrontedLayoutDesignConverter().FromConfig(
            "BpWindow",
            "BaseCanvas",
            config,
            new FrontedLayoutRuntimeContractCatalog());

        var overlay = document.Controls.Single(item => item.Name == "SurPickingBorder0");
        Assert.True(overlay.IsLinkedOverlay);
        Assert.False(overlay.IsSelectableInEditor);
        Assert.False(overlay.IsEditableInEditor);
        Assert.Equal("SurPick0", overlay.LinkedTargetControlName);
    }

    [Fact]
    public void FromConfigKeepsBanSlotDisplaySelectableAndEditable()
    {
        var config = new FrontedCanvasConfig
        {
            CanvasWidth = 100,
            CanvasHeight = 50,
            Controls =
            {
                ["SurBanCurrent0"] = new BanSlotDisplayControlConfig { Width = 44.5, Height = 44.5 }
            }
        };

        var document = new FrontedLayoutDesignConverter().FromConfig(
            "BpWindow",
            "BaseCanvas",
            config,
            new FrontedLayoutRuntimeContractCatalog());

        var banSlot = Assert.Single(document.Controls);
        Assert.True(banSlot.IsSelectableInEditor);
        Assert.True(banSlot.IsEditableInEditor);
        Assert.False(banSlot.IsLinkedOverlay);
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

    [Fact]
    public void DesignerLayoutCatalogListsMigratedWindowsAndCanvases()
    {
        var entries = new FrontedDesignerLayoutCatalog().GetEntries();

        Assert.Equal(9, entries.Count);
        Assert.Contains(entries, entry => entry.WindowTypeName == "ScoreSurWindow" && entry.CanvasName == "BaseCanvas");
        Assert.Contains(entries, entry => entry.WindowTypeName == "ScoreHunWindow" && entry.CanvasName == "BaseCanvas");
        Assert.Contains(entries, entry => entry.WindowTypeName == "ScoreGlobalWindow" && entry.CanvasName == "BaseCanvas");
        Assert.Contains(entries, entry => entry.WindowTypeName == "CutSceneWindow" && entry.CanvasName == "BaseCanvas");
        Assert.Contains(entries, entry => entry.WindowTypeName == "GameDataWindow" && entry.CanvasName == "BaseCanvas");
        Assert.Contains(entries, entry => entry.WindowTypeName == "WidgetsWindow" && entry.CanvasName == "MapBpCanvas");
        Assert.Contains(entries, entry => entry.WindowTypeName == "WidgetsWindow" && entry.CanvasName == "BpOverViewCanvas");
        Assert.Contains(entries, entry => entry.WindowTypeName == "WidgetsWindow" && entry.CanvasName == "MapV2Canvas");
        Assert.Contains(entries, entry => entry.WindowTypeName == "BpWindow" && entry.CanvasName == "BaseCanvas");
        Assert.All(entries, entry =>
        {
            Assert.True(entry.IsMigrated);
            Assert.True(entry.IsEditable);
        });
    }

    [Fact]
    public void DesignerLayoutCatalogListsExactlyThreeWidgetsWindowCanvases()
    {
        var widgetsCanvases = new FrontedDesignerLayoutCatalog()
            .GetEntries()
            .Where(entry => entry.WindowTypeName == "WidgetsWindow")
            .Select(entry => entry.CanvasName)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(["BpOverViewCanvas", "MapBpCanvas", "MapV2Canvas"], widgetsCanvases);
    }

    [Fact]
    public void DesignerPreviewSharedDataServiceProvidesIsolatedPlaceholderGame()
    {
        var service = new DesignerPreviewSharedDataService();

        Assert.Equal("HomeTeam", service.CurrentGame.SurTeam.Name);
        Assert.Equal("AwayTeam", service.CurrentGame.HunTeam.Name);
        Assert.Equal("30", service.RemainingSeconds);
        Assert.Equal(GameProgress.Game1FirstHalf, service.CurrentGame.GameProgress);
        Assert.Equal(Map.EversleepingTown, service.CurrentGame.PickedMap);
        Assert.Equal(Map.TheRedChurch, service.CurrentGame.BannedMap);
        Assert.Equal("Player 1", service.CurrentGame.SurPlayerList[0].Member.Name);
        Assert.Equal("Player 5", service.CurrentGame.HunPlayer.Member.Name);
        Assert.Equal("幸运儿", service.CurrentGame.SurPlayerList[0].Character?.Name);
        Assert.Equal("厂长", service.CurrentGame.HunPlayer.Character?.Name);
        Assert.True(service.CurrentGame.SurPlayerList[0].Talent.BorrowedTime);
        Assert.True(service.CurrentGame.SurPlayerList[0].Talent.FlywheelEffect);
        Assert.True(service.CurrentGame.HunPlayer.Talent.Detention);
        Assert.True(service.CurrentGame.HunPlayer.Talent.TrumpCard);
        Assert.Equal(TraitType.Blink, service.CurrentGame.HunPlayer.Trait.TraitName);
        Assert.Equal(0, service.CurrentGame.MatchScore.HomeTotalMinorScore);
        Assert.Equal(0, service.CurrentGame.MatchScore.AwayTotalMinorScore);
        Assert.All(service.CanCurrentSurBannedList, Assert.True);
        Assert.All(service.CanCurrentHunBannedList, Assert.True);
        Assert.All(service.CanGlobalSurBannedList, Assert.True);
        Assert.All(service.CanGlobalHunBannedList, Assert.True);
    }

    [Theory]
    [MemberData(nameof(CatalogEntries))]
    public void DesignerLayoutCatalogEntryPointsToExistingBuiltInLayout(
        FrontedDesignerLayoutCatalogEntry entry)
    {
        var path = Path.Combine(
            AppConstants.ResourcesPath,
            "FrontedLayouts",
            entry.WindowTypeName,
            $"{entry.CanvasName}.json");

        Assert.True(File.Exists(path), path);
    }

    [Theory]
    [MemberData(nameof(CatalogEntries))]
    public void DesignerLayoutCatalogLayoutLoadsAndValidatesWithoutErrors(
        FrontedDesignerLayoutCatalogEntry entry)
    {
        var config = ReadBuiltInLayout(entry.WindowTypeName, entry.CanvasName);
        var messages = CreateValidator().Validate(entry.WindowTypeName, entry.CanvasName, config);

        Assert.DoesNotContain(messages, message => message.Severity == FrontedLayoutValidationSeverity.Error);
    }

    [Theory]
    [MemberData(nameof(CatalogEntries))]
    public void DesignerDocumentUsesCanvasSizeFromLoadedConfig(
        FrontedDesignerLayoutCatalogEntry entry)
    {
        var config = ReadBuiltInLayout(entry.WindowTypeName, entry.CanvasName);
        var document = new FrontedLayoutDesignConverter().FromConfig(
            entry.WindowTypeName,
            entry.CanvasName,
            config,
            new FrontedLayoutRuntimeContractCatalog());

        Assert.Equal(config.CanvasWidth, document.CanvasConfig.CanvasWidth);
        Assert.Equal(config.CanvasHeight, document.CanvasConfig.CanvasHeight);
    }

    [Theory]
    [InlineData(10.24, 10)]
    [InlineData(10.25, 10.5)]
    [InlineData(10.75, 11)]
    public void DesignerGeometryHelperSnapsToHalfStep(double value, double expected)
    {
        Assert.Equal(expected, FrontedDesignerGeometryHelper.Snap(value));
    }

    [Fact]
    public void DesignerGeometryHelperMovesControlAndMarksDocumentDirty()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 10, Top = 20 }
        };
        var document = CreateDocument([item]);

        FrontedDesignerGeometryHelper.Move(item, 10, 20, 0.24, 0.25, document);

        Assert.Equal(10, item.Config.Left);
        Assert.Equal(20.5, item.Config.Top);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void DesignerGeometryHelperResizesRightBottomAndClampsMinimum()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Image",
            Config = new ImageFrontedControlConfig
            {
                Left = 10,
                Top = 20,
                Width = 50,
                Height = 40
            }
        };
        var document = CreateDocument([item]);

        FrontedDesignerGeometryHelper.Resize(
            item,
            FrontedDesignerResizeHandleKind.BottomRight,
            10,
            20,
            50,
            40,
            -100,
            -100,
            document);

        Assert.Equal(10, item.Config.Left);
        Assert.Equal(20, item.Config.Top);
        Assert.Equal(1, item.Config.Width);
        Assert.Equal(1, item.Config.Height);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void DesignerGeometryHelperLeftTopResizeUpdatesPositionAndSize()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Image",
            Config = new ImageFrontedControlConfig
            {
                Left = 10,
                Top = 20,
                Width = 50,
                Height = 40
            }
        };

        FrontedDesignerGeometryHelper.Resize(
            item,
            FrontedDesignerResizeHandleKind.TopLeft,
            10,
            20,
            50,
            40,
            5.25,
            -4.75);

        Assert.Equal(15.5, item.Config.Left);
        Assert.Equal(15.5, item.Config.Top);
        Assert.Equal(45, item.Config.Width);
        Assert.Equal(45, item.Config.Height);
    }

    [Fact]
    public void DesignerGeometryHelperUsesFallbackSizeWhenResizingControlWithoutSize()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 10, Top = 20 }
        };

        FrontedDesignerGeometryHelper.ResizeBy(
            item,
            FrontedDesignerResizeHandleKind.Right,
            12,
            0);

        Assert.Equal(52, item.Config.Width);
        Assert.Equal(24, item.Config.Height);
    }

    [Fact]
    public void DesignerGeometryHelperKeepsRuntimeCriticalFlagAfterMove()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "SurPick0",
            IsRuntimeCritical = true,
            Config = new ImageFrontedControlConfig { Left = 10, Top = 20 }
        };

        FrontedDesignerGeometryHelper.MoveBy(item, 1, 1);

        Assert.True(item.IsRuntimeCritical);
    }

    [Theory]
    [InlineData("Text", typeof(TextFrontedControlConfig), 160, 40)]
    [InlineData("LocalizedText", typeof(LocalizedTextControlConfig), 200, 40)]
    [InlineData("Image", typeof(ImageFrontedControlConfig), 120, 120)]
    [InlineData("BorderedImage", typeof(BorderedImageFrontedControlConfig), 120, 120)]
    [InlineData("MapNameText", typeof(MapNameTextControlConfig), 240, 40)]
    [InlineData("GameProgressText", typeof(GameProgressTextControlConfig), 260, 56)]
    [InlineData("TalentTraitDisplay", typeof(TalentTraitDisplayControlConfig), 180, 40)]
    [InlineData("GlobalScoreRow", typeof(GlobalScoreRowControlConfig), 540, 40)]
    [InlineData("CurrentBanDisplay", typeof(CurrentBanDisplayControlConfig), 70, 36)]
    [InlineData("BanSlotDisplay", typeof(BanSlotDisplayControlConfig), 48, 48)]
    [InlineData("MapV2Display", typeof(MapV2DisplayControlConfig), 151, 160)]
    public void DefaultConfigFactoryCreatesValidAddControlDefaults(
        string controlType,
        Type expectedType,
        double expectedWidth,
        double expectedHeight)
    {
        var document = CreateDocument(
            [
                new FrontedControlDesignItem
                {
                    Name = "Existing",
                    Config = new TextFrontedControlConfig { ZIndex = 7 }
                }
            ]);
        var factory = new FrontedControlDefaultConfigFactory();

        var config = factory.Create(controlType, document, 100.25, 100.25);

        Assert.IsType(expectedType, config);
        Assert.Equal(controlType, config.ControlType);
        Assert.Equal(expectedWidth, config.Width);
        Assert.Equal(expectedHeight, config.Height);
        Assert.Equal(8, config.ZIndex);
        Assert.Equal(FrontedDesignerGeometryHelper.Snap(config.Left), config.Left);
        Assert.Equal(FrontedDesignerGeometryHelper.Snap(config.Top), config.Top);
    }

    [Fact]
    public void DefaultConfigFactoryDoesNotCreatePickingBorderOverlayFromNormalAddControl()
    {
        var factory = new FrontedControlDefaultConfigFactory();

        Assert.False(factory.CanCreate("PickingBorderOverlay"));
        Assert.Throws<NotSupportedException>(() => factory.Create("PickingBorderOverlay", CreateDocument([])));
    }

    [Fact]
    public void DefaultConfigFactoryUsesControlSpecificRecommendedDefaults()
    {
        var factory = new FrontedControlDefaultConfigFactory();
        var document = CreateDocument([]);

        var text = Assert.IsType<TextFrontedControlConfig>(factory.Create("Text", document));
        Assert.Equal("Text", text.Text);
        Assert.Equal("#FFFFFFFF", text.Color);
        Assert.Equal("Center", text.TextAlignment);

        var localizedText = Assert.IsType<LocalizedTextControlConfig>(factory.Create("LocalizedText", document));
        Assert.Equal("Text", localizedText.LocalizationKey);
        Assert.Equal("Localized Text", localizedText.FallbackText);

        var talent = Assert.IsType<TalentTraitDisplayControlConfig>(factory.Create("TalentTraitDisplay", document));
        Assert.Equal(TalentTraitDisplayKind.SurvivorTalent, talent.DisplayKind);
        Assert.Equal(0, talent.PlayerIndex);
        Assert.Equal(36, talent.IconSize);

        var globalScore = Assert.IsType<GlobalScoreRowControlConfig>(factory.Create("GlobalScoreRow", document));
        Assert.Equal(TeamType.HomeTeam, globalScore.TeamType);

        var currentBan = Assert.IsType<CurrentBanDisplayControlConfig>(factory.Create("CurrentBanDisplay", document));
        Assert.Equal(Camp.Sur, currentBan.Camp);
        Assert.Equal(0, currentBan.Index);

        var banSlot = Assert.IsType<BanSlotDisplayControlConfig>(factory.Create("BanSlotDisplay", document));
        Assert.Equal(BanSlotKind.Current, banSlot.SlotKind);
        Assert.Equal(Camp.Sur, banSlot.Camp);

        var mapV2 = Assert.IsType<MapV2DisplayControlConfig>(factory.Create("MapV2Display", document));
        Assert.Equal("ArmsFactory", mapV2.MapKey);
    }

    [Fact]
    public void ControlNameGeneratorCreatesUniqueNamesAndSkipsLinkedOverlayNames()
    {
        var document = CreateDocument(
            [
                new FrontedControlDesignItem { Name = "Text1", Config = new TextFrontedControlConfig() },
                new FrontedControlDesignItem
                {
                    Name = "Text2",
                    IsSelectableInEditor = false,
                    Config = new PickingBorderOverlayControlConfig { TargetControlName = "Text1" }
                }
            ]);
        var generator = new FrontedControlNameGenerator();

        Assert.Equal("Image1", generator.Generate("Image", document));
        Assert.Equal("Text3", generator.Generate("Text", document));
        Assert.Equal("Text1", generator.Generate("Text", CreateDocument([])));
    }

    [Fact]
    public void AddControlCommandAddsSelectsMarksDirtyClearsFilterAndRequestsPreview()
    {
        var document = CreateDocument(
            [
                new FrontedControlDesignItem
                {
                    Name = "Title",
                    Config = new TextFrontedControlConfig { ZIndex = 3 }
                }
            ]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.ControlFilterText = "will-hide-new-control";
        var previewRequests = 0;
        viewModel.PreviewRenderRequested += (_, _) => previewRequests++;

        viewModel.AddControlCommand.Execute(new FrontedAddControlRequest
        {
            ControlType = "Text",
            CenterX = 300.25,
            CenterY = 200.25
        });

        var added = Assert.Single(document.Controls, control => control.Name == "Text1");
        Assert.IsType<TextFrontedControlConfig>(added.Config);
        Assert.Same(added, viewModel.SelectedDesignItem);
        Assert.True(document.IsDirty);
        Assert.Equal(string.Empty, viewModel.ControlFilterText);
        Assert.Contains(added, viewModel.FilteredDesignItems);
        Assert.Equal(4, added.Config.ZIndex);
        Assert.Equal(220.5, added.Config.Left);
        Assert.Equal(180.5, added.Config.Top);
        Assert.True(previewRequests > 0);
    }

    [Fact]
    public void AddControlCommandRefusesAtCanvasControlLimit()
    {
        var controls = Enumerable.Range(0, FrontedLayoutLimits.MaxControlsPerCanvas)
            .Select(index => new FrontedControlDesignItem
            {
                Name = $"Text{index}",
                IsSelectableInEditor = true,
                IsEditableInEditor = true,
                Config = new TextFrontedControlConfig()
            })
            .ToList();
        var document = CreateDocument(controls);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        viewModel.AddControlCommand.Execute(new FrontedAddControlRequest { ControlType = "Text" });

        Assert.Equal(FrontedLayoutLimits.MaxControlsPerCanvas, document.Controls.Count);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
    }

    [Fact]
    public void CopyPasteNormalControlCreatesOffsetSelectedDirtyUndoableCopy()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Text9",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Text = "A", Left = 10, Top = 20, ZIndex = 3 }
        };
        var document = CreateDocument([title]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(title);

        viewModel.CopySelectedControlCommand.Execute(null);
        viewModel.PasteControlCommand.Execute(null);

        var pasted = Assert.Single(document.Controls, control => control.Name == "Text10");
        Assert.NotSame(title.Config, pasted.Config);
        Assert.Equal(20, pasted.Config.Left);
        Assert.Equal(30, pasted.Config.Top);
        Assert.Equal(4, pasted.Config.ZIndex);
        Assert.Same(pasted, viewModel.SelectedDesignItem);
        Assert.True(document.IsDirty);
        Assert.True(viewModel.CanUndo);
    }

    [Fact]
    public void DeleteSelectedControlRemovesNormalControlMarksDirtyAndClearsSelection()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig()
        };
        var logo = new FrontedControlDesignItem
        {
            Name = "Logo",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new ImageFrontedControlConfig()
        };
        var document = CreateDocument([title, logo]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        var previewRequests = 0;
        viewModel.PreviewRenderRequested += (_, _) => previewRequests++;
        viewModel.SelectDesignItem(title);

        viewModel.DeleteSelectedControlCommand.Execute(null);

        Assert.DoesNotContain(title, document.Controls);
        Assert.Contains(logo, document.Controls);
        Assert.True(document.IsDirty);
        Assert.Null(viewModel.SelectedDesignItem);
        Assert.DoesNotContain(title, viewModel.FilteredDesignItems);
        Assert.True(previewRequests > 0);
    }

    [Fact]
    public void DeleteSelectedControlRefusesRuntimeCriticalAndReferencedControls()
    {
        var runtimeCritical = new FrontedControlDesignItem
        {
            Name = "SurPick0",
            IsRuntimeCritical = true,
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new ImageFrontedControlConfig()
        };
        var referenced = new FrontedControlDesignItem
        {
            Name = "Target",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new ImageFrontedControlConfig()
        };
        var overlay = new FrontedControlDesignItem
        {
            Name = "TargetOverlay",
            IsSelectableInEditor = false,
            IsEditableInEditor = false,
            Config = new PickingBorderOverlayControlConfig { TargetControlName = "Target" }
        };
        var document = CreateDocument([runtimeCritical, referenced, overlay], "BpWindow");
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        viewModel.SelectDesignItem(runtimeCritical);
        viewModel.DeleteSelectedControlCommand.Execute(null);
        Assert.Contains(runtimeCritical, document.Controls);

        viewModel.SelectDesignItem(referenced);
        viewModel.DeleteSelectedControlCommand.Execute(null);
        Assert.Contains(referenced, document.Controls);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void AddControlUndoRedoRestoresInMemoryDocument()
    {
        var document = CreateDocument([]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        viewModel.AddControlCommand.Execute(new FrontedAddControlRequest { ControlType = "Text" });
        Assert.Single(viewModel.CurrentDocument!.Controls);
        Assert.True(viewModel.CanUndo);

        viewModel.UndoCommand.Execute(null);
        Assert.Empty(viewModel.CurrentDocument!.Controls);
        Assert.True(viewModel.CanRedo);

        viewModel.RedoCommand.Execute(null);
        Assert.Single(viewModel.CurrentDocument!.Controls);
    }

    [Fact]
    public void DeleteControlUndoRestoresControl()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig()
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([title]) };
        viewModel.SelectDesignItem(title);

        viewModel.DeleteSelectedControlCommand.Execute(null);
        Assert.Empty(viewModel.CurrentDocument!.Controls);

        viewModel.UndoCommand.Execute(null);
        Assert.Single(viewModel.CurrentDocument!.Controls);
        Assert.Equal("Title", viewModel.CurrentDocument!.Controls[0].Name);
    }

    [Fact]
    public void PropertyAndGeometryUndoRestorePreviousValuesAndClearRedoOnNewEdit()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Text = "Old", Left = 10, Top = 20 }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([title]) };
        viewModel.SelectDesignItem(title);

        viewModel.ApplyPropertyEdit(
            new FrontedPropertyEditorItem
            {
                PropertyName = nameof(TextFrontedControlConfig.Text),
                EditorKind = FrontedPropertyEditorKind.Text
            },
            "New");
        Assert.Equal("New", ((TextFrontedControlConfig)viewModel.CurrentDocument!.Controls[0].Config).Text);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal("Old", ((TextFrontedControlConfig)viewModel.CurrentDocument!.Controls[0].Config).Text);

        viewModel.RedoCommand.Execute(null);
        Assert.Equal("New", ((TextFrontedControlConfig)viewModel.CurrentDocument!.Controls[0].Config).Text);

        viewModel.UndoCommand.Execute(null);
        viewModel.SelectDesignItem(viewModel.CurrentDocument!.Controls[0]);
        viewModel.MoveSelectedDesignItemBy(5, 0);

        Assert.False(viewModel.CanRedo);
        Assert.Equal(15, viewModel.CurrentDocument!.Controls[0].Config.Left);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal(10, viewModel.CurrentDocument!.Controls[0].Config.Left);
    }

    [Fact]
    public void LinkedOverlaySynchronizerCopiesTargetGeometryToPickingBorderOverlay()
    {
        var target = new FrontedControlDesignItem
        {
            Name = "SurPick0",
            Config = new ImageFrontedControlConfig
            {
                Left = 10,
                Top = 20,
                Width = 141,
                Height = 160
            }
        };
        var overlay = new FrontedControlDesignItem
        {
            Name = "SurPickingBorder0",
            Config = new PickingBorderOverlayControlConfig
            {
                TargetControlName = "SurPick0",
                Left = 0,
                Top = 0,
                Width = 1,
                Height = 1
            }
        };
        var document = CreateDocument([target, overlay]);

        FrontedDesignerGeometryHelper.Move(target, 10, 20, 5, 6, document);
        FrontedDesignerGeometryHelper.Resize(
            target,
            FrontedDesignerResizeHandleKind.BottomRight,
            target.Config.Left,
            target.Config.Top,
            target.Config.Width!.Value,
            target.Config.Height!.Value,
            9,
            10,
            document);
        var changed = FrontedLayoutLinkedOverlaySynchronizer.SyncLinkedOverlays(document, target);

        var overlayConfig = Assert.IsType<PickingBorderOverlayControlConfig>(overlay.Config);
        Assert.Single(changed);
        Assert.Equal(target.Config.Left, overlayConfig.Left);
        Assert.Equal(target.Config.Top, overlayConfig.Top);
        Assert.Equal(target.Config.Width, overlayConfig.Width);
        Assert.Equal(target.Config.Height, overlayConfig.Height);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void LinkedOverlaySynchronizerDoesNotLetOverlayDriveTarget()
    {
        var target = new FrontedControlDesignItem
        {
            Name = "SurPick0",
            Config = new ImageFrontedControlConfig { Left = 10, Top = 20, Width = 141, Height = 160 }
        };
        var overlay = new FrontedControlDesignItem
        {
            Name = "SurPickingBorder0",
            IsLinkedOverlay = true,
            Config = new PickingBorderOverlayControlConfig
            {
                TargetControlName = "SurPick0",
                Left = 99,
                Top = 88,
                Width = 77,
                Height = 66
            }
        };
        var document = CreateDocument([target, overlay]);

        var changed = FrontedLayoutLinkedOverlaySynchronizer.SyncLinkedOverlays(document, overlay);

        Assert.Empty(changed);
        Assert.Equal(10, target.Config.Left);
        Assert.Equal(20, target.Config.Top);
        Assert.Equal(141, target.Config.Width);
        Assert.Equal(160, target.Config.Height);
    }

    [Fact]
    public void DesignerControlFilterMatchesNameAndControlType()
    {
        var textItem = new FrontedControlDesignItem
        {
            Name = "SurTeamName",
            Config = new TextFrontedControlConfig { ControlType = "Text" }
        };
        var imageItem = new FrontedControlDesignItem
        {
            Name = "TeamLogo",
            Config = new ImageFrontedControlConfig { ControlType = "Image" }
        };

        Assert.True(FrontedDesignerWindowViewModel.MatchesControlFilter(textItem, "team"));
        Assert.True(FrontedDesignerWindowViewModel.MatchesControlFilter(imageItem, "image"));
        Assert.False(FrontedDesignerWindowViewModel.MatchesControlFilter(imageItem, "score"));
    }

    [Fact]
    public void DesignerViewModelFiltersControlsAndClearsFilterOnDocumentClear()
    {
        var overlay = new FrontedControlDesignItem
        {
            Name = "SurPickingBorder0",
            IsSelectableInEditor = false,
            Config = new PickingBorderOverlayControlConfig
            {
                ControlType = "PickingBorderOverlay",
                TargetControlName = "Logo",
                ZIndex = 3
            }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument(
            [
                new FrontedControlDesignItem
                {
                    Name = "Title",
                    Config = new TextFrontedControlConfig { ControlType = "Text", ZIndex = 1 }
                },
                new FrontedControlDesignItem
                {
                    Name = "Logo",
                    Config = new ImageFrontedControlConfig { ControlType = "Image", ZIndex = 2 }
                },
                overlay
            ])
        };

        Assert.Equal(["Logo", "Title"], viewModel.FilteredDesignItems.Select(item => item.Name));
        Assert.DoesNotContain(overlay, viewModel.FilteredDesignItems);

        viewModel.ControlFilterText = "text";

        Assert.Equal(["Title"], viewModel.FilteredDesignItems.Select(item => item.Name));

        viewModel.ControlFilterText = string.Empty;
        Assert.Equal(2, viewModel.FilteredDesignItems.Count);

        viewModel.CurrentDocument = null;
        viewModel.ControlFilterText = string.Empty;
        Assert.Empty(viewModel.FilteredDesignItems);
    }

    [Fact]
    public void DesignerViewModelSelectDesignItemSetsSelectedItemAndKeepsItAfterMove()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 10, Top = 20, ControlType = "Text" }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([item])
        };

        viewModel.SelectDesignItem(item);
        viewModel.MoveSelectedDesignItem(10, 20, 5, 5, renderPreview: false);

        Assert.Same(item, viewModel.SelectedDesignItem);
        Assert.True(item.IsSelected);
    }

    [Fact]
    public void DesignerViewModelDoesNotSelectReadonlyLinkedOverlay()
    {
        var overlay = new FrontedControlDesignItem
        {
            Name = "SurPickingBorder0",
            IsSelectableInEditor = false,
            Config = new PickingBorderOverlayControlConfig { TargetControlName = "SurPick0" }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([overlay])
        };

        viewModel.SelectDesignItem(overlay);

        Assert.Null(viewModel.SelectedDesignItem);
        Assert.False(overlay.IsSelected);
    }

    [Fact]
    public void DesignerViewModelCanResizeBorderedImageInnerImage()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Pick",
            Config = new BorderedImageFrontedControlConfig
            {
                Left = 10,
                Top = 20,
                Width = 120,
                Height = 80,
                ImageWidth = 60,
                ImageHeight = 40
            }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([item])
        };

        viewModel.SelectDesignItem(item);
        viewModel.BorderedImageResizeTarget = FrontedDesignerResizeTarget.Image;
        viewModel.ResizeSelectedDesignItem(
            FrontedDesignerResizeHandleKind.BottomRight,
            originalLeft: 10,
            originalTop: 20,
            originalWidth: 60,
            originalHeight: 40,
            deltaX: 15,
            deltaY: 10,
            renderPreview: false);

        var config = Assert.IsType<BorderedImageFrontedControlConfig>(item.Config);
        Assert.Equal(120, config.Width);
        Assert.Equal(80, config.Height);
        Assert.Equal(75, config.ImageWidth);
        Assert.Equal(50, config.ImageHeight);
        Assert.True(viewModel.CurrentDocument!.IsDirty);
    }

    [Fact]
    public void PropertyGridBuilderCreatesIdentityAndLayoutRows()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig
            {
                Left = 10,
                Top = 20,
                Width = 100,
                Height = 40,
                ZIndex = 2
            }
        };
        var rows = BuildPropertyRows(CreateDocument([item]), item);

        Assert.Contains(rows, row => row.PropertyName == nameof(FrontedControlDesignItem.Name));
        Assert.Contains(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.ControlType));
        Assert.Contains(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.Left));
        Assert.Contains(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.Top));
        Assert.Contains(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.Width));
        Assert.Contains(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.Height));
        Assert.Contains(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.ZIndex));
    }

    [Fact]
    public void PropertyGridBuilderSeparatesBorderedImageBorderAndImageRows()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Pick",
            Config = new BorderedImageFrontedControlConfig
            {
                ImageWidth = 60,
                ImageHeight = 40,
                Stretch = "UniformToFill"
            }
        };

        var rows = BuildPropertyRows(CreateDocument([item]), item);

        Assert.Equal("Border", rows.Single(row => row.PropertyName == nameof(FrontedControlConfigBase.Width)).GroupName);
        Assert.Equal("Border", rows.Single(row => row.PropertyName == nameof(FrontedControlConfigBase.Height)).GroupName);
        Assert.Equal("Image", rows.Single(row => row.PropertyName == nameof(BorderedImageFrontedControlConfig.ImageWidth)).GroupName);
        Assert.Equal("Image", rows.Single(row => row.PropertyName == nameof(BorderedImageFrontedControlConfig.ImageHeight)).GroupName);
        Assert.Equal("Image", rows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.Stretch)).GroupName);
    }

    [Fact]
    public void PropertyGridBuilderAppliesNameReadOnlyRules()
    {
        var runtimeCritical = new FrontedControlDesignItem
        {
            Name = "SurPick0",
            Config = new ImageFrontedControlConfig()
        };
        var normal = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig()
        };

        var criticalRows = BuildPropertyRows(
            CreateDocument([runtimeCritical], "BpWindow"),
            runtimeCritical);
        var normalRows = BuildPropertyRows(CreateDocument([normal]), normal);

        Assert.True(criticalRows.Single(row => row.PropertyName == nameof(FrontedControlDesignItem.Name)).IsReadOnly);
        Assert.False(normalRows.Single(row => row.PropertyName == nameof(FrontedControlDesignItem.Name)).IsReadOnly);
    }

    [Fact]
    public void PropertyGridBuilderMapsSupportedEditorKinds()
    {
        var text = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig
            {
                Text = "A",
                FontSize = 24,
                Color = "#FFFFFFFF"
            }
        };
        var image = new FrontedControlDesignItem
        {
            Name = "Logo",
            Config = new ImageFrontedControlConfig
            {
                PickingBorder = true,
                SizingMode = ImageSizingMode.FillContainer
            }
        };

        var textRows = BuildPropertyRows(CreateDocument([text]), text);
        var imageRows = BuildPropertyRows(CreateDocument([image]), image);

        Assert.Equal(
            FrontedPropertyEditorKind.Text,
            textRows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.Text)).EditorKind);
        Assert.Equal(
            FrontedPropertyEditorKind.Number,
            textRows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.FontSize)).EditorKind);
        Assert.Equal(
            FrontedPropertyEditorKind.Color,
            textRows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.Color)).EditorKind);
        Assert.Equal(
            FrontedPropertyEditorKind.FontFamily,
            textRows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.FontFamily)).EditorKind);
        Assert.Equal(
            FrontedPropertyEditorKind.Boolean,
            imageRows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.PickingBorder)).EditorKind);
        Assert.Equal(
            FrontedPropertyEditorKind.Enum,
            imageRows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.SizingMode)).EditorKind);
    }

    [Theory]
    [InlineData("HorizontalAlignment", "Left", "Center", "Right", "Stretch")]
    [InlineData("VerticalAlignment", "Top", "Center", "Bottom", "Stretch")]
    [InlineData("TextAlignment", "Left", "Center", "Right", "Justify")]
    [InlineData("TextWrapping", "NoWrap", "Wrap", "WrapWithOverflow")]
    [InlineData("Stretch", "None", "Fill", "Uniform", "UniformToFill")]
    [InlineData("FontWeight", "Normal", "Bold", "SemiBold", "Light", "Medium", "ExtraBold")]
    public void PropertyGridBuilderMapsStringOptionPropertiesToComboBox(string propertyName, params string[] options)
    {
        var text = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig()
        };
        var image = new FrontedControlDesignItem
        {
            Name = "Logo",
            Config = new ImageFrontedControlConfig()
        };
        var item = propertyName == "Stretch" ? image : text;

        var rows = BuildPropertyRows(CreateDocument([item]), item);
        var row = rows.Single(row => row.PropertyName == propertyName);

        Assert.Equal(FrontedPropertyEditorKind.Enum, row.EditorKind);
        Assert.Equal(options, row.Options?.Cast<FrontedPropertyEditorOption>().Select(option => option.Value).Cast<string>().ToArray());
    }

    [Fact]
    public void PropertyGridLocalizationKeepsRawPropertyNamesAndLocalizesDisplay()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig
            {
                Left = 10,
                HorizontalAlignment = "Center"
            }
        };
        var localizer = new TestDesignerLocalizationService(
            propertyNames: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [nameof(FrontedControlDesignItem.Name)] = "控件名称",
                [nameof(FrontedControlConfigBase.Left)] = "X 坐标"
            },
            groupNames: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Identity"] = "标识"
            });
        var rows = BuildPropertyRows(CreateDocument([item]), item, localizer);

        var nameRow = rows.Single(row => row.PropertyName == nameof(FrontedControlDesignItem.Name));
        Assert.Equal(nameof(FrontedControlDesignItem.Name), nameRow.PropertyName);
        Assert.Equal("控件名称", nameRow.DisplayName);
        Assert.Equal("Identity", nameRow.GroupName);
        Assert.Equal("标识", nameRow.GroupDisplayName);

        var leftRow = rows.Single(row => row.PropertyName == nameof(FrontedControlConfigBase.Left));
        Assert.Equal("X 坐标", leftRow.DisplayName);

        var missingRow = rows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.Text));
        Assert.Equal(nameof(TextFrontedControlConfig.Text), missingRow.DisplayName);
    }

    [Fact]
    public void PropertyGridOptionsDisplayLocalizedNamesButKeepRawValues()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig
            {
                HorizontalAlignment = "Center"
            }
        };
        var localizer = new TestDesignerLocalizationService(
            options: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HorizontalAlignment.Right"] = "右"
            });

        var rows = BuildPropertyRows(CreateDocument([item]), item, localizer);
        var row = rows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.HorizontalAlignment));
        var right = Assert.IsType<FrontedPropertyEditorOption>(
            Assert.Single(row.Options!.Cast<FrontedPropertyEditorOption>(), option => Equals(option.Value, "Right")));

        Assert.Equal("Right", right.Value);
        Assert.Equal("右", right.DisplayName);

        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([item]) };
        viewModel.SelectDesignItem(item);
        Assert.True(viewModel.ApplyPropertyEdit(row, right.Value));
        Assert.Equal("Right", ((TextFrontedControlConfig)item.Config).HorizontalAlignment);
        Assert.Contains("\"HorizontalAlignment\":\"Right\"", JsonSerializer.Serialize((TextFrontedControlConfig)item.Config));
    }

    [Fact]
    public void DesignerLocalizationKeepsContractIdsRawAndFallsBackForUnknownControlTypes()
    {
        var localizer = new TestDesignerLocalizationService(
            controlTypes: new Dictionary<string, string>(StringComparer.Ordinal) { ["Text"] = "文本" },
            windows: new Dictionary<string, string>(StringComparer.Ordinal) { ["BpWindow"] = "BP 主窗口" },
            canvases: new Dictionary<string, string>(StringComparer.Ordinal) { ["BaseCanvas"] = "主画布" });
        var catalogEntry = new FrontedDesignerLayoutCatalog().GetEntries()
            .Single(entry => entry.WindowTypeName == "BpWindow" && entry.CanvasName == "BaseCanvas");

        Assert.Equal("Text", new TextFrontedControlConfig().ControlType);
        Assert.Equal("文本", localizer.GetControlTypeDisplayName("Text"));
        Assert.Equal("PluginFancyControl", localizer.GetControlTypeDisplayName("PluginFancyControl"));
        Assert.Equal("BpWindow", catalogEntry.WindowTypeName);
        Assert.Equal("BaseCanvas", catalogEntry.CanvasName);
        Assert.Equal("BP 主窗口", localizer.GetWindowDisplayName(catalogEntry.WindowTypeName));
        Assert.Equal("主画布", localizer.GetCanvasDisplayName(catalogEntry.CanvasName));
    }

    [Fact]
    public void BindingBrowserLocalizationKeepsRawBindingPathVisible()
    {
        var localizer = new TestDesignerLocalizationService(
            bindings: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CurrentGame.SurTeam.Name"] = "求生者队伍名称"
            });
        var provider = new FrontedBindingBrowserProvider(localizer);

        var node = provider.Search("求生者", FrontedBindingTypeFilter.Text)
            .Single(item => item.FullPath == "CurrentGame.SurTeam.Name");

        Assert.Equal("求生者队伍名称", node.DisplayName);
        Assert.Equal("CurrentGame.SurTeam.Name", node.FullPath);
    }

    [Fact]
    public void DesignerPropertyGridLocalizationKeysCoverBuiltInConfigPropertiesAndOptions()
    {
        var requiredKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "Designer.Property.Name",
            "Designer.Property.RuntimeCritical",
            "Designer.Property.LinkedTargetControlName"
        };

        foreach (var type in BuiltInConfigTypes())
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(property => property.GetIndexParameters().Length == 0 && property.CanRead))
            {
                var coreType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (coreType == typeof(string)
                    || coreType == typeof(bool)
                    || coreType.IsEnum
                    || coreType == typeof(int)
                    || coreType == typeof(double))
                {
                    requiredKeys.Add($"Designer.Property.{property.Name}");
                }
            }

            var config = (FrontedControlConfigBase)Activator.CreateInstance(type)!;
            var item = new FrontedControlDesignItem
            {
                Name = type.Name,
                Config = config
            };
            foreach (var row in BuildPropertyRows(CreateDocument([item]), item)
                         .Where(row => row.Options is not null))
            {
                foreach (var option in row.Options!.OfType<FrontedPropertyEditorOption>())
                {
                    requiredKeys.Add($"Designer.Option.{row.PropertyName}.{option.Value}");
                }
            }
        }

        foreach (var fileName in new[] { "Lang.resx", "Lang.en-us.resx", "Lang.ja-jp.resx" })
        {
            var names = LoadResxKeys(fileName);
            foreach (var key in requiredKeys.OrderBy(key => key, StringComparer.Ordinal))
            {
                Assert.Contains(key, names);
            }
        }
    }

    [Fact]
    public void PropertyColorHelperParsesFormatsAndFallsBackSafely()
    {
        Assert.True(FrontedPropertyColorHelper.TryParseArgbColor("#FFFFFFFF", out var color));
        Assert.Equal(Colors.White, color);
        Assert.Equal("#FFFFFFFF", FrontedPropertyColorHelper.ToArgbString(color));
        Assert.False(FrontedPropertyColorHelper.TryParseArgbColor("not-a-color", out var fallback));
        Assert.Equal(FrontedPropertyColorHelper.FallbackColor, fallback);
    }

    [Fact]
    public void ColorEditorBufferTracksPickerColorAndCommitsHexExplicitly()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Color = "#FFFFFFFF" }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([item]) };
        viewModel.SelectDesignItem(item);
        var row = new FrontedPropertyEditorItem
        {
            PropertyName = nameof(TextFrontedControlConfig.Color),
            EditorKind = FrontedPropertyEditorKind.Color,
            Value = "#FFFFFFFF",
            EditText = "#FFFFFFFF"
        };

        row.ColorValue = Color.FromArgb(0x80, 0x11, 0x22, 0x33);

        Assert.Equal("#80112233", row.EditText);
        Assert.Equal("#FFFFFFFF", ((TextFrontedControlConfig)item.Config).Color);

        var result = viewModel.ApplyPropertyEdit(row, row.EditText);

        Assert.True(result);
        Assert.Equal("#80112233", ((TextFrontedControlConfig)item.Config).Color);
    }

    [Fact]
    public void InvalidColorCommitKeepsEditBufferAndSetsError()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Color = "#FFFFFFFF" }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([item]) };
        viewModel.SelectDesignItem(item);
        var row = new FrontedPropertyEditorItem
        {
            PropertyName = nameof(TextFrontedControlConfig.Color),
            EditorKind = FrontedPropertyEditorKind.Color,
            Value = "#FFFFFFFF",
            EditText = "bad-color"
        };

        var result = viewModel.ApplyPropertyEdit(row, row.EditText);

        Assert.False(result);
        Assert.Equal("#FFFFFFFF", ((TextFrontedControlConfig)item.Config).Color);
        Assert.Equal("bad-color", row.EditText);
        Assert.True(row.HasEditError);
    }

    [Fact]
    public void FontFamilyOptionProviderIncludesSystemAndBuiltInPackUriOptions()
    {
        var provider = new FrontedFontFamilyOptionProvider(GetRepositoryPath("neo-bpsys-wpf", "Assets", "Fonts"));

        var options = provider.GetFontFamilyOptions();

        Assert.Contains(options, option => !option.IsBuiltIn);
        Assert.Contains(
            options,
            option => option.IsBuiltIn
                      && option.Value == "pack://application:,,,/Assets/Fonts/#Noto Sans");
        Assert.Contains(
            options,
            option => option.IsBuiltIn
                      && option.Value == "pack://application:,,,/Assets/Fonts/#华康POP1体W5");
    }

    [Fact]
    public void FontFamilyOptionProviderCreatesBuiltInPreviewWithSplitPackUriLogic()
    {
        var provider = new FrontedFontFamilyOptionProvider();
        const string value = "pack://application:,,,/Assets/Fonts/#Noto Sans";

        var preview = provider.CreatePreviewFontFamily(value);

        Assert.Contains("Noto Sans", preview.Source);
        Assert.Equal("Noto Sans", provider.GetDisplayName(value));
        Assert.NotNull(provider.CreatePreviewFontFamily("not a valid font \0 string"));
    }

    [Fact]
    public void ApplyPropertyEditStoresBuiltInFontPackUriAndCustomFontRawValue()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig()
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([item])
        };
        viewModel.SelectDesignItem(item);
        const string builtInFont = "pack://application:,,,/Assets/Fonts/#Noto Sans";

        viewModel.ApplyPropertyEdit(
            new FrontedPropertyEditorItem
            {
                PropertyName = nameof(TextFrontedControlConfig.FontFamily),
                EditorKind = FrontedPropertyEditorKind.FontFamily
            },
            builtInFont);
        Assert.Equal(builtInFont, ((TextFrontedControlConfig)item.Config).FontFamily);

        viewModel.ApplyPropertyEdit(
            new FrontedPropertyEditorItem
            {
                PropertyName = nameof(TextFrontedControlConfig.FontFamily),
                EditorKind = FrontedPropertyEditorKind.FontFamily
            },
            "Custom Font Name");
        Assert.Equal("Custom Font Name", ((TextFrontedControlConfig)item.Config).FontFamily);
    }

    [Fact]
    public void FontFamilyOptionSelectionStoresOptionValueNotDisplayName()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig()
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([item])
        };
        viewModel.SelectDesignItem(item);
        var option = new FrontedFontFamilyOption
        {
            DisplayName = "Noto Sans",
            Value = "pack://application:,,,/Assets/Fonts/#Noto Sans",
            PreviewFontFamily = new FontFamily("Arial"),
            IsBuiltIn = true
        };

        viewModel.ApplyPropertyEdit(
            new FrontedPropertyEditorItem
            {
                PropertyName = nameof(TextFrontedControlConfig.FontFamily),
                EditorKind = FrontedPropertyEditorKind.FontFamily,
                Value = option.DisplayName,
                EditText = option.DisplayName
            },
            option.Value);

        Assert.Equal(option.Value, ((TextFrontedControlConfig)item.Config).FontFamily);
        Assert.NotEqual(option.DisplayName, ((TextFrontedControlConfig)item.Config).FontFamily);
    }

    [Fact]
    public void ApplyPropertyEditUpdatesTextPropertyAndMarksDocumentDirty()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Text = "Old" }
        };
        var document = CreateDocument([item]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(item);

        viewModel.ApplyPropertyEdit(
            new FrontedPropertyEditorItem
            {
                PropertyName = nameof(TextFrontedControlConfig.Text),
                EditorKind = FrontedPropertyEditorKind.Text
            },
            "New");

        Assert.Equal("New", ((TextFrontedControlConfig)item.Config).Text);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void ApplyPropertyEditClampsStaticTextAndBindingPath()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig()
        };
        var document = CreateDocument([item]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(item);

        viewModel.ApplyPropertyEdit(
            new FrontedPropertyEditorItem
            {
                PropertyName = nameof(TextFrontedControlConfig.Text),
                EditorKind = FrontedPropertyEditorKind.Text
            },
            new string('A', FrontedLayoutLimits.MaxStaticTextLength + 10));
        viewModel.ApplyPropertyEdit(
            new FrontedPropertyEditorItem
            {
                PropertyName = nameof(FrontedControlConfigBase.BindingPath),
                EditorKind = FrontedPropertyEditorKind.Text
            },
            new string('B', FrontedLayoutLimits.MaxBindingPathLength + 10));

        var config = Assert.IsType<TextFrontedControlConfig>(item.Config);
        Assert.Equal(FrontedLayoutLimits.MaxStaticTextLength, config.Text?.Length);
        Assert.Equal(FrontedLayoutLimits.MaxBindingPathLength, config.BindingPath?.Length);
    }

    [Fact]
    public void LiveGeometryChangeDoesNotRebuildPropertyGridUntilCommit()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 10, Top = 20 }
        };
        var document = CreateDocument([item]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(item);
        viewModel.PropertyEditorItems.Clear();
        viewModel.PropertyEditorItems.Add(new FrontedPropertyEditorItem { PropertyName = "Sentinel" });

        viewModel.MoveSelectedDesignItem(10, 20, 5, 6, renderPreview: false);

        Assert.Single(viewModel.PropertyEditorItems);
        Assert.Equal("Sentinel", viewModel.PropertyEditorItems[0].PropertyName);

        viewModel.CommitDesignItemGeometryEdit();

        Assert.Contains(viewModel.PropertyEditorItems, row => row.PropertyName == nameof(FrontedControlConfigBase.Left));
    }

    [Fact]
    public void ApplyPropertyEditUpdatesGeometryWithHalfStepSnap()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 10, Top = 20, Width = 100, Height = 40 }
        };
        var document = CreateDocument([item]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(item);

        viewModel.ApplyPropertyEdit(
            new FrontedPropertyEditorItem
            {
                PropertyName = nameof(FrontedControlConfigBase.Left),
                EditorKind = FrontedPropertyEditorKind.Number
            },
            "10.25");
        viewModel.ApplyPropertyEdit(
            new FrontedPropertyEditorItem
            {
                PropertyName = nameof(FrontedControlConfigBase.Width),
                EditorKind = FrontedPropertyEditorKind.Number
            },
            "0.1");

        Assert.Equal(10.5, item.Config.Left);
        Assert.Equal(1, item.Config.Width);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void ApplyPropertyEditRefusesInvalidDuplicateAndRuntimeCriticalNames()
    {
        var title = new FrontedControlDesignItem { Name = "Title", Config = new TextFrontedControlConfig() };
        var logo = new FrontedControlDesignItem { Name = "Logo", Config = new ImageFrontedControlConfig() };
        var runtimeCritical = new FrontedControlDesignItem
        {
            Name = "SurPick0",
            IsRuntimeCritical = true,
            Config = new ImageFrontedControlConfig()
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([title, logo, runtimeCritical], "BpWindow")
        };

        viewModel.SelectDesignItem(title);
        var invalidNameRow = NameEditorRow();
        var invalidResult = viewModel.ApplyPropertyEdit(invalidNameRow, "Bad.Name");
        Assert.False(invalidResult);
        Assert.Equal("Title", title.Name);
        Assert.Equal("Bad.Name", invalidNameRow.EditText);
        Assert.True(invalidNameRow.HasEditError);

        var duplicateNameRow = NameEditorRow();
        var duplicateResult = viewModel.ApplyPropertyEdit(duplicateNameRow, "Logo");
        Assert.False(duplicateResult);
        Assert.Equal("Title", title.Name);
        Assert.Equal("Logo", duplicateNameRow.EditText);
        Assert.True(duplicateNameRow.HasEditError);

        viewModel.SelectDesignItem(runtimeCritical);
        var runtimeResult = viewModel.ApplyPropertyEdit(NameEditorRow(), "SurPickA");
        Assert.False(runtimeResult);
        Assert.Equal("SurPick0", runtimeCritical.Name);
    }

    [Fact]
    public void ApplyPropertyEditValidNameUpdatesDesignItemAndClearsEditError()
    {
        var title = new FrontedControlDesignItem { Name = "Title", Config = new TextFrontedControlConfig() };
        var document = CreateDocument([title]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(title);
        var row = NameEditorRow();
        row.SetEditError("old error");

        var result = viewModel.ApplyPropertyEdit(row, "Title2");

        Assert.True(result);
        Assert.Equal("Title2", title.Name);
        Assert.True(document.IsDirty);
        Assert.False(row.HasEditError);
        Assert.DoesNotContain(viewModel.PropertyEditorItems, item => item.HasEditError);
    }

    [Fact]
    public void ApplyPropertyEditUsesEditBufferForBindingPathAndAllowsEmptyText()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { BindingPath = "Old.Path", Text = "Static" }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([item]) };
        viewModel.SelectDesignItem(item);
        var row = new FrontedPropertyEditorItem
        {
            PropertyName = nameof(FrontedControlConfigBase.BindingPath),
            EditorKind = FrontedPropertyEditorKind.Text,
            Value = "Old.Path",
            EditText = "CurrentGame.SurTeam.Name"
        };

        var result = viewModel.ApplyPropertyEdit(row, row.EditText);

        Assert.True(result);
        Assert.Equal("CurrentGame.SurTeam.Name", item.Config.BindingPath);

        row.EditText = string.Empty;
        result = viewModel.ApplyPropertyEdit(row, row.EditText);

        Assert.True(result);
        Assert.Equal(string.Empty, item.Config.BindingPath);
    }

    [Fact]
    public void BindingBrowserProviderContainsCommonDesignerPaths()
    {
        var provider = new FrontedBindingBrowserProvider();
        var paths = provider.BuildTree()
            .SelectMany(node => node.Flatten())
            .Select(node => node.FullPath)
            .Where(path => path is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CurrentGame.SurTeam.Name", paths);
        Assert.Contains("CurrentGame.HunTeam.Name", paths);
        Assert.Contains("CurrentGame.SurPlayerList[0].Member.Name", paths);
        Assert.Contains("CurrentGame.SurPlayerList[3].Member.Name", paths);
        Assert.Contains("CurrentGame.HunPlayer.Member.Name", paths);
        Assert.Contains("CurrentGame.MatchScore.CurrentSurTeamMajorText", paths);
        Assert.Contains("RemainingSeconds", paths);
    }

    [Fact]
    public void BindingBrowserTreeNodesPreserveRuntimeValueTypes()
    {
        var provider = new FrontedBindingBrowserProvider();
        var nodes = FlattenBindingTree(provider.BuildTree())
            .Where(node => !string.IsNullOrWhiteSpace(node.FullPath))
            .ToDictionary(node => node.FullPath!, StringComparer.Ordinal);

        Assert.Equal(typeof(string), nodes["CurrentGame.SurTeam.Name"].ValueType);
        Assert.True(typeof(ImageSource).IsAssignableFrom(nodes["CurrentGame.SurTeam.Logo"].ValueType));
        Assert.Equal(typeof(GameProgress), nodes["CurrentGame.GameProgress"].ValueType);
        Assert.Equal(typeof(Map?), nodes["CurrentGame.PickedMap"].ValueType);
    }

    [Fact]
    public void BindingBrowserTextFilterIncludesOnlyTextCompatiblePaths()
    {
        var provider = new FrontedBindingBrowserProvider();
        var paths = BindingSearchPaths(provider, FrontedBindingTypeFilter.Text);

        Assert.Contains("CurrentGame.SurTeam.Name", paths);
        Assert.Contains("CurrentGame.MatchScore.CurrentSurTeamMajorText", paths);
        Assert.Contains("CurrentGame.MatchScore.HomeTotalMinorScore", paths);
        Assert.DoesNotContain("CurrentGame.SurTeam.Logo", paths);
        Assert.DoesNotContain("CurrentGame.PickedMapImage", paths);
        Assert.DoesNotContain("CurrentGame.GameProgress", paths);
        Assert.DoesNotContain("CurrentGame.PickedMap", paths);
    }

    [Fact]
    public void BindingBrowserImageFilterIncludesOnlyImageCompatiblePaths()
    {
        var provider = new FrontedBindingBrowserProvider();
        var paths = BindingSearchPaths(provider, FrontedBindingTypeFilter.Image);

        Assert.Contains("CurrentGame.SurTeam.Logo", paths);
        Assert.Contains("CurrentGame.PickedMapImage", paths);
        Assert.Contains("CurrentGame.SurPlayerList[0].PictureShown", paths);
        Assert.DoesNotContain("CurrentGame.SurTeam.Name", paths);
        Assert.DoesNotContain("CurrentGame.MatchScore.CurrentSurTeamMajorText", paths);
        Assert.DoesNotContain("CurrentGame.GameProgress", paths);
        Assert.DoesNotContain("CurrentGame.PickedMap", paths);
    }

    [Fact]
    public void BindingBrowserGameProgressFilterIncludesOnlyGameProgressPaths()
    {
        var provider = new FrontedBindingBrowserProvider();
        var paths = BindingSearchPaths(provider, FrontedBindingTypeFilter.GameProgress);

        Assert.Contains("CurrentGame.GameProgress", paths);
        Assert.DoesNotContain("CurrentGame.SurTeam.Name", paths);
        Assert.DoesNotContain("CurrentGame.SurTeam.Logo", paths);
        Assert.DoesNotContain("CurrentGame.PickedMap", paths);
    }

    [Fact]
    public void BindingBrowserMapFilterIncludesOnlyMapPaths()
    {
        var provider = new FrontedBindingBrowserProvider();
        var paths = BindingSearchPaths(provider, FrontedBindingTypeFilter.Map);

        Assert.Contains("CurrentGame.PickedMap", paths);
        Assert.Contains("CurrentGame.BannedMap", paths);
        Assert.DoesNotContain("CurrentGame.SurTeam.Name", paths);
        Assert.DoesNotContain("CurrentGame.PickedMapImage", paths);
        Assert.DoesNotContain("CurrentGame.GameProgress", paths);
    }

    [Fact]
    public void BindingBrowserProviderSearchFindsPartialNamesAndHasNoDuplicatePaths()
    {
        var provider = new FrontedBindingBrowserProvider();

        var results = provider.Search("SurTeam");
        var allPaths = provider.BuildTree()
            .SelectMany(node => node.Flatten())
            .Where(node => !string.IsNullOrWhiteSpace(node.FullPath))
            .Select(node => node.FullPath!)
            .ToArray();

        Assert.Contains(results, node => node.FullPath == "CurrentGame.SurTeam.Name");
        Assert.Equal(allPaths.Length, allPaths.Distinct(StringComparer.Ordinal).Count());
        Assert.True(allPaths.Length < 400);
    }

    [Fact]
    public void BindingBrowserSearchRespectsTypeFilter()
    {
        var provider = new FrontedBindingBrowserProvider();

        Assert.Empty(provider.Search("Logo", FrontedBindingTypeFilter.Text));
        Assert.Contains(provider.Search("Logo", FrontedBindingTypeFilter.Image), node => node.FullPath == "CurrentGame.SurTeam.Logo");
        Assert.DoesNotContain(provider.Search("Name", FrontedBindingTypeFilter.Image), node => node.ValueType == typeof(string));
    }

    [Fact]
    public void PropertyGridMarksBindingAndResourcePathRows()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "SurPick",
            Config = new ImageFrontedControlConfig
            {
                BindingPath = "CurrentGame.SurPlayerList[0].PictureShown",
                PickingBorderImagePath = "Resources/pickingBorder.png"
            }
        };

        var rows = BuildPropertyRows(CreateDocument([item]), item);

        var bindingRow = Assert.Single(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.BindingPath));
        Assert.True(bindingRow.CanBrowseBinding);
        Assert.False(bindingRow.CanBrowseResource);

        var resourceRow = Assert.Single(rows, row => row.PropertyName == nameof(ImageFrontedControlConfig.PickingBorderImagePath));
        Assert.True(resourceRow.CanBrowseResource);
        Assert.False(resourceRow.CanBrowseBinding);
        Assert.Equal("Resource", resourceRow.GroupName);

        var normalTextRow = rows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.HorizontalAlignment));
        Assert.False(normalTextRow.CanBrowseBinding);
        Assert.False(normalTextRow.CanBrowseResource);
    }

    [Theory]
    [InlineData("Text", typeof(TextFrontedControlConfig), FrontedBindingTargetKind.Text)]
    [InlineData("Image", typeof(ImageFrontedControlConfig), FrontedBindingTargetKind.Image)]
    [InlineData("BorderedImage", typeof(BorderedImageFrontedControlConfig), FrontedBindingTargetKind.Image)]
    [InlineData("GameProgressText", typeof(GameProgressTextControlConfig), FrontedBindingTargetKind.GameProgress)]
    [InlineData("MapNameText", typeof(MapNameTextControlConfig), FrontedBindingTargetKind.Map)]
    public void PropertyGridBuilderSetsBindingTargetKind(
        string name,
        Type configType,
        FrontedBindingTargetKind expectedKind)
    {
        var config = (FrontedControlConfigBase)Activator.CreateInstance(configType)!;
        var item = new FrontedControlDesignItem
        {
            Name = name,
            Config = config
        };

        var rows = BuildPropertyRows(CreateDocument([item]), item);
        var bindingRow = Assert.Single(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.BindingPath));

        Assert.True(bindingRow.CanBrowseBinding);
        Assert.Equal(expectedKind, bindingRow.BindingTargetKind);
    }

    [Fact]
    public void BindingBrowserWindowViewModelInitializedWithImageFilterOnlyExposesImages()
    {
        var viewModel = new FrontedBindingBrowserWindowViewModel(
            new FrontedBindingBrowserProvider(),
            FrontedBindingTypeFilter.Image);

        Assert.Contains(viewModel.SearchResults, node => node.FullPath == "CurrentGame.SurTeam.Logo");
        Assert.Contains(viewModel.SearchResults, node => node.FullPath == "CurrentGame.PickedMapImage");
        Assert.DoesNotContain(viewModel.SearchResults, node => node.FullPath == "CurrentGame.SurTeam.Name");

        viewModel.SearchText = "Name";

        Assert.DoesNotContain(viewModel.SearchResults, node => node.ValueType == typeof(string));
    }

    [Fact]
    public void BrowserSelectionOnlyUpdatesEditTextUntilExplicitApply()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { BindingPath = "Old.Path" }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([item]) };
        viewModel.SelectDesignItem(item);
        var row = viewModel.PropertyEditorItems.Single(row => row.PropertyName == nameof(FrontedControlConfigBase.BindingPath));

        row.EditText = "CurrentGame.SurTeam.Name";

        Assert.Equal("Old.Path", item.Config.BindingPath);
        Assert.False(viewModel.CanUndo);

        var result = viewModel.ApplyPropertyEdit(row, row.EditText);

        Assert.True(result);
        Assert.Equal("CurrentGame.SurTeam.Name", item.Config.BindingPath);
        Assert.True(viewModel.CanUndo);
    }

    [Fact]
    public void ResourceBrowserProviderListsBuiltInResourcesWithResolverPathConvention()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var bpui = Path.Combine(root, "bpui");
        Directory.CreateDirectory(bpui);
        var imagePath = Path.Combine(bpui, "sample.png");
        File.WriteAllText(imagePath, "not a real image");

        try
        {
            var provider = new FrontedResourceBrowserProvider(root);
            var resources = provider.ListBuiltInResources();

            var item = Assert.Single(resources);
            Assert.Equal("sample.png", item.DisplayName);
            Assert.Equal("Resources/sample.png", item.SelectedPath);
            Assert.Null(item.Thumbnail);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResourceBrowserProviderAcceptsAbsoluteFileItem()
    {
        var provider = new FrontedResourceBrowserProvider();
        var path = Path.Combine(Path.GetTempPath(), "designer-resource.png");

        var item = provider.CreateAbsoluteFileItem(path);

        Assert.Equal(path, item.SelectedPath);
        Assert.True(item.IsAbsoluteFile);
        Assert.Equal("AbsoluteFile", item.Category);
    }

    [Fact]
    public void FrontedDesignerWindowXamlContainsBrowserButtonHandlers()
    {
        var xamlPath = GetRepositoryPath("neo-bpsys-wpf", "Views", "Windows", "FrontedDesignerWindow.xaml");
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("BrowseBindingButton_OnClick", xaml, StringComparison.Ordinal);
        Assert.Contains("BrowseResourceButton_OnClick", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontedDesignerToolbarUsesWrappingAndTrimsLongLayoutPath()
    {
        var xaml = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml"));

        Assert.Contains("<ScrollViewer", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"120\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<WrapPanel Orientation=\"Horizontal\">", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"260\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding LayoutSourcePath}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsReadOnly=\"True\"\r\n                        Text=\"{Binding LayoutSourcePath}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<Menu", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MessageBoxHelperSupportsWidthSafeThreeOptionPrompt()
    {
        var code = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf.Core",
            "Helpers",
            "MessageBoxHelper.cs"));

        Assert.Contains("ShowThreeOptionAsync", code, StringComparison.Ordinal);
        Assert.Contains("PrimaryButtonText = primaryButtonText", code, StringComparison.Ordinal);
        Assert.Contains("SecondaryButtonText = secondaryButtonText", code, StringComparison.Ordinal);
        Assert.Contains("CloseButtonText = closeButtonText", code, StringComparison.Ordinal);
        Assert.Contains("messageBox.Width = width.Value", code, StringComparison.Ordinal);
        Assert.Contains("messageBox.MinWidth = minWidth.Value", code, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontedDesignerCloseFlowCancelsClosingBeforeShowingDirtyPrompt()
    {
        var code = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml.cs"));

        Assert.Contains("private void OnClosing", code, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true;", code, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.BeginInvoke(", code, StringComparison.Ordinal);
        Assert.Contains("PromptDirtyCloseAfterCancelAsync", code, StringComparison.Ordinal);
        Assert.Contains("MessageBoxHelper.ShowThreeOptionAsync", code, StringComparison.Ordinal);
        Assert.Contains("_forceCloseAfterDirtyPrompt = true;", code, StringComparison.Ordinal);
        Assert.Contains("_isDirtyClosePromptOpen", code, StringComparison.Ordinal);
        Assert.Contains("CloseValidationDetailsWindowSafely();", code, StringComparison.Ordinal);
        Assert.Contains("catch (InvalidOperationException ex)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("FrontedDesignerDirtyPromptWindow", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TextPropertyEditFailureKeepsEditBufferAndSetsErrorState()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 10 }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([item]) };
        viewModel.SelectDesignItem(item);
        var row = new FrontedPropertyEditorItem
        {
            PropertyName = nameof(FrontedControlConfigBase.Left),
            EditorKind = FrontedPropertyEditorKind.Number,
            Value = "10",
            EditText = "not-a-number"
        };

        var result = viewModel.ApplyPropertyEdit(row, row.EditText);

        Assert.False(result);
        Assert.Equal(10, item.Config.Left);
        Assert.Equal("not-a-number", row.EditText);
        Assert.True(row.HasEditError);
        Assert.NotEmpty(row.ValidationErrors);
    }

    [Fact]
    public void ApplyPropertyEditBlocksReferencedControlRename()
    {
        var target = new FrontedControlDesignItem
        {
            Name = "Target",
            Config = new ImageFrontedControlConfig()
        };
        var overlay = new FrontedControlDesignItem
        {
            Name = "TargetOverlay",
            IsSelectableInEditor = false,
            IsEditableInEditor = false,
            Config = new PickingBorderOverlayControlConfig { TargetControlName = "Target" }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([target, overlay])
        };
        viewModel.SelectDesignItem(target);

        viewModel.ApplyPropertyEdit(NameEditorRow(), "Target2");

        Assert.Equal("Target", target.Name);
    }

    [Fact]
    public void ApplyPropertyEditSyncsLinkedPickingBorderOverlayGeometry()
    {
        var target = new FrontedControlDesignItem
        {
            Name = "SurPick0",
            Config = new ImageFrontedControlConfig { Left = 10, Top = 20, Width = 141, Height = 160 }
        };
        var overlay = new FrontedControlDesignItem
        {
            Name = "SurPickingBorder0",
            IsSelectableInEditor = false,
            IsEditableInEditor = false,
            IsLinkedOverlay = true,
            Config = new PickingBorderOverlayControlConfig
            {
                TargetControlName = "SurPick0",
                Left = 0,
                Top = 0,
                Width = 1,
                Height = 1
            }
        };
        var document = CreateDocument([target, overlay]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(target);

        viewModel.ApplyPropertyEdit(
            new FrontedPropertyEditorItem
            {
                PropertyName = nameof(FrontedControlConfigBase.Height),
                EditorKind = FrontedPropertyEditorKind.Number
            },
            "200.25");

        var overlayConfig = Assert.IsType<PickingBorderOverlayControlConfig>(overlay.Config);
        Assert.Equal(target.Config.Left, overlayConfig.Left);
        Assert.Equal(target.Config.Top, overlayConfig.Top);
        Assert.Equal(target.Config.Width, overlayConfig.Width);
        Assert.Equal(200.5, overlayConfig.Height);
    }

    [Fact]
    public void PropertyGridBuilderTreatsPickingBorderOverlayAsReadOnlyIfSelectedProgrammatically()
    {
        var overlay = new FrontedControlDesignItem
        {
            Name = "SurPickingBorder0",
            IsSelectableInEditor = false,
            IsEditableInEditor = false,
            IsLinkedOverlay = true,
            Config = new PickingBorderOverlayControlConfig { TargetControlName = "SurPick0" }
        };

        var rows = BuildPropertyRows(CreateDocument([overlay]), overlay);

        Assert.All(rows, row => Assert.True(row.IsReadOnly));
    }

    [Fact]
    public void DesignerViewModelZoomByWheelDeltaAppliesManualZoom()
    {
        var viewModel = new FrontedDesignerWindowViewModel();
        viewModel.UpdateFitZoom(720, 405, 1440, 810);

        viewModel.ZoomByWheelDelta(120);

        Assert.Equal(0.55D, viewModel.ZoomScale, precision: 3);
        Assert.False(viewModel.IsFitMode);

        viewModel.ZoomByWheelDelta(-120);

        Assert.Equal(0.5D, viewModel.ZoomScale, precision: 3);
    }

    [Fact]
    public void DesignerViewModelFitZoomUsesViewportAndCanvasSize()
    {
        Assert.Equal(
            0.5D,
            FrontedDesignerWindowViewModel.CalculateFitZoom(720, 405, 1440, 810),
            precision: 3);
        Assert.Equal(
            1D,
            FrontedDesignerWindowViewModel.CalculateFitZoom(1440, 810, 1440, 810),
            precision: 3);
        Assert.True(FrontedDesignerWindowViewModel.CalculateFitZoom(1, 1, 1440, 810) > 0D);
    }

    [Fact]
    public void DesignerViewModelManualWheelZoomClampsAndExitsFitMode()
    {
        var viewModel = new FrontedDesignerWindowViewModel
        {
            ZoomScale = 2D
        };

        viewModel.ZoomByWheelDelta(120);

        Assert.Equal(2D, viewModel.ZoomScale, precision: 3);
        Assert.False(viewModel.IsFitMode);

        viewModel.ZoomScale = 0.25D;
        viewModel.ZoomByWheelDelta(-120);

        Assert.Equal(0.25D, viewModel.ZoomScale, precision: 3);
        Assert.False(viewModel.IsFitMode);
    }

    [Fact]
    public void DesignerBoundsResolverPrefersExplicitSizeThenActualThenFallback()
    {
        var explicitConfig = new TextFrontedControlConfig { Width = 100, Height = 50 };
        var actualConfig = new TextFrontedControlConfig();
        var fallbackConfig = new TextFrontedControlConfig();

        Assert.Equal((100, 50), ToSize(FrontedDesignerBoundsResolver.Resolve(explicitConfig, 200, 80)));
        Assert.Equal((200, 80), ToSize(FrontedDesignerBoundsResolver.Resolve(actualConfig, 200, 80)));
        Assert.Equal(
            (FrontedDesignerGeometryHelper.MinHitWidth, FrontedDesignerGeometryHelper.MinHitHeight),
            ToSize(FrontedDesignerBoundsResolver.Resolve(fallbackConfig)));
    }

    [Fact]
    public void DesignerInteractionHelperKeepsSelectionStableUntilSingleClick()
    {
        Assert.Equal(
            FrontedDesignerPointerAction.WaitForClick,
            FrontedDesignerInteractionHelper.ResolvePointerAction(
                thresholdExceeded: false,
                candidateIsSelected: false,
                isDraggingSelected: false));
        Assert.Equal(
            FrontedDesignerPointerAction.IgnoreUnselectedDrag,
            FrontedDesignerInteractionHelper.ResolvePointerAction(
                thresholdExceeded: true,
                candidateIsSelected: false,
                isDraggingSelected: false));
        Assert.Equal(
            FrontedDesignerPointerAction.BeginDragSelected,
            FrontedDesignerInteractionHelper.ResolvePointerAction(
                thresholdExceeded: true,
                candidateIsSelected: true,
                isDraggingSelected: false));
        Assert.Equal(
            FrontedDesignerPointerAction.DragSelected,
            FrontedDesignerInteractionHelper.ResolvePointerAction(
                thresholdExceeded: true,
                candidateIsSelected: true,
                isDraggingSelected: true));
    }

    [Fact]
    public void DesignerEditorZIndexAndAdornerConstantsMatchLightweightSelection()
    {
        var normalZIndex = FrontedDesignerEditorVisualHelper.GetHitboxZIndex(10, 0, isSelected: false);
        var selectedZIndex = FrontedDesignerEditorVisualHelper.GetHitboxZIndex(0, 0, isSelected: true);

        Assert.True(selectedZIndex > normalZIndex);
        Assert.True(FrontedDesignerEditorVisualHelper.SelectionBorderThickness <= 1);
        Assert.True(FrontedDesignerEditorVisualHelper.HandleVisualSize <= 6);
    }

    [Fact]
    public void FrontedDesignerLocalizationKeysExistInAllResxFiles()
    {
        var expectedKeys = new[]
        {
            "OpenFrontedDesigner",
            "FrontedDesigner",
            "FrontedDesignerWindow",
            "Canvas",
            "LayoutSource",
            "Validation",
            "Errors",
            "Warnings",
            "Infos",
            "ReloadLayout",
            "ValidateLayout",
            "BuiltInLayout",
            "UserLayout",
            "MissingLayout",
            "Zoom",
            "ZoomIn",
            "ZoomOut",
            "FitToWindow",
            "Fit",
            "Preview",
            "SelectedControl",
            "NoControlSelected",
            "RuntimeCriticalControl",
            "ValidationMessages",
            "ControlsList",
            "FilterControls",
            "ZIndexShort",
            "ControlType",
            "NoControlsFound",
            "Properties",
            "Property",
            "Value",
            "Identity",
            "Layout",
            "Binding",
            "Appearance",
            "ControlSpecific",
            "ReadOnly",
            "RuntimeCritical",
            "InvalidControlName",
            "DuplicateControlName",
            "ReferencedControlRenameBlocked",
            "PropertyValidationErrors",
            "NoSelectedControl",
            "EditProperty",
            "Color",
            "ValidationDetails",
            "OpenValidationDetails",
            "AddControl",
            "BasicControls",
            "BusinessControls",
            "ScoreBpControls",
            "FontFamily",
            "BuiltInFont",
            "SystemFont",
            "Placeholder",
            "AddedControl",
            "CannotAddControl",
            "UnsupportedControlType",
            "DeleteControl",
            "DeleteSelectedControl",
            "CannotDeleteRuntimeCriticalControl",
            "CannotDeleteReferencedControl",
            "ConfirmDeleteControl",
            "Undo",
            "Redo",
            "CannotUndo",
            "CannotRedo",
            "PlaceholderPreview",
            "DesignerPlaceholderData",
            "ApplyColor",
            "HexColor",
            "SaveLayout",
            "ResetToBuiltIn",
            "OpenLayoutFolder",
            "Unsaved",
            "UnsavedChanges",
            "UnsavedChangesMessage",
            "LayoutSourceUser",
            "LayoutSourceBuiltIn",
            "LayoutSourceError",
            "SaveBeforeSwitch",
            "SaveBeforeClose",
            "DiscardChanges",
            "ResetLayoutConfirm",
            "LayoutSaved",
            "LayoutSaveFailed",
            "CannotSaveInvalidLayout",
            "Snap",
            "SnapOn",
            "SnapOff",
            "TemporarySnap",
            "SnapGridSize"
        };

        foreach (var fileName in new[] { "Lang.resx", "Lang.en-us.resx", "Lang.ja-jp.resx" })
        {
            var names = XDocument.Load(GetRepositoryPath("neo-bpsys-wpf", "Locales", fileName))
                .Root!
                .Elements("data")
                .Select(element => element.Attribute("name")?.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var key in expectedKeys)
            {
                Assert.Contains(key, names);
            }
        }
    }

    [Fact]
    public void FrontManagePageViewModelExposesOpenFrontedDesignerCommand()
    {
        var text = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "ViewModels",
            "Pages",
            "FrontManagePageViewModel.cs"));

        Assert.Contains("OpenFrontedDesigner", text);
        Assert.Contains("[RelayCommand]", text);
    }

    [Fact]
    public void SettingPageNoLongerContainsFrontedDesignerEntry()
    {
        var text = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Pages",
            "SettingPage.xaml"));

        Assert.DoesNotContain("OpenFrontedDesignerCommand", text);
    }

    [Fact]
    public void FrontedDesignerWindowUsesProjectShellAndZoomHostPreview()
    {
        var text = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml"));

        Assert.Contains("<ui:FluentWindow", text);
        Assert.Contains("controls:CustomTitleBar", text);
        Assert.Contains("IsThemeChangeVisible=\"False\"", text);
        Assert.DoesNotContain("<Viewbox", text);
        Assert.DoesNotContain("MaxWidth=\"{Binding ActualWidth, ElementName=PreviewScrollViewer}\"", text);
        Assert.Contains("x:Name=\"PreviewScrollViewer\"", text);
        Assert.Contains("x:Name=\"PreviewZoomHost\"", text);
        Assert.Contains("ScaleX=\"{Binding ZoomScale}\"", text);
        Assert.Contains("SetZoomPresetCommand", text);
        Assert.Contains("FitToWindowCommand", text);
        Assert.Contains("InteractionLayer", text);
        Assert.Contains("DesignSurfaceGrid", text);
        Assert.Contains("PropertyEditorContentControlStyle", text);
        Assert.Contains("OpenValidationDetails_OnClick", text);
        Assert.Contains("AddControlButton_OnClick", text);
        Assert.Contains("AddControlMenuItem_OnClick", text);
        Assert.Contains("DeleteSelectedControlCommand", text);
        Assert.Contains("UndoCommand", text);
        Assert.Contains("RedoCommand", text);
        Assert.Contains("ControlListItem_OnPreviewMouseRightButtonDown", text);
        Assert.DoesNotContain("DeleteControlMenuItem_OnClick", text);
        Assert.Contains("ApplyColor", text);
        Assert.Contains("GridSplitter", text);
        Assert.Contains("ResizeDirection=\"Columns\"", text);
        Assert.Contains("Grid.Column=\"3\"", text);
        Assert.Contains("PropertyFontFamilyEditorTemplate", text);
        Assert.Contains("PropertyFontComboBox_OnSelectionChanged", text);
        Assert.Contains("DropDownClosed=\"PropertyFontComboBox_OnDropDownClosed\"", text);
        Assert.Contains("Text=\"{Binding EditText", text);
        Assert.Contains("HasEditError", text);
        Assert.DoesNotContain("ItemsSource=\"{Binding ValidationMessages}\"", text);
        Assert.Contains("ListBox.ContextMenu", text);
        Assert.Contains("x:Name=\"ControlsListBox\"", text);
        Assert.Contains("Binding DeleteSelectedControlCommand", text);
        Assert.DoesNotContain("Setter Property=\"ContextMenu\">", text);
    }

    [Fact]
    public void FrontedDesignerViewModelDefaultsZoomToFit()
    {
        var text = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "ViewModels",
            "Windows",
            "FrontedDesignerWindowViewModel.cs"));

        Assert.Contains("FrontedDesignerZoomPreset(\"Fit\"", text);
        Assert.Contains("private bool _isFitMode = true", text);
        Assert.Contains("CalculateFitZoom", text);
        Assert.Contains("private string _zoomDisplay = \"Fit\"", text);
    }

    [Fact]
    public async Task FrontedUserLayoutStoreSavesLoadsAndDeletesExpectedPath()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new FrontedUserLayoutStore(root);
            var config = new FrontedCanvasConfig
            {
                CanvasWidth = 1440,
                CanvasHeight = 810,
                Controls =
                {
                    ["Title"] = new TextFrontedControlConfig { Text = "Saved" }
                }
            };

            await store.SaveAsync("BpWindow", "BaseCanvas", config, TestContext.Current.CancellationToken);

            var expectedPath = Path.Combine(root, "BpWindow", "BaseCanvas.json");
            Assert.Equal(expectedPath, store.GetLayoutPath("BpWindow", "BaseCanvas"));
            Assert.Equal(Path.Combine(root, "BpWindow"), store.GetLayoutFolder("BpWindow", "BaseCanvas"));
            Assert.Equal(root, store.GetRootFolder());
            Assert.True(store.Exists("BpWindow", "BaseCanvas"));

            var loaded = await store.LoadAsync("BpWindow", "BaseCanvas", TestContext.Current.CancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(3, loaded.Version);
            Assert.True(loaded.Controls.ContainsKey("Title"));
            Assert.Contains("\"Title\"", File.ReadAllText(expectedPath));

            await store.DeleteAsync("BpWindow", "BaseCanvas", TestContext.Current.CancellationToken);
            Assert.False(store.Exists("BpWindow", "BaseCanvas"));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task FrontedLayoutServicePrefersUserLayoutOverBuiltIn()
    {
        var root = CreateTempDirectory();
        try
        {
            var userStore = new FrontedUserLayoutStore(Path.Combine(root, "user"));
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInLayout(builtInRoot, "BpWindow", "BaseCanvas", new FrontedCanvasConfig
            {
                CanvasWidth = 100,
                CanvasHeight = 50,
                Controls =
                {
                    ["BuiltInText"] = new TextFrontedControlConfig { Text = "Built-in" }
                }
            });
            await userStore.SaveAsync("BpWindow", "BaseCanvas", new FrontedCanvasConfig
            {
                CanvasWidth = 200,
                CanvasHeight = 100,
                Controls =
                {
                    ["UserText"] = new TextFrontedControlConfig { Text = "User" }
                }
            }, TestContext.Current.CancellationToken);

            var service = new FrontedLayoutService(userStore, builtInRoot, logger: null);
            var result = await service.LoadCanvasConfigWithMetadataAsync(
                "BpWindow",
                "BaseCanvas",
                TestContext.Current.CancellationToken);

            Assert.Equal(FrontedLayoutSource.User, result.Source);
            Assert.Equal(200, result.Config?.CanvasWidth);
            Assert.True(result.Config?.Controls.ContainsKey("UserText"));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task FrontedLayoutServiceFallsBackToBuiltInWhenUserMissingOrInvalid()
    {
        var root = CreateTempDirectory();
        try
        {
            var userStore = new FrontedUserLayoutStore(Path.Combine(root, "user"));
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInLayout(builtInRoot, "BpWindow", "BaseCanvas", new FrontedCanvasConfig
            {
                CanvasWidth = 100,
                CanvasHeight = 50,
                Controls =
                {
                    ["BuiltInText"] = new TextFrontedControlConfig { Text = "Built-in" }
                }
            });
            var service = new FrontedLayoutService(userStore, builtInRoot, logger: null);

            var missingUserResult = await service.LoadCanvasConfigWithMetadataAsync(
                "BpWindow",
                "BaseCanvas",
                TestContext.Current.CancellationToken);
            Assert.Equal(FrontedLayoutSource.BuiltIn, missingUserResult.Source);

            Directory.CreateDirectory(userStore.GetLayoutFolder("BpWindow", "BaseCanvas"));
            File.WriteAllText(userStore.GetLayoutPath("BpWindow", "BaseCanvas"), "{ invalid json");
            var invalidUserResult = await service.LoadCanvasConfigWithMetadataAsync(
                "BpWindow",
                "BaseCanvas",
                TestContext.Current.CancellationToken);

            Assert.Equal(FrontedLayoutSource.BuiltIn, invalidUserResult.Source);
            Assert.Equal(100, invalidUserResult.Config?.CanvasWidth);
            Assert.NotNull(invalidUserResult.Error);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task SaveCurrentLayoutRefusesValidationErrors()
    {
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = new FrontedCanvasDesignDocument
            {
                WindowTypeName = "BpWindow",
                CanvasName = "BaseCanvas",
                CanvasConfig = new FrontedCanvasConfig
                {
                    Version = 3,
                    CanvasWidth = 0,
                    CanvasHeight = 810
                }
            }
        };

        var saved = await viewModel.SaveCurrentLayoutAsync();

        Assert.False(saved);
        Assert.True(viewModel.ErrorCount > 0);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
    }

    [Fact]
    public void SnapEffectiveStateAndStatusFollowToggleAndShiftSeparately()
    {
        var viewModel = new FrontedDesignerWindowViewModel();

        Assert.False(viewModel.EffectiveSnapEnabled);
        Assert.False(viewModel.SnapEnabled);

        viewModel.UpdateShiftSnapActive(true);

        Assert.True(viewModel.EffectiveSnapEnabled);
        Assert.False(viewModel.SnapEnabled);
        Assert.Equal(neo_bpsys_wpf.Helpers.I18nHelper.GetLocalizedString("TemporarySnap"), viewModel.SnapStatusText);

        viewModel.SnapEnabled = true;
        viewModel.UpdateShiftSnapActive(false);

        Assert.True(viewModel.EffectiveSnapEnabled);
        Assert.True(viewModel.SnapEnabled);
        Assert.Equal(neo_bpsys_wpf.Helpers.I18nHelper.GetLocalizedString("SnapOn"), viewModel.SnapStatusText);
    }

    [Fact]
    public void DesignerGeometryHelperNormalizesFreeAndSnapCoordinates()
    {
        Assert.Equal(10, FrontedDesignerGeometryHelper.NormalizeCoordinate(10.24));
        Assert.Equal(10.5, FrontedDesignerGeometryHelper.NormalizeCoordinate(10.25));
        Assert.Equal(20, FrontedDesignerGeometryHelper.NormalizeCoordinate(15.1, true, 10));
    }

    [Fact]
    public void DesignerGeometryHelperMoveAndResizeUseEffectiveSnapOnly()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Image",
            Config = new ImageFrontedControlConfig
            {
                Left = 10,
                Top = 20,
                Width = 50,
                Height = 40
            }
        };

        FrontedDesignerGeometryHelper.Move(item, 10, 20, 2.2, 2.2, effectiveSnapEnabled: false);
        Assert.Equal(12, item.Config.Left);
        Assert.Equal(22, item.Config.Top);

        FrontedDesignerGeometryHelper.Move(item, 10, 20, 6, 6, effectiveSnapEnabled: true, snapGridSize: 10);
        Assert.Equal(20, item.Config.Left);
        Assert.Equal(30, item.Config.Top);

        FrontedDesignerGeometryHelper.Resize(
            item,
            FrontedDesignerResizeHandleKind.BottomRight,
            10,
            20,
            53,
            44,
            4,
            7,
            effectiveSnapEnabled: true,
            snapGridSize: 10);
        Assert.Equal(60, item.Config.Width);
        Assert.Equal(50, item.Config.Height);
    }

    [Fact]
    public void CanvasPropertiesSizeEditValidatesAndMarksDirty()
    {
        var document = CreateDocument([]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        Assert.False(viewModel.ApplyCanvasSizeEdit("0", "810"));
        Assert.Equal(1440, document.CanvasConfig.CanvasWidth);

        Assert.True(viewModel.ApplyCanvasSizeEdit("1920", "1080"));
        Assert.Equal(1920, document.CanvasConfig.CanvasWidth);
        Assert.Equal(1080, document.CanvasConfig.CanvasHeight);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void CanvasPropertiesBackgroundEditAndClearAreUndoable()
    {
        var document = CreateDocument([]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        Assert.True(viewModel.ApplyCanvasBackgroundEdit("Resources/bg.png"));
        Assert.Equal("Resources/bg.png", document.CanvasConfig.BackgroundImage);
        Assert.True(document.IsDirty);
        Assert.DoesNotContain(document.Controls, item => item.Name.Contains("Background", StringComparison.OrdinalIgnoreCase));

        viewModel.UndoCommand.Execute(null);
        Assert.True(string.IsNullOrEmpty(viewModel.CurrentDocument?.CanvasConfig.BackgroundImage));

        Assert.True(viewModel.ApplyCanvasBackgroundEdit("Resources/bg.png"));
        Assert.True(viewModel.ClearCanvasBackground());
        Assert.True(string.IsNullOrEmpty(viewModel.CurrentDocument?.CanvasConfig.BackgroundImage));
    }

    private static FrontedCanvasDesignDocument CreateDocument(
        IList<FrontedControlDesignItem> controls,
        string windowTypeName = "TestWindow")
    {
        return new FrontedCanvasDesignDocument
        {
            WindowTypeName = windowTypeName,
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

    private static IReadOnlyList<FrontedPropertyEditorItem> BuildPropertyRows(
        FrontedCanvasDesignDocument document,
        FrontedControlDesignItem item,
        IFrontedDesignerLocalizationService? localizationService = null)
    {
        var builder = localizationService is null
            ? new FrontedPropertyGridBuilder()
            : new FrontedPropertyGridBuilder(new FrontedFontFamilyOptionProvider(), localizationService);

        return builder.Build(
            document,
            item,
            CreateValidator(),
            new FrontedLayoutReferenceScanner(),
            new FrontedLayoutRuntimeContractCatalog());
    }

    private static IReadOnlyList<Type> BuiltInConfigTypes() =>
    [
        typeof(FrontedControlConfigBase),
        typeof(TextFrontedControlConfig),
        typeof(LocalizedTextControlConfig),
        typeof(ImageFrontedControlConfig),
        typeof(BorderedImageFrontedControlConfig),
        typeof(GameProgressTextControlConfig),
        typeof(MapNameTextControlConfig),
        typeof(TalentTraitDisplayControlConfig),
        typeof(GlobalScoreRowControlConfig),
        typeof(CurrentBanDisplayControlConfig),
        typeof(BanSlotDisplayControlConfig),
        typeof(MapV2DisplayControlConfig),
        typeof(PickingBorderOverlayControlConfig)
    ];

    private static HashSet<string> LoadResxKeys(string fileName)
    {
        return XDocument.Load(GetRepositoryPath("neo-bpsys-wpf", "Locales", fileName))
            .Root!
            .Elements("data")
            .Select(element => element.Attribute("name")?.Value)
            .Where(name => name is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed class TestDesignerLocalizationService : FrontedDesignerLocalizationService
    {
        private readonly IReadOnlyDictionary<string, string> _propertyNames;
        private readonly IReadOnlyDictionary<string, string> _groupNames;
        private readonly IReadOnlyDictionary<string, string> _options;
        private readonly IReadOnlyDictionary<string, string> _controlTypes;
        private readonly IReadOnlyDictionary<string, string> _windows;
        private readonly IReadOnlyDictionary<string, string> _canvases;
        private readonly IReadOnlyDictionary<string, string> _bindings;

        public TestDesignerLocalizationService(
            IReadOnlyDictionary<string, string>? propertyNames = null,
            IReadOnlyDictionary<string, string>? groupNames = null,
            IReadOnlyDictionary<string, string>? options = null,
            IReadOnlyDictionary<string, string>? controlTypes = null,
            IReadOnlyDictionary<string, string>? windows = null,
            IReadOnlyDictionary<string, string>? canvases = null,
            IReadOnlyDictionary<string, string>? bindings = null)
        {
            _propertyNames = propertyNames ?? new Dictionary<string, string>();
            _groupNames = groupNames ?? new Dictionary<string, string>();
            _options = options ?? new Dictionary<string, string>();
            _controlTypes = controlTypes ?? new Dictionary<string, string>();
            _windows = windows ?? new Dictionary<string, string>();
            _canvases = canvases ?? new Dictionary<string, string>();
            _bindings = bindings ?? new Dictionary<string, string>();
        }

        public override string GetPropertyDisplayName(string propertyName) =>
            _propertyNames.GetValueOrDefault(propertyName, propertyName);

        public override string GetGroupDisplayName(string groupName) =>
            _groupNames.GetValueOrDefault(groupName, groupName);

        public override string GetOptionDisplayName(string propertyName, object? value)
        {
            var rawValue = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            return _options.GetValueOrDefault($"{propertyName}.{rawValue}", rawValue);
        }

        public override string GetControlTypeDisplayName(string controlType) =>
            _controlTypes.GetValueOrDefault(controlType, controlType);

        public override string GetWindowDisplayName(string windowTypeName) =>
            _windows.GetValueOrDefault(windowTypeName, windowTypeName);

        public override string GetCanvasDisplayName(string canvasName) =>
            _canvases.GetValueOrDefault(canvasName, canvasName);

        public override string GetBindingNodeDisplayName(string pathOrPropertyName, string? fullPath = null) =>
            fullPath is not null && _bindings.TryGetValue(fullPath, out var displayName)
                ? displayName
                : pathOrPropertyName;
    }

    private static IEnumerable<FrontedBindingTreeNode> FlattenBindingTree(
        IEnumerable<FrontedBindingTreeNode> nodes)
    {
        return nodes.SelectMany(node => node.Flatten());
    }

    private static HashSet<string> BindingSearchPaths(
        FrontedBindingBrowserProvider provider,
        FrontedBindingTypeFilter filter)
    {
        return provider.Search(null, filter)
            .Select(node => node.FullPath)
            .Where(path => path is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    private static FrontedPropertyEditorItem NameEditorRow()
    {
        return new FrontedPropertyEditorItem
        {
            PropertyName = nameof(FrontedControlDesignItem.Name),
            EditorKind = FrontedPropertyEditorKind.Text
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

    private static void WriteBuiltInLayout(
        string builtInRoot,
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config)
    {
        var folder = Path.Combine(builtInRoot, windowTypeName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, $"{canvasName}.json"),
            JsonSerializer.Serialize(config));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "neo-bpsys-wpf-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string GetRepositoryPath(
        string first,
        string second,
        string third,
        string? fourth = null,
        [CallerFilePath] string sourceFilePath = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", ".."));
        return fourth is null
            ? Path.Combine(repositoryRoot, first, second, third)
            : Path.Combine(repositoryRoot, first, second, third, fourth);
    }

    private static (double Width, double Height) ToSize(FrontedDesignerResolvedBounds bounds)
    {
        return (bounds.Width, bounds.Height);
    }

    private sealed class KnownFrontedControlRegistry : IFrontedControlRegistry
    {
        private static readonly IReadOnlyCollection<IFrontedControl> Controls =
        [
            new KnownFrontedControl("Text", typeof(TextFrontedControlConfig)),
            new KnownFrontedControl("LocalizedText", typeof(LocalizedTextControlConfig)),
            new KnownFrontedControl("Image", typeof(ImageFrontedControlConfig)),
            new KnownFrontedControl("BorderedImage", typeof(BorderedImageFrontedControlConfig)),
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
