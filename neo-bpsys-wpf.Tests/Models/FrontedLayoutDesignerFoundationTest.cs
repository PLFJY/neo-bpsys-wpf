#nullable enable

using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
        Assert.Equal(options, row.Options?.Cast<string>().ToArray());
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
        viewModel.ApplyPropertyEdit(NameEditorRow(), "Bad.Name");
        Assert.Equal("Title", title.Name);

        viewModel.ApplyPropertyEdit(NameEditorRow(), "Logo");
        Assert.Equal("Title", title.Name);

        viewModel.SelectDesignItem(runtimeCritical);
        viewModel.ApplyPropertyEdit(NameEditorRow(), "SurPickA");
        Assert.Equal("SurPick0", runtimeCritical.Name);
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
            "OpenValidationDetails"
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
        Assert.Contains("ZoomPresets", text);
        Assert.Contains("FitToWindowCommand", text);
        Assert.Contains("InteractionLayer", text);
        Assert.Contains("DesignSurfaceGrid", text);
        Assert.Contains("PropertyEditorContentControlStyle", text);
        Assert.Contains("OpenValidationDetails_OnClick", text);
        Assert.DoesNotContain("ItemsSource=\"{Binding ValidationMessages}\"", text);
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
        FrontedControlDesignItem item)
    {
        return new FrontedPropertyGridBuilder().Build(
            document,
            item,
            CreateValidator(),
            new FrontedLayoutReferenceScanner(),
            new FrontedLayoutRuntimeContractCatalog());
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
