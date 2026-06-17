#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
            config);

        Assert.Equal(["Title", "SurPick0"], document.Controls.Select(item => item.Name));
    }

    [Fact]
    public void DesignConverterPreservesAndSyncsPluginRequiredPlugins()
    {
        var registry = new PluginFrontedControlRegistryForTests();
        var config = new FrontedCanvasConfig
        {
            CanvasWidth = 100,
            CanvasHeight = 50,
            RequiredPlugins =
            [
                new FrontedPluginDependency
                {
                    PackageId = "top.plfjy.example.fronted",
                    MinVersion = "1.2.3",
                    Controls = ["plugin:top.plfjy.example.fronted/TeamCard"]
                }
            ],
            Controls =
            {
                ["TeamCard1"] = new PluginFrontedControlConfig
                {
                    ControlType = "plugin:top.plfjy.example.fronted/TeamCard",
                    Width = 100,
                    Height = 40
                }
            }
        };

        var converter = new FrontedLayoutDesignConverter(registry);
        var document = converter.FromConfig("TestWindow", "BaseCanvas", config);
        var roundTrip = converter.ToConfig(document);

        var dependency = Assert.Single(roundTrip.RequiredPlugins);
        Assert.Equal("top.plfjy.example.fronted", dependency.PackageId);
        Assert.Equal("1.2.3", dependency.MinVersion);
        Assert.Equal(["plugin:top.plfjy.example.fronted/TeamCard"], dependency.Controls);
        Assert.DoesNotContain("RequiredPlugins", roundTrip.Controls.Keys);
    }

    [Fact]
    public void GenericPluginConfigMaterializesToTypedConfigWhenDescriptorExists()
    {
        var generic = JsonSerializer.Deserialize<PluginFrontedControlConfig>(
            """
            {
              "ControlType": "plugin:top.plfjy.example.fronted/TeamCard",
              "Left": 10,
              "Top": 20,
              "Title": "Home",
              "AccentColor": "#FF112233",
              "Mode": "Compact"
            }
            """);

        Assert.NotNull(generic);
        var materialized = FrontedPluginControlConfigMaterializer.Materialize(
            "TeamCard1",
            generic,
            new PluginFrontedControlRegistryForTests());

        var typed = Assert.IsType<TestPluginDesignerConfig>(materialized);
        Assert.Equal("plugin:top.plfjy.example.fronted/TeamCard", typed.ControlType);
        Assert.Equal(10, typed.Left);
        Assert.Equal("Home", typed.Title);
        Assert.Equal("#FF112233", typed.AccentColor);
        Assert.Equal("Compact", typed.Mode);
    }

    [Fact]
    public void AddControlCatalogIncludesPluginControlsWithDefaultConfig()
    {
        var factory = new FrontedControlDefaultConfigFactory(
            new PluginFrontedControlRegistryForTests(),
            new FrontedDesignerLocalizationService());

        var pluginItem = factory.GetCatalog()
            .SelectMany(group => group.Items)
            .Single(item => item.ControlType == "plugin:top.plfjy.example.fronted/TeamCard");

        Assert.True(pluginItem.IsPlugin);
        Assert.True(pluginItem.IsAvailable);

        var document = new FrontedCanvasDesignDocument
        {
            CanvasConfig = new FrontedCanvasConfig { CanvasWidth = 400, CanvasHeight = 300 }
        };
        var config = Assert.IsType<TestPluginDesignerConfig>(
            factory.Create("plugin:top.plfjy.example.fronted/TeamCard", document));
        Assert.Equal("plugin:top.plfjy.example.fronted/TeamCard", config.ControlType);
        Assert.Equal("Default", config.Title);
    }

    [Fact]
    public void BehaviorGuid_PluginDefault_AlsoGetsFreshGuid()
    {
        var factory = new FrontedControlDefaultConfigFactory(
            new PluginFrontedControlRegistryForTests(),
            new FrontedDesignerLocalizationService());
        var document = new FrontedCanvasDesignDocument
        {
            CanvasConfig = new FrontedCanvasConfig { CanvasWidth = 400, CanvasHeight = 300 }
        };

        var config = factory.Create("plugin:top.plfjy.example.fronted/TeamCard", document);

        Assert.NotEqual(Guid.Empty, config.BehaviorGuid);
        Assert.NotEqual(PluginFrontedControlRegistryForTests.PluginDefaultBehaviorGuid, config.BehaviorGuid);
    }

    [Fact]
    public void PropertyGridUsesPluginDescriptorMetadata()
    {
        var document = new FrontedCanvasDesignDocument
        {
            WindowTypeName = "TestWindow",
            CanvasName = "BaseCanvas",
            CanvasConfig = new FrontedCanvasConfig { CanvasWidth = 400, CanvasHeight = 300 },
            Controls =
            {
                new FrontedControlDesignItem
                {
                    Name = "TeamCard1",
                    Config = new TestPluginDesignerConfig(),
                    IsSelectableInEditor = true,
                    IsEditableInEditor = true
                }
            }
        };

        var builder = new FrontedPropertyGridBuilder(
            new FrontedFontFamilyOptionProvider(),
            new FrontedDesignerLocalizationService(),
            new PluginFrontedControlRegistryForTests());

        var rows = builder.Build(
            document,
            document.Controls[0],
            new FrontedLayoutValidator(new PluginFrontedControlRegistryForTests()),
            new FrontedLayoutReferenceScanner());

        var mode = rows.Single(row => row.PropertyName == nameof(TestPluginDesignerConfig.Mode));
        Assert.Equal(FrontedPropertyEditorKind.Enum, mode.EditorKind);
        Assert.Equal(FrontedBindingTargetKind.Any, mode.BindingTargetKind);
        Assert.Equal("Plugin", mode.GroupName);
        Assert.NotNull(mode.Options);

        var titleBinding = rows.Single(row => row.PropertyName == nameof(TestPluginDesignerConfig.TitleBindingPath));
        Assert.True(titleBinding.CanBrowseBinding);
        Assert.Equal(FrontedBindingTargetKind.Text, titleBinding.BindingTargetKind);
    }

    [Fact]
    public void BehaviorGuid_PropertyGridDoesNotShowGuidRow()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig
            {
                ControlType = "Text",
                Text = "Hello",
                BehaviorGuid = Guid.NewGuid()
            },
            IsSelectableInEditor = true,
            IsEditableInEditor = true
        };
        var document = CreateDocument([item]);

        var rows = BuildPropertyRows(document, item);

        Assert.DoesNotContain(rows, row =>
            string.Equals(row.PropertyName, nameof(FrontedControlConfigBase.BehaviorGuid), StringComparison.Ordinal)
            || string.Equals(row.DisplayName, nameof(FrontedControlConfigBase.BehaviorGuid), StringComparison.Ordinal));
    }

    [Fact]
    public void MissingPluginValidatorWarningDoesNotMaskUnknownBuiltInError()
    {
        var validator = new FrontedLayoutValidator(new KnownFrontedControlRegistry());
        var document = new FrontedCanvasDesignDocument
        {
            WindowTypeName = "TestWindow",
            CanvasName = "BaseCanvas",
            CanvasConfig = new FrontedCanvasConfig { CanvasWidth = 400, CanvasHeight = 300 },
            Controls =
            {
                new FrontedControlDesignItem
                {
                    Name = "MissingPlugin",
                    Config = new PluginFrontedControlConfig { ControlType = "plugin:top.plfjy.missing/TeamCard" }
                },
                new FrontedControlDesignItem
                {
                    Name = "Unknown",
                    Config = new FrontedControlConfigBase { ControlType = "UnknownBuiltIn" }
                }
            }
        };

        var messages = validator.Validate(document);

        Assert.Contains(messages, message => message.Code == "PluginControlMissing" && message.Severity == FrontedLayoutValidationSeverity.Warning);
        Assert.Contains(messages, message => message.Code == "ControlTypeUnknown" && message.Severity == FrontedLayoutValidationSeverity.Error);
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
    public void ValidatorAcceptsValidExistingBpWindowBaseCanvasLayout()
    {
        var config = ReadBuiltInLayout("BpWindow");
        var validator = CreateValidator();

        var messages = validator.Validate("BpWindow", "BaseCanvas", config);

        Assert.DoesNotContain(messages, message => message.Severity == FrontedLayoutValidationSeverity.Error);
    }

    [Fact]
    public void BuiltInScoreGlobalLayoutUsesGlobalScoreRowCells()
    {
        var config = ReadBuiltInLayout("ScoreGlobalWindow");

        var home = Assert.IsType<GlobalScoreRowControlConfig>(config.Controls["HomeGlobalScoreRow"]);
        var away = Assert.IsType<GlobalScoreRowControlConfig>(config.Controls["AwayGlobalScoreRow"]);
        Assert.Equal(14, home.Cells.Count);
        Assert.Equal(14, away.Cells.Count);
        Assert.Contains(home.Cells, cell => cell is
        {
            Id: "Game5OvertimeSecondHalf",
            GameNumber: 5,
            GameKind: ScoreGameKind.Overtime,
            HalfKind: ScoreHalfKind.SecondHalf
        });
        Assert.Contains(home.Cells, cell => cell is
        {
            Id: "Game3OvertimeSecondHalf",
            Visibility: FrontedControlVisibility.Collapsed
        });
        Assert.True(home.MajorGameGap > 0);
        Assert.True(home.HalfGameGap > 0);
    }

    [Fact]
    public void BuiltInScoreGlobalBo3StateHasIndependentGlobalScoreRowCells()
    {
        var config = ReadBuiltInLayout("ScoreGlobalWindow");
        var bo3 = config.BoModeStates["Bo3"];
        var home = Assert.IsType<GlobalScoreRowControlConfig>(bo3.Controls["HomeGlobalScoreRow"]);

        Assert.Equal(14, home.Cells.Count);
        Assert.Contains(home.Cells, cell => cell is
        {
            Id: "Game3OvertimeSecondHalf",
            GameNumber: 3,
            GameKind: ScoreGameKind.Overtime,
            HalfKind: ScoreHalfKind.SecondHalf,
            X: 630
        });
        Assert.Contains(home.Cells, cell => cell is
        {
            Id: "Game5OvertimeSecondHalf",
            Visibility: FrontedControlVisibility.Collapsed
        });
        Assert.DoesNotContain(config.Controls["HomeGlobalScoreRow"].ToString() ?? string.Empty, home.Cells.Select(cell => cell.Id));
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
    public void ValidatorWarnsWhenStaticImagePathCannotResolve()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Logo",
            Config = new ImageFrontedControlConfig { ImagePath = "Resources/missing.png" }
        };
        var validator = new FrontedLayoutValidator(
            new KnownFrontedControlRegistry(),
            new FixedPathFrontedResourceResolver());

        var messages = validator.Validate(CreateDocument([item]));

        Assert.Contains(
            messages,
            message => message.Code == "ImagePathUnresolved"
                       && message.ControlName == "Logo"
                       && message.PropertyName == nameof(ImageFrontedControlConfig.ImagePath)
                       && message.Severity == FrontedLayoutValidationSeverity.Warning);
        Assert.DoesNotContain(messages, message => message.Severity == FrontedLayoutValidationSeverity.Error);
    }

    [Fact]
    public void ValidatorDoesNotErrorWhenBindingPathOverridesImagePath()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Logo",
            Config = new ImageFrontedControlConfig
            {
                BindingPath = "CurrentGame.SurTeam.Logo",
                ImagePath = "Resources/logo.png"
            }
        };

        var messages = CreateValidator().Validate(CreateDocument([item]));

        Assert.Contains(
            messages,
            message => message.Code == "ImagePathIgnored"
                       && message.ControlName == "Logo"
                       && message.PropertyName == nameof(ImageFrontedControlConfig.ImagePath)
                       && message.Severity == FrontedLayoutValidationSeverity.Warning);
        Assert.DoesNotContain(messages, message => message.Severity == FrontedLayoutValidationSeverity.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PropertyGridShowsBindingOverrideMessagesAsWarningsOnStaticContentRows(bool borderedImage)
    {
        var text = new FrontedControlDesignItem
        {
            Name = "Text",
            Config = new TextFrontedControlConfig
            {
                Text = "Static",
                TextBinding = new FrontedTextBindingExpression
                {
                    Sources = [new FrontedBindingSourceConfig { Path = "CurrentGame.SurTeam.Name" }]
                }
            }
        };
        var localizedText = new FrontedControlDesignItem
        {
            Name = "Localized",
            Config = new LocalizedTextControlConfig
            {
                LocalizationKey = "StaticKey",
                TextBinding = new FrontedTextBindingExpression
                {
                    Sources = [new FrontedBindingSourceConfig { Path = "CurrentGame.SurTeam.Name" }]
                }
            }
        };
        var image = new FrontedControlDesignItem
        {
            Name = "Image",
            Config = borderedImage
                ? new BorderedImageFrontedControlConfig
                {
                    BindingPath = "CurrentGame.SurTeam.Logo",
                    ImagePath = "Resources/logo.png"
                }
                : new ImageFrontedControlConfig
                {
                    BindingPath = "CurrentGame.SurTeam.Logo",
                    ImagePath = "Resources/logo.png"
                }
        };
        var document = CreateDocument([text, localizedText, image]);

        AssertWarningOnStaticRow(document, text, nameof(TextFrontedControlConfig.Text));
        AssertWarningOnStaticRow(document, localizedText, nameof(LocalizedTextControlConfig.LocalizationKey));
        AssertWarningOnStaticRow(document, image, nameof(ImageFrontedControlConfig.ImagePath));
    }

    [Fact]
    public void ValidatorWarnsWhenStaticImagePathFailsSafetyValidation()
    {
        var root = CreateTempDirectory();
        try
        {
            var imagePath = Path.Combine(root, "logo.png");
            WriteTinyPng(imagePath);
            var item = new FrontedControlDesignItem
            {
                Name = "Logo",
                Config = new ImageFrontedControlConfig { ImagePath = "Resources/logo.png" }
            };
            var validator = new FrontedLayoutValidator(
                new KnownFrontedControlRegistry(),
                new FixedPathFrontedResourceResolver(imagePath),
                new RejectingImageSafetyService());

            var messages = validator.Validate(CreateDocument([item]));

            Assert.Contains(
                messages,
                message => message.Code == "ImagePathUnsafe"
                           && message.ControlName == "Logo"
                           && message.PropertyName == nameof(ImageFrontedControlConfig.ImagePath)
                           && message.Severity == FrontedLayoutValidationSeverity.Warning);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
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
    public void DesignerLayoutCatalogListsMigratedWindows()
    {
        var entries = new FrontedDesignerLayoutCatalog().GetEntries();

        Assert.Equal(8, entries.Count);
        Assert.Contains(entries, entry => entry.WindowTypeName == "ScoreSurWindow");
        Assert.Contains(entries, entry => entry.WindowTypeName == "ScoreHunWindow");
        Assert.Contains(entries, entry => entry.WindowTypeName == "ScoreGlobalWindow");
        Assert.Contains(entries, entry => entry.WindowTypeName == "CutSceneWindow");
        Assert.Contains(entries, entry => entry.WindowTypeName == "GameDataWindow");
        Assert.Contains(entries, entry => entry.WindowTypeName == "BpOverviewWindow");
        Assert.Contains(entries, entry => entry.WindowTypeName == "MapV2Window");
        Assert.Contains(entries, entry => entry.WindowTypeName == "BpWindow");
        Assert.DoesNotContain(entries, entry => entry.WindowTypeName == "WidgetsWindow");
        Assert.All(entries, entry =>
        {
            Assert.True(entry.IsMigrated);
            Assert.True(entry.IsEditable);
        });
    }

    [Fact]
    public void DesignerLayoutCatalogDoesNotExposeWidgetsWindowCanvases()
    {
        var entries = new FrontedDesignerLayoutCatalog().GetEntries();

        Assert.DoesNotContain(entries, entry => entry.WindowTypeName == "WidgetsWindow");
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
            $"{entry.WindowTypeName}.json");

        Assert.True(File.Exists(path), path);
    }

    [Theory]
    [MemberData(nameof(CatalogEntries))]
    public void DesignerLayoutCatalogLayoutLoadsAndValidatesWithoutErrors(
        FrontedDesignerLayoutCatalogEntry entry)
    {
        var config = ReadBuiltInLayout(entry.WindowTypeName);
        var messages = CreateValidator().Validate(entry.WindowTypeName, "BaseCanvas", config);

        Assert.DoesNotContain(messages, message => message.Severity == FrontedLayoutValidationSeverity.Error);
    }

    [Theory]
    [MemberData(nameof(CatalogEntries))]
    public void DesignerDocumentUsesCanvasSizeFromLoadedConfig(
        FrontedDesignerLayoutCatalogEntry entry)
    {
        var config = ReadBuiltInLayout(entry.WindowTypeName);
        var document = new FrontedLayoutDesignConverter().FromConfig(
            entry.WindowTypeName,
            "BaseCanvas",
            config);

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

    [Theory]
    [InlineData("Text", typeof(TextFrontedControlConfig), 160, 40)]
    [InlineData("LocalizedText", typeof(LocalizedTextControlConfig), 200, 40)]
    [InlineData("Image", typeof(ImageFrontedControlConfig), 120, 120)]
    [InlineData("BorderedImage", typeof(BorderedImageFrontedControlConfig), 120, 120)]
    [InlineData("MapNameText", typeof(MapNameTextControlConfig), 240, 40)]
    [InlineData("GameProgressText", typeof(GameProgressTextControlConfig), 260, 56)]
    [InlineData("TalentTraitDisplay", typeof(TalentTraitDisplayControlConfig), 180, 40)]
    [InlineData("GlobalScoreRow", typeof(GlobalScoreRowControlConfig), 1080, 40)]
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
    public void BehaviorGuid_NewControlViaFactory_HasNonEmptyGuid()
    {
        var factory = new FrontedControlDefaultConfigFactory();

        var config = factory.Create("Text", CreateDocument([]));

        Assert.NotEqual(Guid.Empty, config.BehaviorGuid);
    }

    [Fact]
    public void DefaultConfigFactoryDoesNotCreateCompatibilityOverlaysOrBanControlsFromNormalAddControl()
    {
        var factory = new FrontedControlDefaultConfigFactory();

        Assert.False(factory.CanCreate("PickingBorderOverlay"));
        Assert.Throws<NotSupportedException>(() => factory.Create("PickingBorderOverlay", CreateDocument([])));
        Assert.False(factory.CanCreate("CurrentBanDisplay"));
        Assert.Throws<NotSupportedException>(() => factory.Create("CurrentBanDisplay", CreateDocument([])));
        Assert.False(factory.CanCreate("BanSlotDisplay"));
        Assert.Throws<NotSupportedException>(() => factory.Create("BanSlotDisplay", CreateDocument([])));
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
        Assert.Equal("Arial", globalScore.FontFamily);
        Assert.Equal("Bold", globalScore.FontWeight);
        Assert.Equal("#FFFFFFFF", globalScore.Color);
        Assert.Equal(24, globalScore.FontSize);
        Assert.True(globalScore.ShowCampIcon);
        Assert.Equal(14, globalScore.Cells.Count);
        Assert.All(globalScore.Cells, cell =>
        {
            Assert.Equal(75, cell.Width);
            Assert.Equal(32, cell.Height);
        });

        var mapV2 = Assert.IsType<MapV2DisplayControlConfig>(factory.Create("MapV2Display", document));
        Assert.Equal("ArmsFactory", mapV2.MapKey);
        Assert.Equal("#FF2B483B", mapV2.MapBorderNormalColor);
        Assert.Equal("#FF9C3E2F", mapV2.MapBorderBannedColor);
    }

    [Fact]
    public void ControlNameGeneratorCreatesUniqueNames()
    {
        var document = CreateDocument(
            [
                new FrontedControlDesignItem { Name = "Text1", Config = new TextFrontedControlConfig() },
                new FrontedControlDesignItem { Name = "Text2", Config = new TextFrontedControlConfig() }
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
        var sourceBehaviorGuid = Guid.NewGuid();
        var title = new FrontedControlDesignItem
        {
            Name = "Text9",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig
            {
                Text = "A",
                Left = 10,
                Top = 20,
                ZIndex = 3,
                BehaviorGuid = sourceBehaviorGuid
            }
        };
        var document = CreateDocument([title]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(title);

        viewModel.CopySelectedControlCommand.Execute(null);
        viewModel.PasteControlCommand.Execute(null);

        var pasted = Assert.Single(document.Controls, control => control.Name == "Text10");
        Assert.NotSame(title.Config, pasted.Config);
        Assert.NotEqual(Guid.Empty, pasted.Config.BehaviorGuid);
        Assert.NotEqual(sourceBehaviorGuid, pasted.Config.BehaviorGuid);
        Assert.Equal(20, pasted.Config.Left);
        Assert.Equal(30, pasted.Config.Top);
        Assert.Equal(4, pasted.Config.ZIndex);
        Assert.Same(pasted, viewModel.SelectedDesignItem);
        Assert.True(document.IsDirty);
        Assert.True(viewModel.CanUndo);
        Assert.True(viewModel.HasPendingScheduledDesignerWork);
    }

    [Fact]
    public void PasteControlUsesSourceNameWithUnderscoreSuffixWhenSourceNameDoesNotEndWithNumber()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Text = "A" }
        };
        var document = CreateDocument([title]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(title);

        viewModel.CopySelectedControlCommand.Execute(null);
        viewModel.PasteControlCommand.Execute(null);

        Assert.Contains(document.Controls, control => control.Name == "Title_1");
    }

    [Fact]
    public void PasteControlUsesSourceNameWhenItIsAvailable()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Text = "A" }
        };
        var document = CreateDocument([title]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(title);

        viewModel.CopySelectedControlCommand.Execute(null);
        document.Controls.Remove(title);
        viewModel.ClearSelection();
        viewModel.PasteControlCommand.Execute(null);

        Assert.Contains(document.Controls, control => control.Name == "Title");
        Assert.DoesNotContain(document.Controls, control => control.Name == "Title_1");
    }

    [Fact]
    public void PasteControlIncrementsSourceNameTrailingNumberAndSkipsExistingNames()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Text = "A" }
        };
        var existing = new FrontedControlDesignItem
        {
            Name = "Title2",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Text = "B" }
        };
        var document = CreateDocument([title, existing]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(title);

        viewModel.CopySelectedControlCommand.Execute(null);
        viewModel.PasteControlCommand.Execute(null);

        Assert.Contains(document.Controls, control => control.Name == "Title3");
    }

    [Fact]
    public void PasteSchedulesValidationAndPreviewAndCoalescesRapidPastes()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Text1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Text = "A" }
        };
        var document = CreateDocument([title]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        var previewRequests = 0;
        viewModel.PreviewRenderRequested += (_, _) => previewRequests++;
        viewModel.SelectDesignItem(title);
        viewModel.CopySelectedControlCommand.Execute(null);

        viewModel.PasteControlCommand.Execute(null);
        viewModel.PasteControlCommand.Execute(null);

        Assert.Equal(3, document.Controls.Count);
        Assert.Equal(0, previewRequests);
        Assert.True(viewModel.HasPendingScheduledDesignerWork);

        viewModel.ExecuteScheduledDesignerWorkForTests();

        Assert.Equal(1, previewRequests);
        Assert.Equal(1, viewModel.ScheduledDesignerValidationExecutionCount);
        Assert.Equal(1, viewModel.ScheduledDesignerPreviewExecutionCount);
        Assert.False(viewModel.HasPendingScheduledDesignerWork);
    }

    [Fact]
    public void CopyPasteUsesImmutableClipboardPayload()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Text1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Text = "Copied" }
        };
        var document = CreateDocument([title]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(title);

        viewModel.CopySelectedControlCommand.Execute(null);
        ((TextFrontedControlConfig)title.Config).Text = "Edited";
        viewModel.PasteControlCommand.Execute(null);

        var pasted = Assert.Single(document.Controls, control => control.Name == "Text2");
        Assert.Equal("Copied", Assert.IsType<TextFrontedControlConfig>(pasted.Config).Text);
    }

    [Fact]
    public void PastePreservesPluginConfigAndExtensionData()
    {
        using var extensionJson = JsonDocument.Parse("""{ "Title": "Home", "Nested": { "Enabled": true } }""");
        var plugin = new FrontedControlDesignItem
        {
            Name = "TeamCard1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:top.plfjy.missing/TeamCard",
                ExtensionData =
                {
                    ["Title"] = extensionJson.RootElement.GetProperty("Title").Clone(),
                    ["Nested"] = extensionJson.RootElement.GetProperty("Nested").Clone()
                }
            }
        };
        var document = CreateDocument([plugin]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(plugin);

        viewModel.CopySelectedControlCommand.Execute(null);
        viewModel.PasteControlCommand.Execute(null);

        var pasted = Assert.Single(document.Controls, control => control.Name == "TeamCard2");
        var config = Assert.IsType<PluginFrontedControlConfig>(pasted.Config);
        Assert.Equal("plugin:top.plfjy.missing/TeamCard", config.ControlType);
        Assert.Equal("Home", config.ExtensionData["Title"].GetString());
        Assert.True(config.ExtensionData["Nested"].GetProperty("Enabled").GetBoolean());
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
        Assert.Equal(0, previewRequests);
        Assert.True(viewModel.HasPendingScheduledDesignerWork);
        viewModel.ExecuteScheduledDesignerWorkForTests();
        Assert.True(previewRequests > 0);
    }

    [Fact]
    public void DeleteControl_TriggersBehaviorCleanupCall()
    {
        var behaviorGuid = Guid.NewGuid();
        var behaviorService = new RecordingFrontedBehaviorService();
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { BehaviorGuid = behaviorGuid }
        };
        var document = CreateDocument([title]);
        var viewModel = new FrontedDesignerWindowViewModel(behaviorService) { CurrentDocument = document };
        viewModel.SelectDesignItem(title);

        viewModel.DeleteSelectedControlCommand.Execute(null);

        var removedGuid = Assert.Single(behaviorService.RemovedBehaviorGuids);
        Assert.Equal(behaviorGuid, removedGuid);
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
        Assert.True(viewModel.HasPendingScheduledDesignerWork);

        viewModel.RedoCommand.Execute(null);
        Assert.Single(viewModel.CurrentDocument!.Controls);
    }

    [Fact]
    public void UndoFallsBackToScheduledAtomicPreviewForAddDeleteRestore()
    {
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([]) };
        viewModel.AddControlCommand.Execute(new FrontedAddControlRequest { ControlType = "Text" });

        var previewRestoreStates = new List<bool>();
        var selectedRestoreStates = new List<bool>();
        viewModel.PreviewRenderRequested += (_, _) =>
            previewRestoreStates.Add(viewModel.IsRestoringSnapshotVisuals);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.SelectedDesignItem))
            {
                selectedRestoreStates.Add(viewModel.IsRestoringSnapshotVisuals);
            }
        };

        viewModel.UndoCommand.Execute(null);

        Assert.Empty(viewModel.CurrentDocument!.Controls);
        Assert.True(viewModel.HasPendingScheduledDesignerWork);
        Assert.True(viewModel.IsRestoringSnapshotVisuals);
        Assert.Contains(true, selectedRestoreStates);
        Assert.Empty(previewRestoreStates);
        Assert.Equal(0, viewModel.ScheduledDesignerValidationExecutionCount);
        Assert.Equal(0, viewModel.ScheduledDesignerPreviewExecutionCount);

        viewModel.ExecuteScheduledDesignerWorkForTests();

        Assert.Equal(1, viewModel.ScheduledDesignerValidationExecutionCount);
        Assert.Equal(1, viewModel.ScheduledDesignerPreviewExecutionCount);
        Assert.Equal([true], previewRestoreStates);
        Assert.False(viewModel.IsRestoringSnapshotVisuals);
    }

    [Fact]
    public void UndoGeometryOnlyMoveRestoresInPlaceAndSchedulesValidationOnly()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 10, Top = 20, Width = 100, Height = 30 }
        };
        var document = CreateDocument([title]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(title);

        var currentDocumentChanges = 0;
        var previewRequests = 0;
        var patchRequests = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.CurrentDocument))
            {
                currentDocumentChanges++;
            }
        };
        viewModel.PreviewRenderRequested += (_, _) => previewRequests++;
        viewModel.DesignerGeometryPatchRequested += (_, e) =>
        {
            patchRequests++;
            Assert.Contains(title, e.ChangedItems);
            Assert.True(e.UpdateSelection);
        };

        viewModel.MoveSelectedDesignItemBy(5, 7);
        previewRequests = 0;
        patchRequests = 0;

        viewModel.UndoCommand.Execute(null);

        Assert.Same(document, viewModel.CurrentDocument);
        Assert.Equal(0, currentDocumentChanges);
        Assert.Equal(10, title.Config.Left);
        Assert.Equal(20, title.Config.Top);
        Assert.Equal(1, patchRequests);
        Assert.Equal(0, previewRequests);
        Assert.True(viewModel.HasPendingScheduledDesignerWork);

        viewModel.ExecuteScheduledDesignerWorkForTests();

        Assert.Equal(1, viewModel.ScheduledDesignerValidationExecutionCount);
        Assert.Equal(0, viewModel.ScheduledDesignerPreviewExecutionCount);
    }

    [Fact]
    public void UndoGeometryOnlyResizeRestoresInPlace()
    {
        var image = new FrontedControlDesignItem
        {
            Name = "Image",
            Config = new BorderedImageFrontedControlConfig
            {
                Left = 10,
                Top = 20,
                Width = 100,
                Height = 80,
                ImageWidth = 90,
                ImageHeight = 70
            }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([image]) };
        viewModel.SelectDesignItem(image);
        viewModel.BorderedImageResizeTarget = FrontedDesignerResizeTarget.Image;

        viewModel.CaptureUndoSnapshot();
        var config = Assert.IsType<BorderedImageFrontedControlConfig>(image.Config);
        config.ImageWidth = 120;
        config.ImageHeight = 100;
        viewModel.CurrentDocument!.IsDirty = true;

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(90, config.ImageWidth);
        Assert.Equal(70, config.ImageHeight);
        Assert.True(viewModel.HasPendingScheduledDesignerWork);
    }

    [Fact]
    public void UndoGeometryOnlyZIndexAndOrderRestoresInPlace()
    {
        var first = new FrontedControlDesignItem { Name = "First", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var second = new FrontedControlDesignItem { Name = "Second", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var document = CreateDocument([first, second]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        Assert.True(viewModel.CommitLayerDrop(second, 2, first, insertAfter: false));
        viewModel.ExecuteScheduledDesignerWorkForTests();

        var previewRequests = 0;
        var patchRequests = 0;
        viewModel.PreviewRenderRequested += (_, _) => previewRequests++;
        viewModel.DesignerGeometryPatchRequested += (_, e) =>
        {
            patchRequests++;
            Assert.True(e.RebuildInteractionLayer);
            Assert.True(e.ZIndexChanged);
        };

        viewModel.UndoCommand.Execute(null);

        Assert.Same(document, viewModel.CurrentDocument);
        Assert.Equal(["First", "Second"], document.Controls.Select(item => item.Name));
        Assert.Equal(1, second.Config.ZIndex);
        Assert.Equal(1, patchRequests);
        Assert.Equal(0, previewRequests);
    }

    [Fact]
    public void UndoNonGeometryTextChangeSchedulesFullRestoreWithoutImmediatePreview()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Text = "Old" }
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

        var previewRequests = 0;
        viewModel.PreviewRenderRequested += (_, _) => previewRequests++;

        viewModel.UndoCommand.Execute(null);

        Assert.Equal("Old", ((TextFrontedControlConfig)viewModel.CurrentDocument!.Controls[0].Config).Text);
        Assert.Equal(0, previewRequests);
        Assert.True(viewModel.IsRestoringSnapshotVisuals);
        Assert.True(viewModel.HasPendingScheduledDesignerWork);

        viewModel.ExecuteScheduledDesignerWorkForTests();

        Assert.Equal(1, previewRequests);
        Assert.False(viewModel.IsRestoringSnapshotVisuals);
    }

    [Fact]
    public void UndoPluginAndMissingPluginGeometryOnlyChangesUseFastPath()
    {
        var installedPlugin = new FrontedControlDesignItem
        {
            Name = "TeamCard",
            Config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:top.plfjy.example.fronted/TeamCard",
                Left = 10,
                Top = 20,
                ExtensionData =
                {
                    ["TeamNameBindingPath"] = JsonSerializer.SerializeToElement("CurrentGame.SurTeam.Name")
                }
            }
        };
        var missingPlugin = new FrontedControlDesignItem
        {
            Name = "MissingTeamCard",
            Config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:top.plfjy.missing/TeamCard",
                Left = 30,
                Top = 40
            }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([installedPlugin, missingPlugin])
        };
        viewModel.SelectDesignItem(installedPlugin);
        viewModel.CaptureUndoSnapshot();
        installedPlugin.Config.Left = 50;
        missingPlugin.Config.Left = 60;
        viewModel.CurrentDocument!.IsDirty = true;

        var patchRequests = 0;
        viewModel.DesignerGeometryPatchRequested += (_, e) =>
        {
            patchRequests++;
            Assert.Contains(installedPlugin, e.ChangedItems);
            Assert.Contains(missingPlugin, e.ChangedItems);
        };

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(10, installedPlugin.Config.Left);
        Assert.Equal(30, missingPlugin.Config.Left);
        Assert.Equal(1, patchRequests);
    }

    [Fact]
    public void UndoHistoryKeepsNewestSnapshotsAndDropsOldestPastLimit()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 0 }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([title]) };

        for (var index = 0; index < FrontedLayoutLimits.MaxDesignerUndoSnapshots + 10; index++)
        {
            viewModel.CaptureUndoSnapshot();
            title.Config.Left = index + 1;
            viewModel.CurrentDocument!.IsDirty = true;
        }

        Assert.True(viewModel.CanUndo);

        for (var index = 0; index < FrontedLayoutLimits.MaxDesignerUndoSnapshots; index++)
        {
            Assert.True(viewModel.CanUndo);
            viewModel.UndoCommand.Execute(null);
        }

        Assert.Equal(10, title.Config.Left);
        Assert.False(viewModel.CanUndo);

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(10, title.Config.Left);
    }

    [Fact]
    public void RedoHistoryIsLimitedAfterManyUndoOperations()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 0 }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([title]) };

        for (var index = 0; index < FrontedLayoutLimits.MaxDesignerUndoSnapshots + 10; index++)
        {
            viewModel.CaptureUndoSnapshot();
            title.Config.Left = index + 1;
            viewModel.CurrentDocument!.IsDirty = true;
        }

        for (var index = 0; index < FrontedLayoutLimits.MaxDesignerUndoSnapshots; index++)
        {
            viewModel.UndoCommand.Execute(null);
        }

        Assert.True(viewModel.CanRedo);

        for (var index = 0; index < FrontedLayoutLimits.MaxDesignerUndoSnapshots; index++)
        {
            Assert.True(viewModel.CanRedo);
            viewModel.RedoCommand.Execute(null);
        }

        Assert.Equal(FrontedLayoutLimits.MaxDesignerUndoSnapshots + 10, title.Config.Left);
        Assert.False(viewModel.CanRedo);

        viewModel.RedoCommand.Execute(null);

        Assert.Equal(FrontedLayoutLimits.MaxDesignerUndoSnapshots + 10, title.Config.Left);
    }

    [Fact]
    public void DuplicateUndoSnapshotsDoNotGrowHistoryPastSingleState()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 5 }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([title]) };

        for (var index = 0; index < 10; index++)
        {
            viewModel.CaptureUndoSnapshot();
        }

        title.Config.Left = 25;
        viewModel.CurrentDocument!.IsDirty = true;

        Assert.True(viewModel.CanUndo);

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(5, title.Config.Left);
        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.CanRedo);
    }

    [Fact]
    public void UndoRedoCommandsUpdateCanUndoAndCanRedoWithLimitedHistory()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Left = 0 }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([title]) };

        viewModel.CaptureUndoSnapshot();
        title.Config.Left = 1;
        viewModel.CurrentDocument!.IsDirty = true;

        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);

        viewModel.UndoCommand.Execute(null);

        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.CanRedo);

        viewModel.RedoCommand.Execute(null);

        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
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
        Assert.True(viewModel.HasPendingScheduledDesignerWork);
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
    public void TextBindingEditIsUndoableAndRedoable()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Text = "Static" }
        };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = CreateDocument([title]) };
        viewModel.SelectDesignItem(title);
        var row = new FrontedPropertyEditorItem
        {
            PropertyName = nameof(TextFrontedControlConfig.TextBinding),
            EditorKind = FrontedPropertyEditorKind.TextBinding
        };
        var expression = new FrontedTextBindingExpression
        {
            Sources =
            [
                new FrontedBindingSourceConfig { Path = "CurrentGame.HomeTeam.Name" },
                new FrontedBindingSourceConfig { Path = "CurrentGame.AwayTeam.Name" }
            ],
            StringFormat = "{0} vs {1}"
        };

        Assert.True(viewModel.ApplyTextBindingEdit(row, expression));
        Assert.Equal(
            "{0} vs {1}",
            Assert.IsType<TextFrontedControlConfig>(viewModel.CurrentDocument!.Controls[0].Config)
                .TextBinding!.StringFormat);
        Assert.True(viewModel.CanUndo);

        viewModel.UndoCommand.Execute(null);
        Assert.Null(
            Assert.IsType<TextFrontedControlConfig>(viewModel.CurrentDocument!.Controls[0].Config).TextBinding);

        viewModel.RedoCommand.Execute(null);
        Assert.Equal(
            ["CurrentGame.HomeTeam.Name", "CurrentGame.AwayTeam.Name"],
            Assert.IsType<TextFrontedControlConfig>(viewModel.CurrentDocument!.Controls[0].Config)
                .TextBinding!.Sources.Select(source => source.Path));
    }

    [Fact]
    public void KeyboardMoveUsesGeometryPatchInsteadOfFullPreviewRender()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Title",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { Left = 10, Top = 20, Width = 100, Height = 30 }
        };
        var document = CreateDocument([title]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(title);

        var previewRequests = 0;
        var patchRequests = 0;
        viewModel.PreviewRenderRequested += (_, _) => previewRequests++;
        viewModel.DesignerGeometryPatchRequested += (_, e) =>
        {
            patchRequests++;
            Assert.Contains(title, e.ChangedItems);
            Assert.False(e.RebuildInteractionLayer);
            Assert.True(e.UpdateSelection);
        };

        viewModel.MoveSelectedDesignItemBy(5, 7);

        Assert.Equal(15, title.Config.Left);
        Assert.Equal(27, title.Config.Top);
        Assert.Equal(1, patchRequests);
        Assert.Equal(0, previewRequests);
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
                }
            ])
        };

        Assert.Equal(["Logo", "Title"], viewModel.FilteredDesignItems.Select(item => item.Name));

        viewModel.ControlFilterText = "text";

        Assert.Equal(["Title"], viewModel.FilteredDesignItems.Select(item => item.Name));

        viewModel.ControlFilterText = string.Empty;
        Assert.Equal(2, viewModel.FilteredDesignItems.Count);

        viewModel.CurrentDocument = null;
        viewModel.ControlFilterText = string.Empty;
        Assert.Empty(viewModel.FilteredDesignItems);
    }

    [Fact]
    public void PasteUpdatesFilteredDesignItemsWithoutFullFilterReset()
    {
        var title = new FrontedControlDesignItem
        {
            Name = "Text1",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new TextFrontedControlConfig { ControlType = "Text", ZIndex = 1 }
        };
        var logo = new FrontedControlDesignItem
        {
            Name = "Logo",
            IsSelectableInEditor = true,
            IsEditableInEditor = true,
            Config = new ImageFrontedControlConfig { ControlType = "Image", ZIndex = 3 }
        };
        var document = CreateDocument([title, logo]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(title);
        viewModel.CopySelectedControlCommand.Execute(null);

        viewModel.PasteControlCommand.Execute(null);

        Assert.Equal(["Text2", "Logo", "Text1"], viewModel.FilteredDesignItems.Select(item => item.Name));

        viewModel.ControlFilterText = "logo";
        viewModel.SelectDesignItem(title);
        viewModel.CopySelectedControlCommand.Execute(null);
        viewModel.PasteControlCommand.Execute(null);

        Assert.Equal("logo", viewModel.ControlFilterText);
        Assert.Equal(["Logo"], viewModel.FilteredDesignItems.Select(item => item.Name));
    }

    [Fact]
    public void SelectionChangeOnlyTogglesPreviousAndCurrentItems()
    {
        var first = new FrontedControlDesignItem
        {
            Name = "First",
            IsSelectableInEditor = true,
            Config = new TextFrontedControlConfig()
        };
        var second = new FrontedControlDesignItem
        {
            Name = "Second",
            IsSelectableInEditor = true,
            Config = new TextFrontedControlConfig()
        };
        var document = CreateDocument([first, second]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        viewModel.SelectDesignItem(first);
        viewModel.SelectDesignItem(second);

        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);

        first.IsSelected = true;
        viewModel.CurrentDocument = CreateDocument([first, second]);

        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
    }

    [Fact]
    public void DesignerViewModelSelectDesignItemsKeepsMultipleItemsSelected()
    {
        var first = new FrontedControlDesignItem { Name = "First", Config = new TextFrontedControlConfig() };
        var second = new FrontedControlDesignItem { Name = "Second", Config = new TextFrontedControlConfig() };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([first, second])
        };

        viewModel.SelectDesignItems([first, second], second);

        Assert.Same(second, viewModel.SelectedDesignItem);
        Assert.Equal([first, second], viewModel.SelectedDesignItems);
        Assert.True(first.IsSelected);
        Assert.True(second.IsSelected);
    }

    [Fact]
    public void DesignerViewModelToggleDesignItemSelectionAddsAndRemovesItems()
    {
        var first = new FrontedControlDesignItem { Name = "First", Config = new TextFrontedControlConfig() };
        var second = new FrontedControlDesignItem { Name = "Second", Config = new TextFrontedControlConfig() };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([first, second])
        };

        viewModel.SelectDesignItem(first);
        viewModel.ToggleDesignItemSelection(second);
        viewModel.ToggleDesignItemSelection(first);

        Assert.Same(second, viewModel.SelectedDesignItem);
        Assert.Equal([second], viewModel.SelectedDesignItems);
        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
    }

    [Fact]
    public void DesignerViewModelMoveSelectedDesignItemByMovesAllSelectedItems()
    {
        var first = new FrontedControlDesignItem
        {
            Name = "First",
            Config = new TextFrontedControlConfig { Left = 10, Top = 20 }
        };
        var second = new FrontedControlDesignItem
        {
            Name = "Second",
            Config = new TextFrontedControlConfig { Left = 30, Top = 40 }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([first, second])
        };

        viewModel.SelectDesignItems([first, second], second);
        viewModel.MoveSelectedDesignItemBy(5, -10);

        Assert.Equal(15, first.Config.Left);
        Assert.Equal(10, first.Config.Top);
        Assert.Equal(35, second.Config.Left);
        Assert.Equal(30, second.Config.Top);
    }

    [Fact]
    public void DesignerViewModelApplyPropertyEditUpdatesAllSameTypeSelectedItemsExceptName()
    {
        var first = new FrontedControlDesignItem
        {
            Name = "First",
            Config = new TextFrontedControlConfig { Text = "A" }
        };
        var second = new FrontedControlDesignItem
        {
            Name = "Second",
            Config = new TextFrontedControlConfig { Text = "B" }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([first, second])
        };

        viewModel.SelectDesignItems([first, second], first);
        var applied = viewModel.ApplyPropertyEdit(TextEditorRow(nameof(TextFrontedControlConfig.Text)), "Shared");
        var renamed = viewModel.ApplyPropertyEdit(NameEditorRow(), "Renamed");

        Assert.True(applied);
        Assert.True(renamed);
        Assert.Equal("Shared", Assert.IsType<TextFrontedControlConfig>(first.Config).Text);
        Assert.Equal("Shared", Assert.IsType<TextFrontedControlConfig>(second.Config).Text);
        Assert.Equal("Renamed", first.Name);
        Assert.Equal("Second", second.Name);
    }

    [Fact]
    public void DesignerViewModelMultiSelectionMixedPlaceholderDoesNotOverwriteUnchangedProperty()
    {
        var first = new FrontedControlDesignItem
        {
            Name = "First",
            Config = new TextFrontedControlConfig { Left = 10, Top = 20, Text = "A" }
        };
        var second = new FrontedControlDesignItem
        {
            Name = "Second",
            Config = new TextFrontedControlConfig { Left = 30, Top = 40, Text = "B" }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([first, second])
        };

        viewModel.SelectDesignItems([first, second], first);
        var leftRow = Assert.Single(viewModel.PropertyEditorItems, item => item.PropertyName == nameof(TextFrontedControlConfig.Left));

        Assert.True(leftRow.IsMultiSelectionMixedValue);
        Assert.Equal(string.Empty, leftRow.EditText);
        Assert.True(viewModel.ApplyPropertyEdit(leftRow, string.Empty));

        Assert.Equal(10, first.Config.Left);
        Assert.Equal(30, second.Config.Left);
        Assert.Equal(20, first.Config.Top);
        Assert.Equal(40, second.Config.Top);

        Assert.True(viewModel.ApplyPropertyEdit(leftRow, "50"));

        Assert.Equal(50, first.Config.Left);
        Assert.Equal(50, second.Config.Left);
        Assert.Equal(20, first.Config.Top);
        Assert.Equal(40, second.Config.Top);
        Assert.Equal("A", Assert.IsType<TextFrontedControlConfig>(first.Config).Text);
        Assert.Equal("B", Assert.IsType<TextFrontedControlConfig>(second.Config).Text);
    }

    [Fact]
    public void DesignerViewModelMultiSelectionBindingRowsAreBlankAndReadOnly()
    {
        var first = new FrontedControlDesignItem
        {
            Name = "First",
            Config = new ImageFrontedControlConfig { BindingPath = "CurrentGame.HomeTeam.Logo" }
        };
        var second = new FrontedControlDesignItem
        {
            Name = "Second",
            Config = new ImageFrontedControlConfig { BindingPath = "CurrentGame.AwayTeam.Logo" }
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([first, second])
        };

        viewModel.SelectDesignItems([first, second], first);
        var bindingRow = Assert.Single(viewModel.PropertyEditorItems, item => item.PropertyName == nameof(FrontedControlConfigBase.BindingPath));

        Assert.False(bindingRow.IsMultiSelectionBatchEditable);
        Assert.True(bindingRow.IsReadOnly);
        Assert.Equal(FrontedPropertyEditorKind.ReadOnly, bindingRow.EditorKind);
        Assert.Equal(string.Empty, bindingRow.DisplayValue);
        Assert.Equal(string.Empty, bindingRow.EditText);
        Assert.False(viewModel.ApplyPropertyEdit(bindingRow, "CurrentGame.Map.Name"));
        Assert.Equal("CurrentGame.HomeTeam.Logo", first.Config.BindingPath);
        Assert.Equal("CurrentGame.AwayTeam.Logo", second.Config.BindingPath);
    }

    [Fact]
    public void DesignerViewModelApplyPropertyEditDoesNotBatchMixedControlTypes()
    {
        var text = new FrontedControlDesignItem
        {
            Name = "Text",
            Config = new TextFrontedControlConfig { Text = "A" }
        };
        var image = new FrontedControlDesignItem
        {
            Name = "Image",
            Config = new ImageFrontedControlConfig()
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([text, image])
        };

        viewModel.SelectDesignItems([text, image], text);
        var applied = viewModel.ApplyPropertyEdit(TextEditorRow(nameof(TextFrontedControlConfig.Text)), "OnlyText");

        Assert.True(applied);
        Assert.Equal("OnlyText", Assert.IsType<TextFrontedControlConfig>(text.Config).Text);
        Assert.IsType<ImageFrontedControlConfig>(image.Config);
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
        Assert.Equal("Image", rows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.ImagePath)).GroupName);
        Assert.Equal("Image", rows.Single(row => row.PropertyName == nameof(BorderedImageFrontedControlConfig.ImageWidth)).GroupName);
        Assert.Equal("Image", rows.Single(row => row.PropertyName == nameof(BorderedImageFrontedControlConfig.ImageHeight)).GroupName);
        Assert.Equal("Image", rows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.Stretch)).GroupName);
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ImageFrontedControlConfig.PickingBorder));
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ImageFrontedControlConfig.BanLockAvailable));
    }

    [Fact]
    public void PropertyGridBuilderHidesPureImageNoOpRows()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Map",
            Config = new ImageFrontedControlConfig
            {
                SizingMode = ImageSizingMode.FillContainer,
                Stretch = "UniformToFill",
                PickingBorder = true,
                BanLockAvailable = true
            }
        };

        var rows = BuildPropertyRows(CreateDocument([item]), item);

        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ImageFrontedControlConfig.SizingMode));
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ImageFrontedControlConfig.PickingBorder));
        Assert.DoesNotContain(rows, row => row.PropertyName == nameof(ImageFrontedControlConfig.BanLockAvailable));
        Assert.Contains(rows, row => row.PropertyName == nameof(ImageFrontedControlConfig.Stretch));
    }

    [Theory]
    [InlineData("Image", typeof(ImageFrontedControlConfig), "Resource")]
    [InlineData("BorderedImage", typeof(BorderedImageFrontedControlConfig), "Image")]
    public void PropertyGridBuilderExposesImagePathAsResourceBrowser(
        string name,
        Type configType,
        string expectedGroup)
    {
        var item = new FrontedControlDesignItem
        {
            Name = name,
            Config = (FrontedControlConfigBase)Activator.CreateInstance(configType)!
        };

        var rows = BuildPropertyRows(CreateDocument([item]), item);
        var imagePathRow = Assert.Single(rows, row => row.PropertyName == nameof(ImageFrontedControlConfig.ImagePath));

        Assert.True(imagePathRow.CanBrowseResource);
        Assert.False(imagePathRow.CanBrowseBinding);
        Assert.Equal(expectedGroup, imagePathRow.GroupName);
        Assert.Equal(FrontedPropertyEditorKind.Text, imagePathRow.EditorKind);
    }

    [Fact]
    public void PropertyGridBuilderMarksOnlySensitiveTextRowsAsExplicitCommit()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Image",
            Config = new ImageFrontedControlConfig
            {
                Left = 10,
                BindingPath = "CurrentGame.SurTeam.Logo",
                ImagePath = "Resources/logo.png",
                PickingBorderAvailable = true,
                PickingBorderName = "PickingBorder"
            }
        };
        var text = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig
            {
                Text = "Title",
                FontFamily = "Arial",
                Color = "#FFFFFFFF"
            }
        };

        var imageRows = BuildPropertyRows(CreateDocument([item]), item);
        var textRows = BuildPropertyRows(CreateDocument([text]), text);

        Assert.True(imageRows.Single(row => row.PropertyName == nameof(FrontedControlDesignItem.Name)).RequiresExplicitCommit);
        Assert.True(imageRows.Single(row => row.PropertyName == nameof(FrontedControlConfigBase.BindingPath)).RequiresExplicitCommit);
        Assert.True(imageRows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.ImagePath)).RequiresExplicitCommit);
        Assert.True(imageRows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.PickingBorderName)).RequiresExplicitCommit);
        Assert.False(imageRows.Single(row => row.PropertyName == nameof(FrontedControlConfigBase.Left)).RequiresExplicitCommit);
        Assert.False(textRows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.Text)).RequiresExplicitCommit);
        Assert.False(textRows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.Color)).RequiresExplicitCommit);
        Assert.True(textRows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.FontFamily)).RequiresExplicitCommit);
    }

    [Fact]
    public void PropertyGridBuilderAppliesNameReadOnlyRules()
    {
        var nonEditable = new FrontedControlDesignItem
        {
            Name = "Overlay",
            IsEditableInEditor = false,
            Config = new ImageFrontedControlConfig()
        };
        var normal = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig()
        };

        var nonEditableRows = BuildPropertyRows(
            CreateDocument([nonEditable], "BpWindow"),
            nonEditable);
        var normalRows = BuildPropertyRows(CreateDocument([normal]), normal);

        Assert.True(nonEditableRows.Single(row => row.PropertyName == nameof(FrontedControlDesignItem.Name)).IsReadOnly);
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
            Config = new BorderedImageFrontedControlConfig
            {
                ClipToBounds = true,
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
            imageRows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.ClipToBounds)).EditorKind);
        Assert.Equal(
            FrontedPropertyEditorKind.Enum,
            imageRows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.SizingMode)).EditorKind);
    }

    [Fact]
    public void MapV2BorderColorsAppearAsColorPropertiesInBorderGroup()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Map",
            Config = new MapV2DisplayControlConfig()
        };

        var rows = BuildPropertyRows(CreateDocument([item]), item);
        var normal = rows.Single(row => row.PropertyName == nameof(MapV2DisplayControlConfig.MapBorderNormalColor));
        var banned = rows.Single(row => row.PropertyName == nameof(MapV2DisplayControlConfig.MapBorderBannedColor));

        Assert.Equal(FrontedPropertyEditorKind.Color, normal.EditorKind);
        Assert.Equal("Border", normal.GroupName);
        Assert.Equal(FrontedPropertyEditorKind.Color, banned.EditorKind);
        Assert.Equal("Border", banned.GroupName);
    }

    [Fact]
    public void MapV2DisplayStyleCanApplyToAllSameTypeControlsIncludingSizeWithoutChangingPositionOrBinding()
    {
        var source = new FrontedControlDesignItem
        {
            Name = "Map0",
            Config = new MapV2DisplayControlConfig
            {
                MapKey = "Map0",
                Left = 10,
                Top = 20,
                Width = 300,
                Height = 180,
                ZIndex = 2,
                Visibility = FrontedControlVisibility.Hidden,
                BindingPath = "Source.Binding",
                MapNameFontFamily = "Source Map Font",
                MapNameFontWeight = "Bold",
                MapNameColor = "#FF010203",
                MapNameFontSize = 20,
                TeamNameFontFamily = "Source Team Font",
                TeamNameFontWeight = "SemiBold",
                TeamNameColor = "#FF040506",
                TeamNameFontSize = 18,
                CampNameFontFamily = "Source Camp Font",
                CampNameFontWeight = "Medium",
                CampNameColor = "#FF070809",
                CampNameFontSize = 16,
                MapBorderNormalColor = "#FF112233",
                MapBorderBannedColor = "#FF445566",
                PickingBorderImagePath = "Resources/picking.png",
                PickingBorderFillColor = "#FF778899"
            }
        };
        var target = new FrontedControlDesignItem
        {
            Name = "Map1",
            Config = new MapV2DisplayControlConfig
            {
                MapKey = "Map1",
                Left = 100,
                Top = 200,
                Width = 320,
                Height = 190,
                ZIndex = 7,
                Visibility = FrontedControlVisibility.Collapsed,
                BindingPath = "Target.Binding",
                MapNameColor = "#FFFFFFFF",
                MapBorderNormalColor = "#FF000000",
                PickingBorderImagePath = "Resources/old.png"
            }
        };
        var unrelated = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Color = "#FFAABBCC" }
        };
        var document = CreateDocument([source, target, unrelated]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.SelectDesignItem(source);

        Assert.True(viewModel.IsMapV2DisplaySelected);

        viewModel.ApplyMapV2DisplayStyleToAllCommand.Execute(null);

        var targetConfig = Assert.IsType<MapV2DisplayControlConfig>(target.Config);
        Assert.Equal("Source Map Font", targetConfig.MapNameFontFamily);
        Assert.Equal("#FF010203", targetConfig.MapNameColor);
        Assert.Equal("Source Team Font", targetConfig.TeamNameFontFamily);
        Assert.Equal("#FF040506", targetConfig.TeamNameColor);
        Assert.Equal("Source Camp Font", targetConfig.CampNameFontFamily);
        Assert.Equal("#FF070809", targetConfig.CampNameColor);
        Assert.Equal("#FF112233", targetConfig.MapBorderNormalColor);
        Assert.Equal("#FF445566", targetConfig.MapBorderBannedColor);
        Assert.Equal("Resources/picking.png", targetConfig.PickingBorderImagePath);
        Assert.Equal("#FF778899", targetConfig.PickingBorderFillColor);
        Assert.Equal("Map1", targetConfig.MapKey);
        Assert.Equal(100, targetConfig.Left);
        Assert.Equal(200, targetConfig.Top);
        Assert.Equal(300, targetConfig.Width);
        Assert.Equal(180, targetConfig.Height);
        Assert.Equal(7, targetConfig.ZIndex);
        Assert.Equal(FrontedControlVisibility.Collapsed, targetConfig.Visibility);
        Assert.Equal("Target.Binding", targetConfig.BindingPath);
        Assert.Equal("#FFAABBCC", Assert.IsType<TextFrontedControlConfig>(unrelated.Config).Color);
        Assert.True(document.IsDirty);
        Assert.True(viewModel.CanUndo);

        viewModel.UndoCommand.Execute(null);

        var restoredTarget = Assert.IsType<MapV2DisplayControlConfig>(
            viewModel.CurrentDocument!.Controls.Single(item => item.Name == "Map1").Config);
        Assert.Equal("#FFFFFFFF", restoredTarget.MapNameColor);
        Assert.Equal("#FF000000", restoredTarget.MapBorderNormalColor);
        Assert.Equal("Resources/old.png", restoredTarget.PickingBorderImagePath);
        Assert.Equal("Map1", restoredTarget.MapKey);
        Assert.Equal(100, restoredTarget.Left);
        Assert.Equal(320, restoredTarget.Width);
        Assert.Equal(190, restoredTarget.Height);

        viewModel.SelectDesignItem(viewModel.CurrentDocument.Controls.Single(item => item.Name == "Title"));
        Assert.False(viewModel.IsMapV2DisplaySelected);
    }

    [Fact]
    public void MapV2DisplayStyleApplyToAllAlsoReplacesBehaviorsAndRewritesMapKeyFilters()
    {
        var sourceGuid = Guid.NewGuid();
        var targetGuid = Guid.NewGuid();
        var source = new FrontedControlDesignItem
        {
            Name = "Map0",
            Config = new MapV2DisplayControlConfig
            {
                BehaviorGuid = sourceGuid,
                MapKey = "ArmsFactory",
                Width = 300,
                Height = 180
            }
        };
        var target = new FrontedControlDesignItem
        {
            Name = "Map1",
            Config = new MapV2DisplayControlConfig
            {
                BehaviorGuid = targetGuid,
                MapKey = "TheRedChurch",
                Width = 100,
                Height = 80
            }
        };
        var document = CreateDocument([source, target]);
        var behavior = new FrontedBehavior
        {
            Name = "PickingBorderBreathing",
            Kind = FrontedBehaviorKind.Loop,
            StartTrigger = new TriggerDescriptor
            {
                EventType = "MapV2.PickingBorderStateChanged",
                Filters =
                [
                    new TriggerFilter { Left = "Event.MapKey", Right = "ArmsFactory" },
                    new TriggerFilter { Left = "Event.IsPickingBorderVisible", Right = "true" }
                ]
            },
            StopTriggers =
            [
                new TriggerDescriptor
                {
                    EventType = "MapV2.PickingBorderStateChanged",
                    Filters =
                    [
                        new TriggerFilter { Left = "StopEvent.MapKey", Right = "ArmsFactory" },
                        new TriggerFilter { Left = "StopEvent.IsPickingBorderVisible", Right = "false" }
                    ]
                }
            ],
            StartGraph = new FrontedNodeGraph
            {
                Nodes =
                [
                    new FrontedNode
                    {
                        NodeType = "action.animate",
                        Properties =
                        {
                            ["Target"] = JsonSerializer.SerializeToElement($"part:{sourceGuid}:PickingBorder")
                        }
                    },
                    new FrontedNode
                    {
                        NodeType = "flow.if",
                        Properties =
                        {
                            ["Left"] = JsonSerializer.SerializeToElement("Event.MapKey"),
                            ["Operator"] = JsonSerializer.SerializeToElement("Equals"),
                            ["Right"] = JsonSerializer.SerializeToElement("ArmsFactory")
                        }
                    }
                ]
            }
        };
        var oldTargetBehavior = new FrontedBehavior { Name = "OldTargetBehavior" };
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };
        viewModel.BehaviorPanel.SetDocument(new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = "TestWindow",
            CanvasName = "BaseCanvas",
            ControlBehaviorSets =
            [
                new ControlBehaviorSet
                {
                    BehaviorGuid = sourceGuid,
                    DisplayName = "Map0",
                    Behaviors = [behavior]
                },
                new ControlBehaviorSet
                {
                    BehaviorGuid = targetGuid,
                    DisplayName = "Map1",
                    Behaviors = [oldTargetBehavior]
                }
            ]
        });
        viewModel.SelectDesignItem(source);

        viewModel.ApplyMapV2DisplayStyleToAllCommand.Execute(null);

        var targetSet = Assert.Single(
            viewModel.BehaviorPanel.CurrentDocument.ControlBehaviorSets,
            set => set.BehaviorGuid == targetGuid);
        Assert.Equal("Map1", targetSet.DisplayName);
        var copied = Assert.Single(targetSet.Behaviors);
        Assert.NotEqual(behavior.BehaviorId, copied.BehaviorId);
        Assert.Equal("PickingBorderBreathing", copied.Name);
        Assert.DoesNotContain(targetSet.Behaviors, item => item.Name == "OldTargetBehavior");
        Assert.Equal(
            $"part:{targetGuid}:PickingBorder",
            copied.StartGraph.Nodes[0].Properties["Target"].GetString());
        Assert.Equal("TheRedChurch", copied.StartTrigger!.Filters.Single(filter => filter.Left == "Event.MapKey").Right);
        Assert.Equal("TheRedChurch", copied.StopTriggers[0].Filters.Single(filter => filter.Left == "StopEvent.MapKey").Right);
        Assert.Equal("TheRedChurch", copied.StartGraph.Nodes[1].Properties["Right"].GetString());
        Assert.True(viewModel.AreBehaviorsDirty);

        viewModel.UndoCommand.Execute(null);

        var restoredTargetSet = Assert.Single(
            viewModel.BehaviorPanel.CurrentDocument.ControlBehaviorSets,
            set => set.BehaviorGuid == targetGuid);
        Assert.Equal("OldTargetBehavior", Assert.Single(restoredTargetSet.Behaviors).Name);
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
            .Single(entry => entry.WindowTypeName == "BpWindow");

        Assert.Equal("Text", new TextFrontedControlConfig().ControlType);
        Assert.Equal("文本", localizer.GetControlTypeDisplayName("Text"));
        Assert.Equal("PluginFancyControl", localizer.GetControlTypeDisplayName("PluginFancyControl"));
        Assert.Equal("BpWindow", catalogEntry.WindowTypeName);
        Assert.Equal("BP 主窗口", localizer.GetWindowDisplayName(catalogEntry.WindowTypeName));
        Assert.Equal("主画布", localizer.GetCanvasDisplayName("BaseCanvas"));
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
    public void PropertyGridInvalidColorMessageUsesDesignerLocalization()
    {
        var localizer = new TestDesignerLocalizationService(
            designerTexts: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Designer.Validation.InvalidArgbColor"] = "颜色格式不正确"
            });
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Color = "not-a-color" }
        };

        var rows = BuildPropertyRows(CreateDocument([item]), item, localizer);
        var colorRow = rows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.Color));

        Assert.Contains("颜色格式不正确", colorRow.ValidationErrors);
        Assert.DoesNotContain("Invalid color. Use #RRGGBB or #AARRGGBB.", colorRow.ValidationErrors);
    }

    [Fact]
    public void ResourceBrowserLocalizationKeepsRawResourceUriVisible()
    {
        var localizer = new TestDesignerLocalizationService(
            designerTexts: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Designer.Editor.Source.BuiltInResources"] = "内置资源",
                ["Designer.Editor.Type.Image"] = "图片"
            });
        var root = CreateTempDirectory();
        try
        {
            var bpui = Path.Combine(root, "bpui");
            Directory.CreateDirectory(bpui);
            var imagePath = Path.Combine(bpui, "sample.png");
            File.WriteAllText(imagePath, "not a real image");
            var provider = new FrontedResourceBrowserProvider(
                root,
                new AllowAllImageSafetyService(),
                localizer);

            var item = Assert.Single(provider.Search("sample"));

            Assert.Equal("sample.png", item.DisplayName);
            Assert.Equal("内置资源 / 图片", item.SourceAndTypeDisplayName);
            Assert.Equal("Resources/sample.png", item.SelectedPath);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public void DesignerPropertyGridLocalizationKeysCoverBuiltInConfigPropertiesAndOptions()
    {
        var requiredKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "Designer.Property.Name",
            "Designer.Value.True",
            "Designer.Value.False",
            "Designer.Editor.Search",
            "Designer.Editor.Select",
            "Designer.Editor.Cancel",
            "Designer.Editor.NoResults",
            "Designer.Editor.RawPath",
            "Designer.Editor.RawUri",
            "Designer.Editor.ExpectedType",
            "Designer.Editor.BindingBrowser",
            "Designer.Editor.ResourceBrowser",
            "Designer.Validation.InvalidArgbColor",
            "Designer.PropertyGroup.Overlay",
            "Designer.ControlType.GameProgress",
            "Designer.ControlType.MapName"
        };

        foreach (var type in BuiltInConfigTypes())
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(property => property.GetIndexParameters().Length == 0 && property.CanRead
                                            && !property.IsDefined(typeof(JsonIgnoreAttribute), inherit: true)))
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
                    // DisplayLanguage 选项使用自定义键（zh_Hans/en_US/ja_JP），不生成 Designer.Option.* 条目
                    if (row.PropertyName == "DisplayLanguage")
                        continue;

                    if (option.Value is bool boolValue)
                    {
                        requiredKeys.Add(boolValue ? "Designer.Value.True" : "Designer.Value.False");
                        continue;
                    }

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
    public void GameProgressDisplayModePropertyOffersEightOfficialPresets()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "GameProgress",
            Config = new GameProgressTextControlConfig()
        };

        var rows = BuildPropertyRows(CreateDocument([item]), item);
        var displayModeRow = rows.Single(row => row.PropertyName == nameof(GameProgressTextControlConfig.DisplayMode));
        var values = displayModeRow.Options!
            .OfType<FrontedPropertyEditorOption>()
            .Select(option => Assert.IsType<GameProgressTextDisplayMode>(option.Value))
            .ToArray();

        Assert.Equal(
            [
                GameProgressTextDisplayMode.Inline,
                GameProgressTextDisplayMode.TwoLine,
                GameProgressTextDisplayMode.HorizontalGameOnly,
                GameProgressTextDisplayMode.HorizontalHalfOnly,
                GameProgressTextDisplayMode.Vertical,
                GameProgressTextDisplayMode.VerticalTwoLine,
                GameProgressTextDisplayMode.VerticalGameOnly,
                GameProgressTextDisplayMode.VerticalHalfOnly
            ],
            values);

        Assert.DoesNotContain(GameProgressTextDisplayMode.VerticalGameAndHalf, values);
        Assert.DoesNotContain(GameProgressTextDisplayMode.VerticalSeparatedGameAndHalf, values);
        Assert.DoesNotContain(GameProgressTextDisplayMode.RibbonGameOnly, values);
    }

    [Fact]
    public void PropertyColorHelperParsesFormatsAndFallsBackSafely()
    {
        Assert.True(FrontedPropertyColorHelper.TryParseArgbColor("#FFFFFFFF", out var color));
        Assert.Equal(Colors.White, color);
        Assert.Equal("#FFFFFFFF", FrontedPropertyColorHelper.ToArgbString(color));
        Assert.True(FrontedPropertyColorHelper.TryParseArgbColor("#112233", out var rgbColor));
        Assert.Equal(Color.FromArgb(0xFF, 0x11, 0x22, 0x33), rgbColor);
        Assert.Equal("#FF112233", FrontedPropertyColorHelper.ToArgbString(rgbColor));
        Assert.False(FrontedPropertyColorHelper.TryParseArgbColor("not-a-color", out var fallback));
        Assert.Equal(FrontedPropertyColorHelper.FallbackColor, fallback);
    }

    [Fact]
    public void PropertyGridAcceptsStoredRgbColorWithoutValidationError()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig { Color = "#112233" }
        };

        var rows = BuildPropertyRows(CreateDocument([item]), item);
        var colorRow = rows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.Color));

        Assert.Empty(colorRow.ValidationErrors);
        Assert.Equal(Color.FromArgb(0xFF, 0x11, 0x22, 0x33), colorRow.ColorValue);
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
    public void RgbColorCommitNormalizesToOpaqueArgb()
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
            EditText = "#112233"
        };

        var result = viewModel.ApplyPropertyEdit(row, row.EditText);

        Assert.True(result);
        Assert.Equal("#FF112233", ((TextFrontedControlConfig)item.Config).Color);
        Assert.Equal("#FF112233", row.EditText);
        Assert.False(row.HasEditError);
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
    public async Task FontFamilyOptionProviderPlacesActivePackageFontsFirst()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var builtInRoot = Path.Combine(root, "builtIn");
            var packageId = "package-fonts";
            var packagePath = Path.Combine(packageRoot, packageId);
            var fontsPath = Path.Combine(packagePath, "resources", "fonts");
            Directory.CreateDirectory(fontsPath);
            Directory.CreateDirectory(Path.Combine(packagePath, "FrontedLayouts"));
            File.Copy(
                GetRepositoryPath("neo-bpsys-wpf", "Assets", "Fonts", "NotoSans-Regular.ttf"),
                Path.Combine(fontsPath, "NotoSans-Regular.ttf"));
            File.WriteAllText(
                Path.Combine(packagePath, "manifest.json"),
                JsonSerializer.Serialize(new neo_bpsys_wpf.Core.Models.FrontedLayout.Packages.FrontedLayoutPackageManifest
                {
                    PackageId = packageId,
                    Name = packageId
                }));

            var manager = new FrontedLayoutPackageManager(packageRoot, builtInRoot);
            await manager.ActivatePackageAsync(packageId, TestContext.Current.CancellationToken);
            var provider = new FrontedFontFamilyOptionProvider(manager);

            var options = provider.GetFontFamilyOptions();

            Assert.NotEmpty(options);
            Assert.True(options[0].IsPackageFont);
            Assert.Equal("BPUI", options[0].BadgeText);
            Assert.StartsWith($"bpui://{packageId}/resources/fonts/NotoSans-Regular.ttf#", options[0].Value);
            Assert.Contains(options, option => option.IsBuiltIn);
            Assert.Contains(options, option => !option.IsBuiltIn && !option.IsPackageFont);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public void LocalResourceStoreCopiesPackageFontAndRejectsUnsupportedExtension()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "package");
            var store = new FrontedLocalResourceStore();
            var results = store.StorePackageFontWithResult(
                GetRepositoryPath("neo-bpsys-wpf", "Assets", "Fonts", "NotoSans-Regular.ttf"),
                "package-fonts",
                packageRoot);

            var result = Assert.Single(results);
            Assert.Equal("Noto Sans", result.FontFamilyName);
            Assert.StartsWith("bpui://package-fonts/resources/fonts/NotoSans-Regular-", result.ResourceUri);
            Assert.EndsWith("#Noto Sans", result.ResourceUri);
            Assert.True(File.Exists(result.PhysicalPath));

            var unsupported = Path.Combine(root, "font.txt");
            File.WriteAllText(unsupported, "not a font");
            Assert.Throws<NotSupportedException>(() => store.StorePackageFontWithResult(
                unsupported,
                "package-fonts",
                packageRoot));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task PackageFontManagerBlocksReferencedFontAndDeletesUnreferencedFont()
    {
        var root = CreateTempDirectory();
        try
        {
            var packageRoot = Path.Combine(root, "packages");
            var builtInRoot = Path.Combine(root, "builtIn");
            var packageId = "package-font-manager";
            var packagePath = Path.Combine(packageRoot, packageId);
            var layoutsPath = Path.Combine(packagePath, "FrontedLayouts");
            var fontsPath = Path.Combine(packagePath, "resources", "fonts");
            var fontFileName = "NotoSans-Regular.ttf";
            var fontPath = Path.Combine(fontsPath, fontFileName);
            var fontUri = $"bpui://{packageId}/resources/fonts/{fontFileName}#Noto Sans";
            Directory.CreateDirectory(layoutsPath);
            Directory.CreateDirectory(fontsPath);
            File.Copy(
                GetRepositoryPath("neo-bpsys-wpf", "Assets", "Fonts", fontFileName),
                fontPath);
            File.WriteAllText(
                Path.Combine(packagePath, "manifest.json"),
                JsonSerializer.Serialize(new neo_bpsys_wpf.Core.Models.FrontedLayout.Packages.FrontedLayoutPackageManifest
                {
                    PackageId = packageId,
                    Name = packageId
                }));
            var layoutPath = Path.Combine(layoutsPath, "BpWindow.json");
            File.WriteAllText(layoutPath, JsonSerializer.Serialize(new { FontFamily = fontUri }));

            var packageManager = new FrontedLayoutPackageManager(packageRoot, builtInRoot);
            await packageManager.ActivatePackageAsync(packageId, TestContext.Current.CancellationToken);
            var fontManager = new FrontedPackageFontManager(packageManager);

            var referenced = Assert.Single(await fontManager.ListActivePackageFontsAsync(TestContext.Current.CancellationToken));
            Assert.True(referenced.IsReferenced);
            Assert.False(referenced.CanDelete);
            Assert.Contains("Noto Sans", referenced.FontFamilyNames);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fontManager.DeleteActivePackageFontAsync(fontFileName, TestContext.Current.CancellationToken));

            File.WriteAllText(layoutPath, JsonSerializer.Serialize(new { FontFamily = "Arial" }));
            var unreferenced = Assert.Single(await fontManager.ListActivePackageFontsAsync(TestContext.Current.CancellationToken));
            Assert.False(unreferenced.IsReferenced);
            Assert.True(unreferenced.CanDelete);

            await fontManager.DeleteActivePackageFontAsync(fontFileName, TestContext.Current.CancellationToken);

            Assert.False(File.Exists(fontPath));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
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
    public void FontFamilyPropertyRowDisplaysFontNameInsteadOfStoredUri()
    {
        const string builtInFont = "pack://application:,,,/Assets/Fonts/#Noto Sans";
        var item = new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig
            {
                FontFamily = builtInFont
            }
        };
        var document = CreateDocument([item]);

        var rows = BuildPropertyRows(document, item);

        var row = rows.Single(row => row.PropertyName == nameof(TextFrontedControlConfig.FontFamily));
        Assert.Equal(builtInFont, row.Value);
        Assert.Equal("Noto Sans", row.EditText);
        Assert.DoesNotContain("pack://", row.EditText, StringComparison.OrdinalIgnoreCase);
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
    public void GlobalScoreRowParentMovePreservesChildRelativePositions()
    {
        var (viewModel, rowItem, cell) = CreateGlobalScoreRowDesigner();
        viewModel.SelectDesignItem(rowItem);

        viewModel.MoveSelectedDesignItem(10, 20, 5, 7, renderPreview: false);

        Assert.Equal(15, rowItem.Config.Left);
        Assert.Equal(27, rowItem.Config.Top);
        Assert.Equal(12, cell.X);
        Assert.Equal(4, cell.Y);
    }

    [Fact]
    public void GlobalScoreRowChildMoveStoresRelativeCoordinatesOnly()
    {
        var (viewModel, rowItem, cell) = CreateGlobalScoreRowDesigner();
        viewModel.SelectGlobalScoreCell(rowItem, cell);

        viewModel.MoveSelectedGlobalScoreCell(12, 4, 8, 6, renderPreview: false);

        Assert.Equal(10, rowItem.Config.Left);
        Assert.Equal(20, rowItem.Config.Top);
        Assert.Equal(20, cell.X);
        Assert.Equal(8, cell.Y);
    }

    [Fact]
    public void GlobalScoreRowChildPropertyEditUpdatesOnlySelectedCell()
    {
        var (viewModel, rowItem, cell) = CreateGlobalScoreRowDesigner();
        var other = ((GlobalScoreRowControlConfig)rowItem.Config).Cells[1];
        viewModel.SelectGlobalScoreCell(rowItem, cell);
        var xRow = viewModel.PropertyEditorItems.Single(row => row.PropertyName == nameof(GlobalScoreCellConfig.X));

        Assert.True(viewModel.ApplyPropertyEdit(xRow, "44"));

        Assert.Equal(44, cell.X);
        Assert.Equal(102, other.X);
    }

    [Fact]
    public void GlobalScoreRowChildMoveAndResizeAreUndoable()
    {
        var (viewModel, rowItem, cell) = CreateGlobalScoreRowDesigner();
        viewModel.SelectGlobalScoreCell(rowItem, cell);

        viewModel.CaptureUndoSnapshot();
        viewModel.MoveSelectedGlobalScoreCell(12, 4, 30, 0, renderPreview: false);
        viewModel.CommitDesignItemGeometryEdit();
        Assert.Equal(42, cell.X);

        viewModel.UndoCommand.Execute(null);

        var rowAfterUndo = Assert.IsType<GlobalScoreRowControlConfig>(viewModel.CurrentDocument!.Controls[0].Config);
        Assert.Equal(12, rowAfterUndo.Cells[0].X);

        viewModel.SelectGlobalScoreCell(viewModel.CurrentDocument.Controls[0], rowAfterUndo.Cells[0]);
        viewModel.CaptureUndoSnapshot();
        viewModel.ResizeSelectedGlobalScoreCell(
            FrontedDesignerResizeHandleKind.BottomRight,
            12,
            4,
            75,
            32,
            20,
            10,
            renderPreview: false);
        Assert.Equal(95, rowAfterUndo.Cells[0].Width);

        viewModel.UndoCommand.Execute(null);

        var rowAfterResizeUndo = Assert.IsType<GlobalScoreRowControlConfig>(viewModel.CurrentDocument!.Controls[0].Config);
        Assert.Equal(75, rowAfterResizeUndo.Cells[0].Width);
        Assert.Equal(32, rowAfterResizeUndo.Cells[0].Height);
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
    public void ApplyPropertyEditRefusesInvalidAndDuplicateNames()
    {
        var title = new FrontedControlDesignItem { Name = "Title", Config = new TextFrontedControlConfig() };
        var logo = new FrontedControlDesignItem { Name = "Logo", Config = new ImageFrontedControlConfig() };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([title, logo], "BpWindow")
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
    public void ApplyPropertyEdit_AllowsRenamingToDeletedControlName()
    {
        var text1 = new FrontedControlDesignItem { Name = "Text1", Config = new TextFrontedControlConfig() };
        var text2 = new FrontedControlDesignItem { Name = "Text2", Config = new TextFrontedControlConfig() };
        var document = CreateDocument([text1, text2]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        // 删除 "Text1"
        viewModel.SelectDesignItem(text1);
        document.Controls.Remove(text1);

        // 选择 "Text2" 并重命名为 "Text1"
        viewModel.SelectDesignItem(text2);
        var row = NameEditorRow();
        var result = viewModel.ApplyPropertyEdit(row, "Text1");

        Assert.True(result);
        Assert.Equal("Text1", text2.Name);
        Assert.False(row.HasEditError);
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
        Assert.Contains("CurrentGame.SurPlayerList[0].PictureShownWithFullCharacter", paths);
        Assert.Contains("CurrentGame.SurPlayerList[0].PictureShownHeader", paths);
        Assert.Contains("CurrentGame.HunPlayer.Member.Name", paths);
        Assert.Contains("CurrentGame.HunPlayer.PictureShownWithFullCharacter", paths);
        Assert.Contains("CurrentGame.HunPlayer.PictureShownHeader", paths);
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
        Assert.Contains("CurrentGame.GameProgress", paths);
        Assert.Contains("CurrentGame.PickedMap", paths);
    }

    [Fact]
    public void BindingBrowserImageFilterIncludesOnlyImageCompatiblePaths()
    {
        var provider = new FrontedBindingBrowserProvider();
        var paths = BindingSearchPaths(provider, FrontedBindingTypeFilter.Image);

        Assert.Contains("CurrentGame.SurTeam.Logo", paths);
        Assert.Contains("CurrentGame.PickedMapImage", paths);
        Assert.Contains("CurrentGame.SurPlayerList[0].PictureShown", paths);
        Assert.Contains("CurrentGame.SurPlayerList[0].PictureShownWithFullCharacter", paths);
        Assert.Contains("CurrentGame.SurPlayerList[0].PictureShownHeader", paths);
        Assert.Contains("CurrentGame.SurPlayerList[0].Character.HeaderImageSingleColor", paths);
        Assert.Contains("CurrentGame.SurPlayerList[0].Character.HalfImage", paths);
        Assert.Contains("CurrentGame.SurPlayerList[0].Character.BigImage", paths);
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
    public void BindingBrowserBooleanFilterIncludesFixedCanBanLists()
    {
        var provider = new FrontedBindingBrowserProvider();
        var paths = BindingSearchPaths(provider, new FrontedBindingTypeFilter(FrontedBindingTargetKind.Boolean));

        Assert.Contains("CanCurrentSurBannedList[0]", paths);
        Assert.Contains("CanCurrentHunBannedList[0]", paths);
        Assert.Contains("CanGlobalSurBannedList[0]", paths);
        Assert.Contains("CanGlobalHunBannedList[0]", paths);
        Assert.DoesNotContain("CurrentGame.SurTeam.Name", paths);
    }

    [Fact]
    public void ReflectionBindingCatalogHonorsAttributesWithoutInvokingGetters()
    {
        var provider = new FrontedBindingReflectionCatalogProvider(
            new TestBindingRootProvider(),
            []);

        var paths = provider.BuildCatalog()
            .SelectMany(node => node.Flatten())
            .Select(node => node.FullPath)
            .Where(path => path is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Root.Name", paths);
        Assert.Contains("Root.Children[0].Name", paths);
        Assert.DoesNotContain("Root.Hidden", paths);
        Assert.DoesNotContain("Root.GetterThatThrows", paths);
        Assert.DoesNotContain("Root.IsActive", paths);
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
        Assert.True(allPaths.Length < 800);
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
        var imageItem = new FrontedControlDesignItem
        {
            Name = "SurPick",
            Config = new ImageFrontedControlConfig
            {
                BindingPath = "CurrentGame.SurPlayerList[0].PictureShown"
            }
        };

        var rows = BuildPropertyRows(CreateDocument([imageItem]), imageItem);

        var bindingRow = Assert.Single(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.BindingPath));
        Assert.True(bindingRow.CanBrowseBinding);
        Assert.False(bindingRow.CanBrowseResource);

        var lockPathRow = Assert.Single(rows, row => row.PropertyName == nameof(ImageFrontedControlConfig.LockVisibilityBindingPath));
        Assert.True(lockPathRow.CanBrowseBinding);
        Assert.Equal(FrontedBindingTargetKind.Boolean, lockPathRow.BindingTargetKind);

        var lockImageRow = Assert.Single(rows, row => row.PropertyName == nameof(ImageFrontedControlConfig.LockImagePath));
        Assert.True(lockImageRow.CanBrowseResource);
        Assert.False(lockImageRow.CanBrowseBinding);
        Assert.Equal("Overlay", lockImageRow.GroupName);

        var normalTextRow = rows.Single(row => row.PropertyName == nameof(ImageFrontedControlConfig.HorizontalAlignment));
        Assert.False(normalTextRow.CanBrowseBinding);
        Assert.False(normalTextRow.CanBrowseResource);
    }

    [Theory]
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
    public void PropertyGridBuilderUsesDedicatedTextBindingEditorForTextControls()
    {
        foreach (var config in new FrontedControlConfigBase[]
                 {
                     new TextFrontedControlConfig(),
                     new LocalizedTextControlConfig()
                 })
        {
            var item = new FrontedControlDesignItem { Name = config.ControlType, Config = config };
            var rows = BuildPropertyRows(CreateDocument([item]), item);
            var bindingRow = Assert.Single(rows, row => row.PropertyName == nameof(TextFrontedControlConfig.TextBinding));

            Assert.Equal(FrontedPropertyEditorKind.TextBinding, bindingRow.EditorKind);
            Assert.DoesNotContain(rows, row => row.PropertyName == nameof(FrontedControlConfigBase.BindingPath));
        }
    }

    [Fact]
    public void PropertyGridLocalizesTextBindingNameGroupAndSummary()
    {
        var config = new TextFrontedControlConfig
        {
            TextBinding = new FrontedTextBindingExpression
            {
                Sources = [new FrontedBindingSourceConfig { Path = "CurrentGame.SurTeam.Name" }]
            }
        };
        var item = new FrontedControlDesignItem { Name = "Text", Config = config };
        var localizer = new TestDesignerLocalizationService(
            propertyNames: new Dictionary<string, string>
            {
                [nameof(TextFrontedControlConfig.TextBinding)] = "本地化文本绑定"
            },
            groupNames: new Dictionary<string, string>
            {
                ["Content"] = "本地化内容"
            },
            designerTexts: new Dictionary<string, string>
            {
                ["Designer.TextBinding.SourceSummary"] = "来源 {0}"
            });

        var row = Assert.Single(
            BuildPropertyRows(CreateDocument([item]), item, localizer),
            candidate => candidate.PropertyName == nameof(TextFrontedControlConfig.TextBinding));

        Assert.Equal("本地化文本绑定", row.DisplayName);
        Assert.Equal("本地化内容", row.GroupDisplayName);
        Assert.Equal("来源 1", row.DisplayValue);
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
    public void BrowserSelectionOnlyUpdatesImageBindingEditTextUntilExplicitApply()
    {
        var item = new FrontedControlDesignItem
        {
            Name = "Logo",
            Config = new ImageFrontedControlConfig { BindingPath = "Old.Path" }
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
    public void FrontedDesignerWindowXamlContainsCriticalToolbarContracts()
    {
        var xaml = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml"));

        Assert.Contains("Text=\"{Binding LayoutSourcePath}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip=\"{Binding LayoutSourcePath}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AddControlButton_OnClick", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenValidationDetails_OnClick", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenDesignerHelp_OnClick", xaml, StringComparison.Ordinal);
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

    [Theory]
    [InlineData(1D, 11D)]
    [InlineData(0.5D, 22D)]
    [InlineData(2D, 11D)]
    public void SelectionLabelFontSizeScalesWithZoomForReadability(double zoomScale, double expectedFontSize)
    {
        Assert.Equal(expectedFontSize, FrontedDesignerEditorVisualHelper.GetEffectiveSelectionLabelFontSize(zoomScale));
    }

    [Theory]
    [InlineData(double.NaN, 11D)]
    [InlineData(double.PositiveInfinity, 11D)]
    [InlineData(0D, 11D)]
    [InlineData(-1D, 11D)]
    public void SelectionLabelFontSizeFallsBackSafelyForInvalidZoom(double zoomScale, double expectedFontSize)
    {
        Assert.Equal(expectedFontSize, FrontedDesignerEditorVisualHelper.GetEffectiveSelectionLabelFontSize(zoomScale));
        Assert.Equal(FrontedDesignerEditorVisualHelper.SelectionLabelBaseOffset, FrontedDesignerEditorVisualHelper.GetEffectiveSelectionLabelTopOffset(zoomScale));
    }

    [Fact]
    public void SelectionLabelTopOffsetGrowsWhenZoomedOut()
    {
        Assert.Equal(18D, FrontedDesignerEditorVisualHelper.GetEffectiveSelectionLabelTopOffset(1D));
        Assert.Equal(36D, FrontedDesignerEditorVisualHelper.GetEffectiveSelectionLabelTopOffset(0.5D));
    }

    [Fact]
    public void FrontedDesignerWindowUpdatesSelectionOnZoomScaleChange()
    {
        var codeBehind = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml.cs"));

        Assert.Contains("if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.ZoomScale))", codeBehind);
        Assert.Contains("UpdateSelectedInteractionVisuals();", codeBehind);
        Assert.Contains("ApplySelectionLabelZoomMetrics", codeBehind);
        Assert.Contains("GetEffectiveSelectionLabelFontSize", codeBehind);
    }

    [Fact]
    public void FrontedDesignerLayerDragGhostUsesPanelOverlayWithoutMutatingDuringDragOver()
    {
        var codeBehind = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml.cs"));
        var xaml = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml"));

        Assert.DoesNotContain("LayerDrag" + "PreviewAdorner", codeBehind);
        Assert.DoesNotContain("AdornerLayer.Get" + "AdornerLayer(this)", codeBehind);
        Assert.DoesNotContain("TryStartLayer" + "DragPreview", codeBehind);
        Assert.DoesNotContain("TryUpdateLayer" + "DragPreview", codeBehind);
        Assert.DoesNotContain("RemoveLayer" + "DragPreview", codeBehind);
        Assert.Contains("ShowLayerDragGhost(_activeLayerDragNode!, e.GetPosition(LayerPanelHostGrid))", codeBehind);
        Assert.Contains("UpdateLayerDragGhost(e.GetPosition(LayerPanelHostGrid))", codeBehind);
        Assert.Contains("UpdateLayerAutoScroll(e.GetPosition(LayerPanelScrollViewer))", codeBehind);
        Assert.Contains("HideLayerDragGhost();", codeBehind);
        Assert.Contains("finally", codeBehind);
        Assert.Contains("x:Name=\"LayerPanelHostGrid\"", xaml);
        Assert.Contains("x:Name=\"LayerPanelScrollViewer\"", xaml);
        Assert.Contains("x:Name=\"LayerDragGhost\"", xaml);
        Assert.Contains("IsHitTestVisible=\"False\"", xaml);

        var dragOverMethod = codeBehind[
            codeBehind.IndexOf("private void UpdateLayerDragOver", StringComparison.Ordinal)..];
        var dragOverMethodEnd = dragOverMethod.IndexOf("private void UpdateLayerAutoScroll", StringComparison.Ordinal);
        Assert.True(dragOverMethodEnd > 0);
        Assert.DoesNotContain("CommitLayerDrop", dragOverMethod[..dragOverMethodEnd]);
    }

    [Fact]
    public void LayerDragGhostAdornerFileIsNotPresent()
    {
        Assert.False(File.Exists(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "LayerDrag" + "PreviewAdorner.cs")));
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
    public void FrontedDesignerWindowXamlContainsRequiredNamedPartsAndHandlers()
    {
        var text = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml"));

        Assert.Contains("<ui:FluentWindow", text);
        Assert.Contains("controls:CustomTitleBar", text);
        Assert.Contains("x:Name=\"PreviewScrollViewer\"", text);
        Assert.Contains("x:Name=\"PreviewZoomHost\"", text);
        Assert.Contains("InteractionLayer", text);
        Assert.Contains("DesignSurfaceGrid", text);
        Assert.Contains("OpenValidationDetails_OnClick", text);
        Assert.Contains("AddControlButton_OnClick", text);
        Assert.Contains("UndoCommand", text);
        Assert.Contains("RedoCommand", text);
        Assert.Contains("LayerItem_OnPreviewMouseRightButtonDown", text);
        Assert.Contains("PropertyFontComboBox_OnSelectionChanged", text);
        Assert.Contains("DropDownClosed=\"PropertyFontComboBox_OnDropDownClosed\"", text);
        Assert.Contains("ImportFontButton_OnClick", text);
        Assert.Contains("ManagePackageFontsButton_OnClick", text);
        Assert.Contains("IsPackageFont", text);
        Assert.Contains("ItemsSource=\"{Binding LayerGroups}\"", text);
        Assert.Contains("x:Name=\"LayerPanelScrollViewer\"", text);
        Assert.Contains("x:Name=\"LayerTopDropZone\"", text);
        Assert.Contains("x:Name=\"LayerBottomDropZone\"", text);
        Assert.Contains("x:Name=\"LayerDragGhost\"", text);
    }

    [Fact]
    public void FrontedDesignerLayerDropZonesExposeRequiredNamedPartsAndHandlers()
    {
        var xaml = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml"));
        var codeBehind = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml.cs"));

        var topSnippet = ReadElementStart(xaml, "LayerTopDropZone", "<Border", 900);
        var scrollSnippet = ReadElementStart(xaml, "LayerPanelScrollViewer", "<ScrollViewer", 500);
        var bottomSnippet = ReadElementStart(xaml, "LayerBottomDropZone", "<Border", 900);
        var ghostSnippet = ReadElementStart(xaml, "LayerDragGhost", "<Border", 500);

        Assert.Contains("AllowDrop=\"True\"", topSnippet);
        Assert.Contains("DragOver=\"LayerTopDropZone_OnDragOver\"", topSnippet);
        Assert.Contains("Drop=\"LayerTopDropZone_OnDrop\"", topSnippet);

        Assert.Contains("AllowDrop=\"True\"", scrollSnippet);

        Assert.Contains("AllowDrop=\"True\"", bottomSnippet);
        Assert.Contains("DragOver=\"LayerBottomDropZone_OnDragOver\"", bottomSnippet);
        Assert.Contains("Drop=\"LayerBottomDropZone_OnDrop\"", bottomSnippet);

        Assert.Contains("IsHitTestVisible=\"False\"", xaml);
        Assert.Contains("DragOver=\"LayerItem_OnDragOver\"", xaml);
        Assert.Contains("SetDropZoneVisibility", codeBehind);
        Assert.Contains("LayerDropZoneStripHeight = 44D", codeBehind);
    }

    [Fact]
    public void FrontedDesignerLayerDropZonesKeepTopBottomCommitBehavior()
    {
        var codeBehind = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml.cs"));

        var topDropMethod = ReadMethodBody(
            codeBehind,
            "private void LayerTopDropZone_OnDrop",
            "private void LayerBottomDropZone_OnDragOver");
        var bottomDropMethod = ReadMethodBody(
            codeBehind,
            "private void LayerBottomDropZone_OnDrop",
            "private void UpdateLayerDragOver");
        var panelDropMethod = ReadMethodBody(
            codeBehind,
            "private void LayerPanel_OnDrop",
            "private void LayerPanel_OnDragLeave");
        var dragOverMethod = ReadMethodBody(
            codeBehind,
            "private void UpdateLayerDragOver",
            "private void UpdateLayerAutoScroll");

        Assert.Contains("CommitLayerDrop(source, null, null, insertAfter: false, moveToNewTopLayer: true)", topDropMethod);
        Assert.Contains("StopLayerDrag(e);", topDropMethod);
        Assert.Contains("CommitLayerDrop(source, null, null, insertAfter: true, moveToNewBottomLayer: true)", bottomDropMethod);
        Assert.Contains("StopLayerDrag(e);", bottomDropMethod);
        Assert.Contains("moveToNewTopLayer: true", panelDropMethod);
        Assert.Contains("moveToNewBottomLayer: true", panelDropMethod);
        Assert.DoesNotContain("CommitLayerDrop", dragOverMethod);
        Assert.DoesNotContain("RebuildFilteredDesignItems", dragOverMethod);
    }

    private static string ReadElementStart(string text, string name, string elementStart, int maxLength)
    {
        var nameIndex = text.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
        Assert.True(nameIndex >= 0);

        var startIndex = text.LastIndexOf(elementStart, nameIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0);

        return text[startIndex..Math.Min(text.Length, startIndex + maxLength)];
    }

    private static string ReadMethodBody(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0);

        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start);

        return text[start..end];
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

            await store.SaveAsync("BpWindow", neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedWindowConfigCanvasAdapter.FromCanvasConfig(config), TestContext.Current.CancellationToken);

            var expectedPath = Path.Combine(root, "BpWindow.json");
            Assert.Equal(expectedPath, store.GetLayoutPath("BpWindow"));
            Assert.Equal(root, store.GetRootFolder());
            Assert.True(store.Exists("BpWindow"));

            var loaded = await store.LoadAsync("BpWindow", TestContext.Current.CancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(3, loaded.Version);
            Assert.True(loaded.ControlLayout.Controls.ContainsKey("Title"));
            Assert.Contains("\"Title\"", File.ReadAllText(expectedPath));

            await store.DeleteAsync("BpWindow", TestContext.Current.CancellationToken);
            Assert.False(store.Exists("BpWindow"));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task FrontedLayoutServiceLoadsOnlyActivePackageLayout()
    {
        var root = CreateTempDirectory();
        try
        {
            var userStore = new FrontedUserLayoutStore(Path.Combine(root, "user"));
            var builtInRoot = Path.Combine(root, "builtIn");
            var packageRoot = Path.Combine(root, "packages");
            WriteBuiltInLayout(builtInRoot, "BpWindow", "BaseCanvas", new FrontedCanvasConfig
            {
                CanvasWidth = 100,
                CanvasHeight = 50,
                Controls =
                {
                    ["BuiltInText"] = new TextFrontedControlConfig { Text = "Built-in" }
                }
            });
            var packageId = "active-package";
            var layoutPath = Path.Combine(packageRoot, packageId, "FrontedLayouts", "BpWindow.json");
            Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
            File.WriteAllText(Path.Combine(packageRoot, packageId, "manifest.json"), "{\"PackageId\":\"active-package\",\"FormatVersion\":3}");
            File.WriteAllText(layoutPath, JsonSerializer.Serialize(neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedWindowConfigCanvasAdapter.FromCanvasConfig(new FrontedCanvasConfig
            {
                CanvasWidth = 200,
                CanvasHeight = 100,
                Controls =
                {
                    ["UserText"] = new TextFrontedControlConfig { Text = "User" }
                }
            })));

            var packageManager = new FrontedLayoutPackageManager(packageRoot, builtInRoot);
            await packageManager.ActivatePackageAsync(packageId, TestContext.Current.CancellationToken);
            var service = new FrontedLayoutService(userStore, packageManager, logger: null);
            var result = await service.LoadWindowConfigWithMetadataAsync(
                "BpWindow",
                TestContext.Current.CancellationToken);

            Assert.Equal(FrontedLayoutSource.User, result.Source);
            Assert.Equal(200, result.Config?.CanvasSettings.CanvasWidth);
            Assert.True(result.Config?.ControlLayout.Controls.ContainsKey("UserText"));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task FrontedLayoutServiceReturnsErrorWhenActivePackageLayoutMissingOrInvalid()
    {
        var root = CreateTempDirectory();
        try
        {
            var userStore = new FrontedUserLayoutStore(Path.Combine(root, "user"));
            var builtInRoot = Path.Combine(root, "builtIn");
            var packageRoot = Path.Combine(root, "packages");
            WriteBuiltInLayout(builtInRoot, "BpWindow", "BaseCanvas", new FrontedCanvasConfig
            {
                CanvasWidth = 100,
                CanvasHeight = 50,
                Controls =
                {
                    ["BuiltInText"] = new TextFrontedControlConfig { Text = "Built-in" }
                }
            });
            var packageId = "broken-package";
            Directory.CreateDirectory(Path.Combine(packageRoot, packageId));
            File.WriteAllText(Path.Combine(packageRoot, packageId, "manifest.json"), "{\"PackageId\":\"broken-package\",\"FormatVersion\":3}");
            var packageManager = new FrontedLayoutPackageManager(packageRoot, builtInRoot);
            await packageManager.ActivatePackageAsync(packageId, TestContext.Current.CancellationToken);
            var service = new FrontedLayoutService(userStore, packageManager, logger: null);

            var missingUserResult = await service.LoadWindowConfigWithMetadataAsync(
                "BpWindow",
                TestContext.Current.CancellationToken);
            Assert.Equal(FrontedLayoutSource.MissingOrError, missingUserResult.Source);
            Assert.Null(missingUserResult.Config);

            var layoutPath = Path.Combine(packageRoot, packageId, "FrontedLayouts", "BpWindow.json");
            Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
            File.WriteAllText(layoutPath, "{ invalid json");
            var invalidUserResult = await service.LoadWindowConfigWithMetadataAsync(
                "BpWindow",
                TestContext.Current.CancellationToken);

            Assert.Equal(FrontedLayoutSource.MissingOrError, invalidUserResult.Source);
            Assert.Null(invalidUserResult.Config);
            Assert.NotNull(invalidUserResult.Error);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task FrontedLayoutService_PackageManagerMode_LoadsActiveBuiltinAndIgnoresLegacyUserLayout()
    {
        var root = CreateTempDirectory();
        try
        {
            var userStore = new FrontedUserLayoutStore(Path.Combine(root, "user"));
            var builtInRoot = Path.Combine(root, "builtIn");
            var packageManager = new FrontedLayoutPackageManager(Path.Combine(root, "packages"), builtInRoot);
            var builtIn = new FrontedWindowConfig();
            builtIn.WindowSettings.BackgroundColor = "#FF00FF00";
            Directory.CreateDirectory(builtInRoot);
            File.WriteAllText(Path.Combine(builtInRoot, "BpWindow.json"), JsonSerializer.Serialize(builtIn));
            var user = new FrontedWindowConfig();
            user.WindowSettings.BackgroundColor = "#FFFF0000";
            await userStore.SaveAsync("BpWindow", user, TestContext.Current.CancellationToken);

            var service = new FrontedLayoutService(userStore, builtInRoot, packageManager, null, null);
            var result = await service.LoadWindowConfigWithMetadataAsync("BpWindow", TestContext.Current.CancellationToken);

            Assert.Equal(FrontedLayoutSource.BuiltIn, result.Source);
            Assert.Equal("#FF00FF00", result.Config?.WindowSettings.BackgroundColor);
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
    public async Task FrontedDesigner_SaveIncludesBehaviorSave_WhenOnlyBehaviorsDirty()
    {
        var behaviorService = new RecordingFrontedBehaviorService();
        var viewModel = new FrontedDesignerWindowViewModel(behaviorService)
        {
            CurrentDocument = new FrontedCanvasDesignDocument
            {
                WindowTypeName = "BpWindow",
                CanvasName = "BaseCanvas",
                CanvasConfig = new FrontedCanvasConfig
                {
                    Version = 3,
                    CanvasWidth = 100,
                    CanvasHeight = 50
                },
                IsDirty = false
            }
        };
        viewModel.BehaviorPanel.SetDocument(new FrontedBehaviorDocument
        {
            WindowType = "BpWindow",
            CanvasName = "BaseCanvas"
        });
        viewModel.AreBehaviorsDirty = true;

        var saved = await viewModel.SaveCurrentLayoutAsync();

        Assert.True(saved);
        Assert.Equal(1, behaviorService.SaveCount);
        Assert.False(viewModel.AreBehaviorsDirty);
    }

    [Fact]
    public void DeleteControl_RemovesBehaviorSetFromCurrentDocument()
    {
        var behaviorGuid = Guid.NewGuid();
        var document = new FrontedCanvasDesignDocument
        {
            WindowTypeName = "BpWindow",
            CanvasName = "BaseCanvas",
            CanvasConfig = new FrontedCanvasConfig
            {
                Version = 3,
                CanvasWidth = 100,
                CanvasHeight = 50
            },
            Controls =
            {
                new FrontedControlDesignItem
                {
                    Name = "Title",
                    Config = new TextFrontedControlConfig { BehaviorGuid = behaviorGuid }
                }
            }
        };
        var behaviorDocument = new FrontedBehaviorDocument
        {
            WindowType = "BpWindow",
            CanvasName = "BaseCanvas"
        };
        behaviorDocument.GetOrCreateSet(behaviorGuid, "Title").Behaviors.Add(new FrontedBehavior { Name = "Fade" });
        var viewModel = new FrontedDesignerWindowViewModel(new RecordingFrontedBehaviorService())
        {
            CurrentDocument = document
        };
        viewModel.BehaviorPanel.SetDocument(behaviorDocument);
        viewModel.SelectedDesignItem = document.Controls[0];

        viewModel.DeleteSelectedControlCommand.Execute(null);

        Assert.Null(viewModel.BehaviorPanel.CurrentDocument.FindSet(behaviorGuid));
        Assert.True(viewModel.AreBehaviorsDirty);
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
    public void SmartSnapMoveAlignsControlEdgesAndProducesGuides()
    {
        var active = CreateSnapItem("Active", 10, 20, 100, 50);
        var target = CreateSnapItem("Target", 205, 100, 100, 50);
        var document = CreateDocument([active, target]);

        var result = FrontedDesignerSmartSnapHelper.Move(
            active,
            document,
            10,
            20,
            100,
            50,
            95,
            4,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 6);

        Assert.Equal(105, result.Left);
        Assert.Equal(20, result.Top);
        var guide = Assert.Single(result.Guides);
        Assert.Equal(FrontedDesignerSnapGuideOrientation.Vertical, guide.Orientation);
        Assert.Equal(205, guide.Position);
        Assert.Equal(FrontedDesignerSnapGuideSource.Control, guide.Source);
    }

    [Fact]
    public void SmartSnapMoveAlignsControlCenters()
    {
        var active = CreateSnapItem("Active", 10, 20, 100, 50);
        var target = CreateSnapItem("Target", 300, 100, 120, 60);
        var document = CreateDocument([active, target]);

        var result = FrontedDesignerSmartSnapHelper.Move(
            active,
            document,
            10,
            20,
            100,
            50,
            300,
            85,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 6);

        Assert.Equal(310, result.Left);
        Assert.Equal(105, result.Top);
        Assert.Contains(result.Guides, guide => guide.Orientation == FrontedDesignerSnapGuideOrientation.Vertical
                                               && guide.Position == 360);
        Assert.Contains(result.Guides, guide => guide.Orientation == FrontedDesignerSnapGuideOrientation.Horizontal
                                               && guide.Position == 130);
    }

    [Fact]
    public void SmartSnapUsesCanvasCandidatesAndNearestCandidateWins()
    {
        var active = CreateSnapItem("Active", 10, 20, 100, 50);
        var near = CreateSnapItem("Near", 716, 100, 50, 50);
        var document = CreateDocument([active, near]);

        var result = FrontedDesignerSmartSnapHelper.Move(
            active,
            document,
            10,
            20,
            100,
            50,
            606,
            -17,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 6);

        Assert.Equal(616, result.Left);
        Assert.Equal(0, result.Top);
        Assert.Contains(result.Guides, guide => guide.Orientation == FrontedDesignerSnapGuideOrientation.Vertical
                                               && guide.Position == 716
                                               && guide.Source == FrontedDesignerSnapGuideSource.Control);
        Assert.Contains(result.Guides, guide => guide.Orientation == FrontedDesignerSnapGuideOrientation.Horizontal
                                               && guide.Position == 0
                                               && guide.Source == FrontedDesignerSnapGuideSource.Canvas);
    }

    [Fact]
    public void SmartSnapFallsBackToGridOrHalfStepWhenNoSmartCandidateExists()
    {
        var active = CreateSnapItem("Active", 10, 20, 100, 50);
        var target = CreateSnapItem("Target", 300, 100, 100, 50);
        var document = CreateDocument([active, target]);

        var snapResult = FrontedDesignerSmartSnapHelper.Move(
            active,
            document,
            10,
            20,
            100,
            50,
            13,
            14,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 2);

        Assert.Equal(20, snapResult.Left);
        Assert.Equal(30, snapResult.Top);
        Assert.Empty(snapResult.Guides);

        var freeResult = FrontedDesignerSmartSnapHelper.Move(
            active,
            document,
            10,
            20,
            100,
            50,
            0.24,
            0.25,
            effectiveSnapEnabled: false,
            snapGridSize: 10,
            logicalTolerance: 6);

        Assert.Equal(10, freeResult.Left);
        Assert.Equal(20.5, freeResult.Top);
        Assert.Empty(freeResult.Guides);
    }

    [Fact]
    public void SmartSnapResizeSnapsAffectedEdgesAndKeepsMinimumSize()
    {
        var active = CreateSnapItem("Active", 100, 100, 100, 50);
        var target = CreateSnapItem("Target", 250, 25, 100, 50);
        var document = CreateDocument([active, target]);

        var right = FrontedDesignerSmartSnapHelper.Resize(
            active,
            document,
            FrontedDesignerResizeHandleKind.Right,
            100,
            100,
            100,
            50,
            148,
            0,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 6);

        Assert.Equal(350, right.Left + right.Width);
        Assert.Contains(right.Guides, guide => guide.Orientation == FrontedDesignerSnapGuideOrientation.Vertical
                                              && guide.Position == 350);

        var left = FrontedDesignerSmartSnapHelper.Resize(
            active,
            document,
            FrontedDesignerResizeHandleKind.Left,
            100,
            100,
            100,
            50,
            296,
            0,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 6);

        Assert.Equal(199, left.Left);
        Assert.Equal(1, left.Width);
        Assert.Equal(200, left.Left + left.Width);
    }

    [Fact]
    public void SmartSnapResizeFallbackKeepsOppositeEdgeFixed()
    {
        var active = CreateSnapItem("Active", 103, 107, 50, 40);
        var document = CreateDocument([active]);

        var right = FrontedDesignerSmartSnapHelper.Resize(
            active,
            document,
            FrontedDesignerResizeHandleKind.Right,
            103,
            107,
            50,
            40,
            6,
            0,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 2);

        Assert.Equal(103, right.Left);
        Assert.Equal(57, right.Width);
        Assert.Equal(160, right.Left + right.Width);
        Assert.Equal(107, right.Top);
        Assert.Equal(40, right.Height);

        var left = FrontedDesignerSmartSnapHelper.Resize(
            active,
            document,
            FrontedDesignerResizeHandleKind.Left,
            103,
            107,
            50,
            40,
            -6,
            0,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 2);

        Assert.Equal(100, left.Left);
        Assert.Equal(53, left.Width);
        Assert.Equal(153, left.Left + left.Width);

        var bottom = FrontedDesignerSmartSnapHelper.Resize(
            active,
            document,
            FrontedDesignerResizeHandleKind.Bottom,
            103,
            107,
            50,
            40,
            0,
            6,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 2);

        Assert.Equal(107, bottom.Top);
        Assert.Equal(43, bottom.Height);
        Assert.Equal(150, bottom.Top + bottom.Height);
        Assert.Equal(103, bottom.Left);
        Assert.Equal(50, bottom.Width);

        var top = FrontedDesignerSmartSnapHelper.Resize(
            active,
            document,
            FrontedDesignerResizeHandleKind.Top,
            103,
            107,
            50,
            40,
            0,
            -6,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 2);

        Assert.Equal(100, top.Top);
        Assert.Equal(47, top.Height);
        Assert.Equal(147, top.Top + top.Height);
    }

    [Fact]
    public void SmartSnapSkipsLinkedOverlaysButUsesPluginPlaceholders()
    {
        var active = CreateSnapItem("Active", 10, 20, 100, 50);
        var overlay = CreateSnapItem("Overlay", 205, 80, 100, 50);
        overlay.IsLinkedOverlay = true;
        var plugin = new FrontedControlDesignItem
        {
            Name = "MissingTeamCard",
            Config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:top.plfjy.missing/TeamCard",
                Left = 220,
                Top = 100,
                Width = 100,
                Height = 60
            },
            IsSelectableInEditor = true,
            IsEditableInEditor = true
        };
        var document = CreateDocument([active, overlay, plugin]);

        var result = FrontedDesignerSmartSnapHelper.Move(
            active,
            document,
            10,
            20,
            100,
            50,
            110,
            0,
            effectiveSnapEnabled: true,
            snapGridSize: 10,
            logicalTolerance: 6);

        Assert.Equal(120, result.Left);
        Assert.Contains(result.Guides, guide => guide.Label == "MissingTeamCard");
        Assert.DoesNotContain(result.Guides, guide => guide.Label == "Overlay");
    }

    [Fact]
    public void ViewModelMoveAndResizeUpdateAndClearActiveSnapGuides()
    {
        var active = CreateSnapItem("Active", 10, 20, 100, 50);
        var target = CreateSnapItem("Target", 205, 100, 100, 50);
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([active, target]),
            SelectedDesignItem = active,
            SnapEnabled = true
        };

        viewModel.MoveSelectedDesignItem(10, 20, 103, 0, renderPreview: false);

        Assert.NotEmpty(viewModel.ActiveSnapGuides);
        Assert.Equal(105, active.Config.Left);

        viewModel.ClearActiveSnapGuides();

        Assert.Empty(viewModel.ActiveSnapGuides);

        viewModel.ResizeSelectedDesignItem(
            FrontedDesignerResizeHandleKind.Right,
            105,
            20,
            100,
            50,
            50,
            0,
            renderPreview: false);

        Assert.NotEmpty(viewModel.ActiveSnapGuides);

        viewModel.SnapEnabled = false;

        Assert.Empty(viewModel.ActiveSnapGuides);
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

    [Fact]
    public void Bo3StateBackgroundEditIsGenericAndUndoable()
    {
        var document = CreateDocument([], "ScoreGlobalWindow");
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        viewModel.EnableBoModeStates = true;
        viewModel.SelectedBoModeStateOption = viewModel.BoModeStateOptions.First(option => option.State == FrontedCanvasBoModeState.Bo3);
        Assert.True(viewModel.IsBoModeStateSelectorVisible);
        Assert.True(viewModel.ApplyCanvasBackgroundEdit("Resources/scoreGlobalBo3.png"));
        Assert.Equal(
            "Resources/scoreGlobalBo3.png",
            viewModel.CurrentDocument!.CanvasConfig.BoModeStates["Bo3"].BackgroundImage);
        Assert.True(viewModel.CurrentDocument.IsDirty);

        viewModel.UndoCommand.Execute(null);
        Assert.Null(viewModel.CurrentDocument!.CanvasConfig.BoModeStates["Bo3"].BackgroundImage);
    }

    [Fact]
    public void LayerGroupsFollowZIndexDescendingAndDocumentOrderWithinLayer()
    {
        var first = new FrontedControlDesignItem { Name = "First", Config = new TextFrontedControlConfig { ZIndex = 2 } };
        var second = new FrontedControlDesignItem { Name = "Second", Config = new ImageFrontedControlConfig { ZIndex = 1 } };
        var third = new FrontedControlDesignItem { Name = "Third", Config = new TextFrontedControlConfig { ZIndex = 2 } };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([first, second, third])
        };

        Assert.Equal([2, 1], viewModel.LayerGroups.Select(group => group.ZIndex));
        Assert.Equal(["First", "Third"], viewModel.LayerGroups[0].Items.Select(item => item.ControlItem?.Name));
        Assert.Equal(["Second"], viewModel.LayerGroups[1].Items.Select(item => item.ControlItem?.Name));
    }

    [Fact]
    public void LayerGroupsRespectControlFilter()
    {
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument(
            [
                new FrontedControlDesignItem { Name = "Title", Config = new TextFrontedControlConfig { ZIndex = 2 } },
                new FrontedControlDesignItem { Name = "Logo", Config = new ImageFrontedControlConfig { ZIndex = 1 } }
            ])
        };

        viewModel.ControlFilterText = "Logo";

        var group = Assert.Single(viewModel.LayerGroups);
        Assert.Equal(1, group.ZIndex);
        Assert.Equal("Logo", Assert.Single(group.Items).ControlItem?.Name);
        Assert.False(viewModel.CanReorderLayers);
    }

    [Fact]
    public void LayerNodeBuilderCreatesTopLevelControlNodes()
    {
        var text = new FrontedControlDesignItem { Name = "Title", Config = new TextFrontedControlConfig { ZIndex = 2 } };
        var image = new FrontedControlDesignItem { Name = "Logo", Config = new ImageFrontedControlConfig { ZIndex = 1 } };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([text, image])
        };

        var nodes = viewModel.LayerGroups.SelectMany(group => group.Items).ToArray();

        Assert.All(nodes, node => Assert.Equal(DesignerLayerNodeKind.Control, node.Kind));
        Assert.Equal(["Title", "Logo"], nodes.Select(node => node.DisplayName));
        Assert.All(nodes, node => Assert.True(node.CanReorder));
    }

    [Fact]
    public void GlobalScoreRowLayerPanelShowsOnlyTopLevelControl()
    {
        var (viewModel, rowItem, _) = CreateGlobalScoreRowDesigner();

        var group = Assert.Single(viewModel.LayerGroups);
        var rowNode = Assert.Single(group.Items);

        Assert.Equal(DesignerLayerNodeKind.Control, rowNode.Kind);
        Assert.Same(rowItem, rowNode.ControlItem);
        Assert.True(rowNode.CanReorder);
        Assert.DoesNotContain(group.Items, node => node.DisplayName.Contains("Game1FirstHalf", StringComparison.Ordinal));
    }

    [Fact]
    public void SelectingGlobalScoreRowExposesDedicatedCellEditorItems()
    {
        var (viewModel, rowItem, cell) = CreateGlobalScoreRowDesigner();

        viewModel.SelectDesignItem(rowItem);

        Assert.True(viewModel.HasGlobalScoreCellEditor);
        Assert.Contains(cell, viewModel.GlobalScoreCellEditorItems);
        Assert.All(viewModel.LayerGroups.SelectMany(group => group.Items), node =>
            Assert.Equal(DesignerLayerNodeKind.Control, node.Kind));
    }

    [Fact]
    public void SelectingParentLayerNodeClearsSelectedGlobalScoreCell()
    {
        var (viewModel, rowItem, cell) = CreateGlobalScoreRowDesigner();
        var rowNode = viewModel.LayerGroups.Single().Items[0];
        viewModel.SelectGlobalScoreCell(rowItem, cell);

        viewModel.SelectLayerNode(rowNode);

        Assert.Same(rowItem, viewModel.SelectedDesignItem);
        Assert.Null(viewModel.SelectedGlobalScoreCell);
        Assert.True(rowNode.IsSelected);
    }

    [Fact]
    public void SelectingDedicatedGlobalScoreCellEditorItemSelectsParentAndCell()
    {
        var (viewModel, rowItem, cell) = CreateGlobalScoreRowDesigner();

        viewModel.SelectDesignItem(rowItem);
        viewModel.SelectedGlobalScoreCell = cell;

        Assert.Same(rowItem, viewModel.SelectedDesignItem);
        Assert.Same(cell, viewModel.SelectedGlobalScoreCell);
        Assert.Equal(rowItem.Name, viewModel.SelectedGlobalScoreCellParentName);
        Assert.Equal(cell.Id, viewModel.SelectedGlobalScoreCellId);
    }

    [Fact]
    public void CanvasAndDedicatedEditorGlobalScoreCellSelectionProduceSameViewModelState()
    {
        var (viewModel, rowItem, cell) = CreateGlobalScoreRowDesigner();

        viewModel.SelectGlobalScoreCell(rowItem, cell);
        var canvasSelection = (viewModel.SelectedDesignItem, viewModel.SelectedGlobalScoreCell);

        viewModel.ClearSelection();
        viewModel.SelectDesignItem(rowItem);
        viewModel.SelectedGlobalScoreCell = cell;

        Assert.Same(canvasSelection.SelectedDesignItem, viewModel.SelectedDesignItem);
        Assert.Same(canvasSelection.SelectedGlobalScoreCell, viewModel.SelectedGlobalScoreCell);
    }

    [Fact]
    public void GlobalScoreCellDoesNotEnterTopLevelReorderList()
    {
        var (viewModel, rowItem, _) = CreateGlobalScoreRowDesigner();
        var node = Assert.Single(viewModel.LayerGroups.Single().Items);

        Assert.Equal(DesignerLayerNodeKind.Control, node.Kind);
        Assert.Same(rowItem, node.ControlItem);
    }

    [Fact]
    public void LayerDropInsideSameGroupReordersDocumentWithoutChangingZIndex()
    {
        var first = new FrontedControlDesignItem { Name = "First", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var second = new FrontedControlDesignItem { Name = "Second", Config = new ImageFrontedControlConfig { ZIndex = 1 } };
        var third = new FrontedControlDesignItem { Name = "Third", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var document = CreateDocument([first, second, third]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        Assert.True(viewModel.CommitLayerDrop(third, 1, first, insertAfter: false));

        Assert.Equal(["Third", "First", "Second"], document.Controls.Select(item => item.Name));
        Assert.All(document.Controls, item => Assert.Equal(1, item.Config.ZIndex));
    }

    [Fact]
    public void LayerDropIntoDifferentGroupChangesZIndexAndOrder()
    {
        var top = new FrontedControlDesignItem { Name = "Top", Config = new TextFrontedControlConfig { ZIndex = 5 } };
        var middle = new FrontedControlDesignItem { Name = "Middle", Config = new ImageFrontedControlConfig { ZIndex = 3 } };
        var bottom = new FrontedControlDesignItem { Name = "Bottom", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var document = CreateDocument([top, middle, bottom]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        Assert.True(viewModel.CommitLayerDrop(bottom, 5, top, insertAfter: true));

        Assert.Equal(5, bottom.Config.ZIndex);
        Assert.Equal(["Top", "Bottom", "Middle"], document.Controls.Select(item => item.Name));
    }

    [Fact]
    public void LayerDropToTopAndBottomZonesCreatesNewZIndex()
    {
        var top = new FrontedControlDesignItem { Name = "Top", Config = new TextFrontedControlConfig { ZIndex = 5 } };
        var bottom = new FrontedControlDesignItem { Name = "Bottom", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var document = CreateDocument([top, bottom]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        Assert.True(viewModel.CommitLayerDrop(bottom, null, null, insertAfter: false, moveToNewTopLayer: true));
        Assert.Equal(6, bottom.Config.ZIndex);

        Assert.True(viewModel.CommitLayerDrop(top, null, null, insertAfter: true, moveToNewBottomLayer: true));
        Assert.Equal(4, top.Config.ZIndex);
    }

    [Fact]
    public void LayerDropIsUndoableAndRedoable()
    {
        var first = new FrontedControlDesignItem { Name = "First", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var second = new FrontedControlDesignItem { Name = "Second", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([first, second])
        };

        Assert.True(viewModel.CommitLayerDrop(second, 1, first, insertAfter: false));
        Assert.Equal(["Second", "First"], viewModel.CurrentDocument!.Controls.Select(item => item.Name));

        viewModel.UndoCommand.Execute(null);
        viewModel.ExecuteScheduledDesignerWorkForTests();
        Assert.Equal(["First", "Second"], viewModel.CurrentDocument!.Controls.Select(item => item.Name));

        viewModel.RedoCommand.Execute(null);
        viewModel.ExecuteScheduledDesignerWorkForTests();
        Assert.Equal(["Second", "First"], viewModel.CurrentDocument!.Controls.Select(item => item.Name));
    }

    [Fact]
    public void LayerDropIsDisabledWhenFilterIsActive()
    {
        var first = new FrontedControlDesignItem { Name = "First", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var second = new FrontedControlDesignItem { Name = "Second", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var document = CreateDocument([first, second]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        viewModel.ControlFilterText = "First";

        Assert.False(viewModel.CommitLayerDrop(second, 1, first, insertAfter: false));
        Assert.Equal(["First", "Second"], document.Controls.Select(item => item.Name));
    }

    [Fact]
    public void MissingPluginPlaceholderCanBeReorderedWhenEditable()
    {
        var text = new FrontedControlDesignItem { Name = "Text", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var plugin = new FrontedControlDesignItem
        {
            Name = "MissingTeamCard",
            Config = new PluginFrontedControlConfig
            {
                ControlType = "plugin:top.plfjy.missing/TeamCard",
                ZIndex = 1
            },
            IsSelectableInEditor = true,
            IsEditableInEditor = true
        };
        var document = CreateDocument([text, plugin]);
        var viewModel = new FrontedDesignerWindowViewModel { CurrentDocument = document };

        Assert.True(viewModel.CommitLayerDrop(plugin, 1, text, insertAfter: false));

        Assert.Equal(["MissingTeamCard", "Text"], document.Controls.Select(item => item.Name));
    }

    [Fact]
    public void LayerDropSchedulesOneValidationAndPreviewRender()
    {
        var first = new FrontedControlDesignItem { Name = "First", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var second = new FrontedControlDesignItem { Name = "Second", Config = new TextFrontedControlConfig { ZIndex = 1 } };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([first, second])
        };

        Assert.True(viewModel.CommitLayerDrop(second, 1, first, insertAfter: false));
        Assert.True(viewModel.HasPendingScheduledDesignerWork);
        Assert.Equal(0, viewModel.ScheduledDesignerValidationExecutionCount);
        Assert.Equal(0, viewModel.ScheduledDesignerPreviewExecutionCount);

        viewModel.ExecuteScheduledDesignerWorkForTests();

        Assert.Equal(1, viewModel.ScheduledDesignerValidationExecutionCount);
        Assert.Equal(1, viewModel.ScheduledDesignerPreviewExecutionCount);
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

    private static (FrontedDesignerWindowViewModel ViewModel, FrontedControlDesignItem RowItem, GlobalScoreCellConfig Cell)
        CreateGlobalScoreRowDesigner()
    {
        var rowConfig = new GlobalScoreRowControlConfig
        {
            Left = 10,
            Top = 20,
            Width = 220,
            Height = 40,
            TeamType = TeamType.HomeTeam,
            Cells =
            [
                new GlobalScoreCellConfig
                {
                    Id = "Game1FirstHalf",
                    GameNumber = 1,
                    GameKind = ScoreGameKind.Normal,
                    HalfKind = ScoreHalfKind.FirstHalf,
                    X = 12,
                    Y = 4,
                    Width = 75,
                    Height = 32
                },
                new GlobalScoreCellConfig
                {
                    Id = "Game1SecondHalf",
                    GameNumber = 1,
                    GameKind = ScoreGameKind.Normal,
                    HalfKind = ScoreHalfKind.SecondHalf,
                    X = 102,
                    Y = 4,
                    Width = 75,
                    Height = 32
                }
            ]
        };
        var rowItem = new FrontedControlDesignItem
        {
            Name = "HomeGlobalScoreRow",
            Config = rowConfig
        };
        var viewModel = new FrontedDesignerWindowViewModel
        {
            CurrentDocument = CreateDocument([rowItem])
        };

        return (viewModel, rowItem, rowConfig.Cells[0]);
    }

    private static FrontedControlDesignItem CreateSnapItem(
        string name,
        double left,
        double top,
        double width,
        double height)
    {
        return new FrontedControlDesignItem
        {
            Name = name,
            Config = new ImageFrontedControlConfig
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height
            },
            IsSelectableInEditor = true,
            IsEditableInEditor = true
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
            new FrontedLayoutReferenceScanner());
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
        typeof(MapV2DisplayControlConfig),
        typeof(RectangleFrontedControlConfig),
        typeof(PolygonFrontedControlConfig),
        typeof(BackgroundTintRectangleFrontedControlConfig),
        typeof(BackgroundTintPolygonFrontedControlConfig)
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
        private readonly IReadOnlyDictionary<string, string> _designerTexts;

        public TestDesignerLocalizationService(
            IReadOnlyDictionary<string, string>? propertyNames = null,
            IReadOnlyDictionary<string, string>? groupNames = null,
            IReadOnlyDictionary<string, string>? options = null,
            IReadOnlyDictionary<string, string>? controlTypes = null,
            IReadOnlyDictionary<string, string>? windows = null,
            IReadOnlyDictionary<string, string>? canvases = null,
            IReadOnlyDictionary<string, string>? bindings = null,
            IReadOnlyDictionary<string, string>? designerTexts = null)
        {
            _propertyNames = propertyNames ?? new Dictionary<string, string>();
            _groupNames = groupNames ?? new Dictionary<string, string>();
            _options = options ?? new Dictionary<string, string>();
            _controlTypes = controlTypes ?? new Dictionary<string, string>();
            _windows = windows ?? new Dictionary<string, string>();
            _canvases = canvases ?? new Dictionary<string, string>();
            _bindings = bindings ?? new Dictionary<string, string>();
            _designerTexts = designerTexts ?? new Dictionary<string, string>();
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

        public override string GetDesignerText(string key, string fallback) =>
            _designerTexts.GetValueOrDefault(key, fallback);
    }

    private sealed class AllowAllImageSafetyService : IFrontedImageSafetyService
    {
        public FrontedImageValidationResult ValidateFile(
            string path,
            FrontedImagePurpose purpose,
            bool knownBackgroundImage = false,
            bool knownUiImage = false) =>
            new()
            {
                IsValid = true,
                FileBytes = File.Exists(path) ? new FileInfo(path).Length : 0,
                PixelWidth = 1,
                PixelHeight = 1
            };
    }

    private sealed class RejectingImageSafetyService : IFrontedImageSafetyService
    {
        public FrontedImageValidationResult ValidateFile(
            string path,
            FrontedImagePurpose purpose,
            bool knownBackgroundImage = false,
            bool knownUiImage = false) =>
            new()
            {
                IsValid = false,
                ErrorCode = "ImageTooLarge",
                ErrorMessage = "Image file is too large."
            };
    }

    private sealed class FixedPathFrontedResourceResolver(string? resolvedPath = null) : IFrontedResourceResolver
    {
        public string? ResolveImagePath(string? path) => resolvedPath;

        public ImageSource? ResolveImage(
            string? path,
            FrontedImagePurpose purpose = FrontedImagePurpose.PackageResource) => null;
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

    private static FrontedPropertyEditorItem TextEditorRow(string propertyName)
    {
        return new FrontedPropertyEditorItem
        {
            PropertyName = propertyName,
            EditorKind = FrontedPropertyEditorKind.Text
        };
    }

    private static FrontedLayoutValidator CreateValidator()
    {
        return new FrontedLayoutValidator(
            new KnownFrontedControlRegistry(),
            referenceScanner: new FrontedLayoutReferenceScanner());
    }

    private static FrontedCanvasConfig ReadBuiltInLayout(string windowTypeName, string canvasName = "BaseCanvas")
    {
        var path = Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts", $"{windowTypeName}.json");

        Assert.True(File.Exists(path), path);
        var config = JsonSerializer.Deserialize<FrontedWindowConfig>(File.ReadAllText(path))?.ToCanvasConfig();
        Assert.NotNull(config);
        return config;
    }

    private static void WriteBuiltInLayout(
        string builtInRoot,
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config)
    {
        _ = canvasName;
        Directory.CreateDirectory(builtInRoot);
        File.WriteAllText(
            Path.Combine(builtInRoot, $"{windowTypeName}.json"),
            JsonSerializer.Serialize(neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedWindowConfigCanvasAdapter.FromCanvasConfig(config)));
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

    private static void WriteTinyPng(string path)
    {
        File.WriteAllBytes(
            path,
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="));
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

    private static void AssertWarningOnStaticRow(
        FrontedCanvasDesignDocument document,
        FrontedControlDesignItem item,
        string propertyName)
    {
        var row = Assert.Single(BuildPropertyRows(document, item), candidate => candidate.PropertyName == propertyName);
        Assert.Contains(
            row.ValidationMessages,
            message => message.Severity == FrontedLayoutValidationSeverity.Warning);
        Assert.DoesNotContain(
            row.ValidationMessages,
            message => message.Severity == FrontedLayoutValidationSeverity.Error);
    }

    private sealed class TestBindingRootProvider : IFrontedBindingRootProvider
    {
        public IReadOnlyList<FrontedBindingRootDescriptor> GetRoots() =>
        [
            new("Root", typeof(TestBindingRoot))
        ];
    }

    [FrontedBindingObject]
    private sealed class TestBindingRoot : ObservableRecipient
    {
        public string Name { get; } = string.Empty;

        [FrontedBindingIgnore]
        public string Hidden { get; } = string.Empty;

        [FrontedBindingCollection(FixedCount = 2)]
        public IReadOnlyList<TestBindingChild> Children { get; } = [];

        [FrontedBindingIgnore]
        public string GetterThatThrows => throw new InvalidOperationException("Catalog scan invoked a getter.");
    }

    [FrontedBindingObject]
    private sealed class TestBindingChild
    {
        public string Name { get; } = string.Empty;
    }

    private class KnownFrontedControlRegistry : IFrontedControlRegistry
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
            new KnownFrontedControl("MapV2Display", typeof(MapV2DisplayControlConfig)),
            new KnownFrontedControl("Rectangle", typeof(RectangleFrontedControlConfig)),
            new KnownFrontedControl("Polygon", typeof(PolygonFrontedControlConfig)),
            new KnownFrontedControl("BackgroundTintRectangle", typeof(BackgroundTintRectangleFrontedControlConfig)),
            new KnownFrontedControl("BackgroundTintPolygon", typeof(BackgroundTintPolygonFrontedControlConfig))
        ];

        public virtual IFrontedControl? GetControl(string controlType)
        {
            return Controls.FirstOrDefault(control => control.ControlType == controlType);
        }

        public virtual IReadOnlyCollection<IFrontedControl> GetControls()
        {
            return Controls;
        }

        public virtual IFrontedPluginControlDescriptor? GetPluginDescriptor(string fullControlType)
        {
            return null;
        }

        public virtual IReadOnlyCollection<IFrontedPluginControlDescriptor> GetPluginDescriptors()
        {
            return [];
        }
    }

    private sealed class PluginFrontedControlRegistryForTests : KnownFrontedControlRegistry
    {
        public static readonly Guid PluginDefaultBehaviorGuid = Guid.NewGuid();

        private readonly FrontedPluginControlDescriptor<TestPluginDesignerConfig> _descriptor = new()
        {
            PackageId = "top.plfjy.example.fronted",
            ControlTypeName = "TeamCard",
            ConfigType = typeof(TestPluginDesignerConfig),
            DisplayNameKey = "Designer.ControlType.TeamCard",
            DescriptionKey = "Designer.ControlType.TeamCard.Description",
            CreateDefaultConfig = () => new TestPluginDesignerConfig
            {
                Title = "Default",
                BehaviorGuid = PluginDefaultBehaviorGuid
            },
            Properties =
            [
                new FrontedPluginPropertyDescriptor
                {
                    PropertyName = nameof(TestPluginDesignerConfig.Title),
                    GroupName = "Plugin"
                },
                new FrontedPluginPropertyDescriptor
                {
                    PropertyName = nameof(TestPluginDesignerConfig.TitleBindingPath),
                    GroupName = "Plugin",
                    BindingTargetKind = FrontedBindingTargetKind.Text
                },
                new FrontedPluginPropertyDescriptor
                {
                    PropertyName = nameof(TestPluginDesignerConfig.AccentColor),
                    GroupName = "Plugin",
                    EditorKind = FrontedPropertyEditorKind.Color
                },
                new FrontedPluginPropertyDescriptor
                {
                    PropertyName = nameof(TestPluginDesignerConfig.Mode),
                    GroupName = "Plugin",
                    EditorKind = FrontedPropertyEditorKind.Enum,
                    Options =
                    [
                        new FrontedPropertyEditorOption { Value = "Compact", DisplayName = "Compact" },
                        new FrontedPropertyEditorOption { Value = "Expanded", DisplayName = "Expanded" }
                    ]
                }
            ],
            Validate = config => string.IsNullOrWhiteSpace(config.Title)
                ? [new FrontedLayoutValidationMessage
                {
                    Severity = FrontedLayoutValidationSeverity.Warning,
                    Code = "PluginTitleEmpty",
                    PropertyName = nameof(TestPluginDesignerConfig.Title),
                    Message = "Title is empty."
                }]
                : [],
            CreateControl = (name, _, _) => new Border { Name = name }
        };

        public override IFrontedControl? GetControl(string controlType)
        {
            return controlType == _descriptor.FullControlType
                ? new KnownFrontedControl(_descriptor.FullControlType, typeof(TestPluginDesignerConfig))
                : base.GetControl(controlType);
        }

        public override IReadOnlyCollection<IFrontedControl> GetControls()
        {
            return [.. base.GetControls(), new KnownFrontedControl(_descriptor.FullControlType, typeof(TestPluginDesignerConfig))];
        }

        public override IFrontedPluginControlDescriptor? GetPluginDescriptor(string fullControlType)
        {
            return string.Equals(fullControlType, _descriptor.FullControlType, StringComparison.OrdinalIgnoreCase)
                ? _descriptor
                : null;
        }

        public override IReadOnlyCollection<IFrontedPluginControlDescriptor> GetPluginDescriptors()
        {
            return [_descriptor];
        }
    }

    private sealed class RecordingFrontedBehaviorService : IFrontedBehaviorService
    {
        public List<Guid> RemovedBehaviorGuids { get; } = [];

        public int SaveCount { get; private set; }

        public Task<FrontedBehaviorDocument> LoadDocumentAsync(
            string windowType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FrontedBehaviorDocument
            {
                Version = 1,
                WindowType = windowType,
                CanvasName = FrontedLayoutConstants.BaseCanvasName
            });
        }

        public Task SaveDocumentAsync(
            FrontedBehaviorDocument document,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public void RemoveBehaviors(Guid behaviorGuid)
        {
            RemovedBehaviorGuids.Add(behaviorGuid);
        }
    }

    private sealed class TestPluginDesignerConfig : FrontedControlConfigBase
    {
        public TestPluginDesignerConfig()
        {
            ControlType = "plugin:top.plfjy.example.fronted/TeamCard";
            Width = 220;
            Height = 80;
        }

        public string? Title { get; set; }

        public string? TitleBindingPath { get; set; }

        public string AccentColor { get; set; } = "#FFFFFFFF";

        public string Mode { get; set; } = "Compact";
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
