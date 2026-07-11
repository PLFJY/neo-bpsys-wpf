using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class LegacyFrontedLayoutConversionPolishTest : IDisposable
{
    private readonly Func<string, string>? _previousLocalizeTemplate;

    public LegacyFrontedLayoutConversionPolishTest()
    {
        _previousLocalizeTemplate = LegacyConvertMessageHelper.LocalizeTemplate;
        LegacyConvertMessageHelper.LocalizeTemplate = null;
    }

    public void Dispose()
    {
        LegacyConvertMessageHelper.LocalizeTemplate = _previousLocalizeTemplate;
    }

    private const string LegacyFont = "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简";
    private const string NotoSansFont = "pack://application:,,,/Assets/Fonts/#Noto Sans";

    [Fact]
    public void FormatterKeepsBenignDiagnosticsOutOfUserWarnings()
    {
        var result = new FrontedLayoutPackageLegacyConvertResult
        {
            Success = true,
            Messages =
            [
                LegacyConvertMessageHelper.Info(LegacyConvertMessageHelper.CodeResourceCopied,
                    LegacyConvertMessageHelper.Args(new { FileName = "CurrentBanLock.png" })),
                LegacyConvertMessageHelper.Info(LegacyConvertMessageHelper.CodeGlobalScoreCellsAggregated,
                    LegacyConvertMessageHelper.Args(new { Team = "Home", TargetName = "HomeGlobalScoreRow" })),
                LegacyConvertMessageHelper.Info(LegacyConvertMessageHelper.CodeControlGeometryFuzzyMatched),
                LegacyConvertMessageHelper.Info(LegacyConvertMessageHelper.CodeOvertimeScoreCellsAggregated),
                LegacyConvertMessageHelper.Info(LegacyConvertMessageHelper.CodeLockOverlayGeometryConsumed,
                    LegacyConvertMessageHelper.Args(new { LegacyName = "HunBanCurrentLock0", TargetName = "HunBanCurrent0" })),
                LegacyConvertMessageHelper.Info(LegacyConvertMessageHelper.CodeIrregularCellSpacingApproximated,
                    LegacyConvertMessageHelper.Args(new { Team = "Home", TargetName = "HomeGlobalScoreRow" })),
            ]
        };

        Assert.False(LegacyConversionMessageFormatter.HasUserFacingWarnings(result));
        Assert.Equal(string.Empty, LegacyConversionMessageFormatter.BuildUserSummary(result));
        Assert.Contains(LegacyConvertMessageHelper.CodeLockOverlayGeometryConsumed,
            LegacyConversionMessageFormatter.BuildTechnicalDetails(result));
    }

    [Fact]
    public async Task ConverterUsesLegacyCutSceneCompositeScoreBlueprint()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInCutSceneLayoutWithIndependentScores(builtInRoot);
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson: "{}",
                customResources: [],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/CutSceneWindowConfig-BaseCanvas.json"] =
                        """
                        {
                          "SurTeamMajorPoint": { "Left": 380, "Top": 42, "Width": 120, "Height": 36 },
                          "HunTeamMajorPoint": { "Left": 971, "Top": 42, "Width": 120, "Height": 36 }
                        }
                        """
                });

            var result = await ConvertAsync(builtInRoot, root, archivePath, "converted.legacy.cutscene-score");

            Assert.True(result.Success, result.ErrorMessage);
            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var layout = ReadLayout(archive, "FrontedLayouts/CutSceneWindow.json");

            var sur = Assert.IsType<TextFrontedControlConfig>(layout.Controls["SurTeamMajorPoint"]);
            var hun = Assert.IsType<TextFrontedControlConfig>(layout.Controls["HunTeamMajorPoint"]);
            Assert.Equal("CurrentGame.MatchScore.CurrentSurTeamMajorText", Assert.Single(sur.TextBinding!.Sources).Path);
            Assert.Equal("CurrentGame.MatchScore.CurrentHunTeamMajorText", Assert.Single(hun.TextBinding!.Sources).Path);

            foreach (var name in new[] { "SurWin", "SurTie", "W1", "D1", "HunWin", "HunTie", "W2", "D2" })
            {
                Assert.DoesNotContain(name, layout.Controls.Keys);
            }
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConverterBuildsCutSceneFromBlueprintWithoutBuiltInLayoutRoot()
    {
        var root = CreateTempDirectory();
        try
        {
            var missingBuiltInRoot = Path.Combine(root, "builtIn-does-not-exist");
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson:
                """
                {
                  "CutSceneWindowSettings": {
                    "TextSettings": {
                      "MajorPoints": { "IsActive": false, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Normal", "FontSize": 48 }
                    }
                  }
                }
                """,
                customResources: [],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/CutSceneWindowConfig-BaseCanvas.json"] =
                        """
                        {
                          "SurTeamMajorPoint": { "Left": 380, "Top": 42, "Width": 120, "Height": 36 },
                          "HunTeamMajorPoint": { "Left": 971, "Top": 42, "Width": 120, "Height": 36 }
                        }
                        """
                });

            var result = await ConvertAsync(missingBuiltInRoot, root, archivePath, "converted.legacy.no-built-in");

            Assert.True(result.Success, result.ErrorMessage);
            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var layout = ReadLayout(archive, "FrontedLayouts/CutSceneWindow.json");

            var sur = Assert.IsType<TextFrontedControlConfig>(layout.Controls["SurTeamMajorPoint"]);
            var hun = Assert.IsType<TextFrontedControlConfig>(layout.Controls["HunTeamMajorPoint"]);
            Assert.Equal("CurrentGame.MatchScore.CurrentSurTeamMajorText", Assert.Single(sur.TextBinding!.Sources).Path);
            Assert.Equal("CurrentGame.MatchScore.CurrentHunTeamMajorText", Assert.Single(hun.TextBinding!.Sources).Path);

            foreach (var name in new[] { "SurWin", "SurTie", "W1", "D1", "HunWin", "HunTie", "W2", "D2" })
            {
                Assert.DoesNotContain(name, layout.Controls.Keys);
            }
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConverterPreservesLegacyImageStyleBlueprintDefaults()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson: "{}",
                customResources: [],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/CutSceneWindowConfig-BaseCanvas.json"] = "{}",
                    ["FrontElementsConfig/GameDataWindowConfig-BaseCanvas.json"] = "{}"
                });

            var result = await ConvertAsync(
                Path.Combine(root, "missing-built-in"),
                root,
                archivePath,
                "converted.legacy.image-style");

            Assert.True(result.Success, result.ErrorMessage);
            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var cutScene = ReadLayout(archive, "FrontedLayouts/CutSceneWindow.json");

            var cutSceneMap = Assert.IsType<BorderedImageFrontedControlConfig>(cutScene.Controls["Map"]);

            foreach (var name in new[] { "SurPick0", "SurPick1", "SurPick2", "SurPick3" })
            {
                var pick = Assert.IsType<BorderedImageFrontedControlConfig>(cutScene.Controls[name]);
                Assert.Equal(ImageSizingMode.OverflowCrop, pick.SizingMode);
            }

            var hunPick = Assert.IsType<BorderedImageFrontedControlConfig>(cutScene.Controls["HunPick"]);
            Assert.Equal(ImageSizingMode.OverflowCrop, hunPick.SizingMode);

            foreach (var name in new[] { "SurTeamLogo", "HunTeamLogo" })
            {
                var logo = Assert.IsType<ImageFrontedControlConfig>(cutScene.Controls[name]);
            }

            var gameData = ReadLayout(archive, "FrontedLayouts/GameDataWindow.json");
            var gameDataMap = Assert.IsType<BorderedImageFrontedControlConfig>(gameData.Controls["Map"]);

            foreach (var name in new[] { "Player0Header", "Player1Header", "Player2Header", "Player3Header" })
            {
                var header = Assert.IsType<BorderedImageFrontedControlConfig>(gameData.Controls[name]);
                Assert.Equal(ImageSizingMode.Auto, header.SizingMode);
            }

            var hunImage = Assert.IsType<BorderedImageFrontedControlConfig>(gameData.Controls["HunImage"]);
            Assert.Equal(ImageSizingMode.FillContainer, hunImage.SizingMode);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConverterMigratesLegacyTextSettingsIntoV3Layouts()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInTextStyleLayouts(builtInRoot);
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson: LegacyTextSettingsConfigJson,
                customResources: [],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/BpWindowConfig-BaseCanvas.json"] = """{ "SurTeamName": { "Left": 101 } }""",
                    ["FrontElementsConfig/CutSceneWindowConfig-BaseCanvas.json"] = "{}",
                    ["FrontElementsConfig/ScoreSurWindowConfig-BaseCanvas.json"] = "{}",
                    ["FrontElementsConfig/ScoreHunWindowConfig-BaseCanvas.json"] = "{}",
                    ["FrontElementsConfig/ScoreGlobalWindowConfig-BaseCanvas.json"] = "{}",
                    ["FrontElementsConfig/GameDataWindowConfig-BaseCanvas.json"] = "{}",
                    ["FrontElementsConfig/WidgetsWindowConfig-BpOverViewCanvas.json"] = "{}",
                    ["FrontElementsConfig/WidgetsWindowConfig-MapV2Canvas.json"] = "{}"
                });

            var result = await ConvertAsync(builtInRoot, root, archivePath, "converted.legacy.text-style");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.False(
                LegacyConversionMessageFormatter.HasUserFacingWarnings(result),
                string.Join(Environment.NewLine, result.Warnings));
            Assert.Contains(result.Diagnostics, item => item.Contains("TextSettingsApplied", StringComparison.Ordinal));

            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var bp = ReadLayout(archive, "FrontedLayouts/BpWindow.json");
            var surTeamName = Assert.IsType<TextFrontedControlConfig>(bp.Controls["SurTeamName"]);
            var hunTeamName = Assert.IsType<TextFrontedControlConfig>(bp.Controls["HunTeamName"]);
            var timer = Assert.IsType<TextFrontedControlConfig>(bp.Controls["Timer"]);

            var cutScene = ReadLayout(archive, "FrontedLayouts/CutSceneWindow.json");
            var cutSurTeamName = Assert.IsType<TextFrontedControlConfig>(cutScene.Controls["SurTeamName"]);
            var cutHunTeamName = Assert.IsType<TextFrontedControlConfig>(cutScene.Controls["HunTeamName"]);

            var scoreSur = ReadLayout(archive, "FrontedLayouts/ScoreSurWindow.json");
            AssertTextStyle(scoreSur.Controls["GameScoresSur"], "#FF000000", "Bold", LegacyFont, 100);
            var scoreHun = ReadLayout(archive, "FrontedLayouts/ScoreHunWindow.json");
            AssertTextStyle(scoreHun.Controls["GameScoresHun"], "#FF000000", "Bold", LegacyFont, 100);

            var scoreGlobal = ReadLayout(archive, "FrontedLayouts/ScoreGlobalWindow.json");
            AssertTextStyle(scoreGlobal.Controls["HomeTeamName"], "#FF000000", "Bold", LegacyFont, 24);
            AssertTextStyle(scoreGlobal.Controls["AwayTeamName"], "#FF000000", "Bold", LegacyFont, 24);
            AssertTextStyle(scoreGlobal.Controls["HomeScoreTotal"], "#FF000000", "Bold", LegacyFont, 40);
            AssertTextStyle(scoreGlobal.Controls["AwayScoreTotal"], "#FF000000", "Bold", LegacyFont, 40);
            AssertTextStyle(scoreGlobal.Controls["HomeGlobalScoreRow"], "#FF000000", "Bold", LegacyFont, 24);
            AssertTextStyle(scoreGlobal.Controls["AwayGlobalScoreRow"], "#FF000000", "Bold", LegacyFont, 24);

            var gameData = ReadLayout(archive, "FrontedLayouts/GameDataWindow.json");
            AssertTextStyle(gameData.Controls["SurId0"], "#FF000000", "Bold", LegacyFont, 22);
            AssertTextStyle(gameData.Controls["Header_ID"], "#FFFFFFFF", "Normal", NotoSansFont, 16);
            AssertTextStyle(gameData.Controls["Sur0MachineDecoded"], "#FF000000", "Bold", LegacyFont, 22);

            var overview = ReadLayout(archive, "FrontedLayouts/BpOverviewWindow.json");
            AssertTextStyle(overview.Controls["GameProgress"], "#FF000000", "Bold", LegacyFont, 22);
            AssertTextStyle(overview.Controls["GameScoresSur"], "#FF000000", "Bold", LegacyFont, 20);
            AssertTextStyle(overview.Controls["GameScoresHun"], "#FF000000", "Bold", LegacyFont, 20);

            var mapV2 = ReadLayout(archive, "FrontedLayouts/MapV2Window.json");
            var map = Assert.IsType<MapV2DisplayControlConfig>(mapV2.Controls["Arms_Factory"]);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConverterMapsLegacyScoreGlobalBo3BackgroundAndNormalizesGeometryNoise()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInScoreGlobalLayout(builtInRoot);
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson:
                """
                {
                  "ScoreWindowSettings": {
                    "GlobalScoreBgImageUri": "scoreGlobal.png",
                    "GlobalScoreBgImageUriBo3": "scoreGlobalBo3.png"
                  }
                }
                """,
                customResources: ["scoreGlobal.png", "scoreGlobalBo3.png"],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/ScoreGlobalWindowConfig-BaseCanvas.json"] =
                        """{ "HomeTeamGame1FirstHalf": { "Left": 100.0000000006, "Top": 12.4999999997 }, "HomeTeamGame1SecondHalf": { "Left": 190.0000000006, "Top": 12.4999999997 } }"""
                });

            var result = await ConvertAsync(builtInRoot, root, archivePath, "converted.legacy.bo3-bg");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.DoesNotContain(result.Diagnostics, item => item.Contains("GlobalScoreBgImageUriBo3", StringComparison.Ordinal));
            Assert.DoesNotContain(result.Warnings, item => item.Contains("GlobalScoreBgImageUriBo3", StringComparison.Ordinal));
            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var layoutJson = ReadZipEntry(archive, "FrontedLayouts/ScoreGlobalWindow.json");
            var layout = JsonSerializer.Deserialize<FrontedWindowConfig>(layoutJson)!.ToCanvasConfig();

            Assert.True(layout.EnableBoModeStates);
            Assert.StartsWith(
                "bpui://converted.legacy.bo3-bg/resources/images/scoreGlobalBo3-",
                layout.BoModeStates["Bo3"].BackgroundImage);
            Assert.Equal(2, archive.Entries.Count(entry => entry.FullName.StartsWith("resources/images/", StringComparison.Ordinal)));
            Assert.DoesNotContain(".0000000006", layoutJson, StringComparison.Ordinal);
            var row = Assert.IsType<GlobalScoreRowControlConfig>(layout.Controls["HomeGlobalScoreRow"]);
            Assert.Contains(row.Cells, cell => cell is
            {
                GameNumber: 1,
                GameKind: ScoreGameKind.Normal,
                HalfKind: ScoreHalfKind.FirstHalf,
                X: 0,
                Y: 0
            });
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public void FormatterSummarizesActionableWarningsWithoutClosestCandidates()
    {
        var result = new FrontedLayoutPackageLegacyConvertResult
        {
            Success = true,
            Messages =
            [
                LegacyConvertMessageHelper.Warning(LegacyConvertMessageHelper.CodeResourceMissing,
                    LegacyConvertMessageHelper.Args(new { Field = "BpWindowSettings.CurrentBanLockImageUri", Value = "C:\\legacy\\missing.png" })),
                LegacyConvertMessageHelper.Warning(LegacyConvertMessageHelper.CodeControlNotInBlueprintMap,
                    LegacyConvertMessageHelper.Args(new { SourceWindow = "WidgetsWindow", SourceCanvas = "BpOverViewCanvas", ControlName = "LegacyOnly" })),
                LegacyConvertMessageHelper.Warning(LegacyConvertMessageHelper.CodeUnknownLayoutFileSkipped,
                    LegacyConvertMessageHelper.Args(new { FileName = "UnknownWindowConfig-BaseCanvas.json" })),
                LegacyConvertMessageHelper.Error(LegacyConvertMessageHelper.CodeLayoutValidationError,
                    LegacyConvertMessageHelper.Args(new { TargetWindow = "BpWindow", CanvasName = "BaseCanvas", Details = "bad" })),
            ]
        };

        var summary = LegacyConversionMessageFormatter.BuildUserSummary(result);

        Assert.True(LegacyConversionMessageFormatter.HasUserFacingWarnings(result));
        Assert.Contains("LegacyConvert.UnknownLayoutFileSkipped", LegacyConversionMessageFormatter.BuildTechnicalDetails(result));
    }

    [Fact]
    public void LegacyBlueprintAuditDocumentListsEveryLegacyNamedElement()
    {
        var rows = ReadLegacyBlueprintAuditRows();
        var validStatuses = new HashSet<string>(
            ["Mapped", "Folded", "Aggregated", "Unsupported", "RemovedWithReason"],
            StringComparer.Ordinal);
        var validPropertyParityStatuses = new HashSet<string>(
            ["Exact", "Approximate", "Folded", "Aggregated", "UnsupportedWithReason"],
            StringComparer.Ordinal);

        Assert.All(rows, row => Assert.Contains(row.Status, validStatuses));
        Assert.All(rows, row => Assert.Contains(row.PropertyParityStatus, validPropertyParityStatuses));
        Assert.All(rows.Where(row => row.Status == "Mapped"), row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.TargetControlType));
            Assert.False(string.IsNullOrWhiteSpace(row.PropertyParityStatus));
        });
        foreach (var expected in EnumerateExpectedLegacyNamedElements())
        {
            var row = Assert.Single(rows, row =>
                string.Equals(row.SourceWindow, expected.SourceWindow, StringComparison.Ordinal)
                && string.Equals(row.SourceCanvas, expected.SourceCanvas, StringComparison.Ordinal)
                && string.Equals(row.LegacyName, expected.LegacyName, StringComparison.Ordinal));

            Assert.Contains(row.Status, validStatuses);
        }

        var mapBpRows = rows
            .Where(row => row.SourceWindow == "WidgetsWindow" && row.SourceCanvas == "MapBpCanvas")
            .ToArray();
        Assert.NotEmpty(mapBpRows);
        Assert.All(mapBpRows, row =>
        {
            Assert.Equal("Unsupported", row.Status);
            Assert.Contains("Legacy MapBpCanvas / MapBpV1 is not supported", row.Notes, StringComparison.Ordinal);
        });

        var scoreCells = rows
            .Where(row => row.SourceWindow == "ScoreGlobalWindow"
                          && row.SourceCanvas == "BaseCanvas"
                          && row.LegacyName.Contains("TeamGame", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(40, scoreCells.Length);
        Assert.All(scoreCells, row => Assert.Equal("Aggregated", row.Status));
        Assert.All(scoreCells, row => Assert.Equal("Aggregated", row.PropertyParityStatus));
    }

    [Fact]
    public async Task ConverterConsumesScoreGlobalOvertimeCellsWithoutUnmatchedWarnings()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInScoreGlobalLayout(builtInRoot);
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson: "{}",
                customResources: [],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/ScoreGlobalWindowConfig-BaseCanvas.json"] =
                        """
                        {
                          "HomeTeamGame5OvertimeFirstHalf": { "Left": 900, "Top": 90 },
                          "HomeTeamGame5OvertimeSecondHalf": { "Left": 990, "Top": 90 },
                          "AwayTeamGame5OvertimeFirstHalf": { "Left": 900, "Top": 150 },
                          "AwayTeamGame5OvertimeSecondHalf": { "Left": 990, "Top": 150 }
                        }
                        """
                });

            var result = await ConvertAsync(builtInRoot, root, archivePath, "converted.legacy.overtime");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("Overtime", StringComparison.Ordinal));
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("no v3 control matches", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Diagnostics, item => item.Contains("OvertimeScoreCellsAggregated", StringComparison.Ordinal));
            Assert.False(LegacyConversionMessageFormatter.HasUserFacingWarnings(result));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConverterConsumesLegacyCurrentBanLockOverlayGeometry()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInWidgetsOverviewLayout(builtInRoot);
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson: "{}",
                customResources: [],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/WidgetsWindowConfig-BpOverViewCanvas.json"] =
                        """
                        {
                          "HunBanCurrentLock0": { "Left": 11, "Top": 22, "Width": 33, "Height": 44 },
                          "SurBanCurrent0": { "Left": 100, "Top": 20, "Width": 30, "Height": 40 },
                          "SurBanCurrentLock0": { "Left": 1, "Top": 2, "Width": 3, "Height": 4 }
                        }
                        """
                });

            var result = await ConvertAsync(builtInRoot, root, archivePath, "converted.legacy.lock-geometry");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("BanCurrentLock", StringComparison.Ordinal));
            Assert.Contains(result.Diagnostics, item => item.Contains("HunBanCurrentLock0", StringComparison.Ordinal)
                && item.Contains("HunBanCurrent0", StringComparison.Ordinal));
            Assert.False(LegacyConversionMessageFormatter.HasUserFacingWarnings(result));

            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var layout = ReadLayout(archive, "FrontedLayouts/BpOverviewWindow.json");
            var hun = Assert.IsType<ImageFrontedControlConfig>(layout.Controls["HunBanCurrent0"]);
            Assert.True(hun.Lockable);
            Assert.Contains(result.Diagnostics, item => item.Contains("FoldedGeometryNotRepresentable", StringComparison.Ordinal));
            var sur = Assert.IsType<ImageFrontedControlConfig>(layout.Controls["SurBanCurrent0"]);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConverterFailsWhenLegacyControlIsMissingFromBlueprint()
    {
        var root = CreateTempDirectory();
        try
        {
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson: "{}",
                customResources: [],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/CutSceneWindowConfig-BaseCanvas.json"] =
                        """{ "LegacyOnly": { "Left": 1, "Top": 2, "Width": 3, "Height": 4 } }"""
                });

            var result = await ConvertAsync(
                Path.Combine(root, "missing-built-in"),
                root,
                archivePath,
                "converted.legacy.unmapped");

            Assert.False(result.Success);
            Assert.Contains("explicit legacy blueprint map", result.ErrorMessage);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConverterMigratesLockAndPickingBorderResourcesIntoV3Config()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInBpLayout(builtInRoot);
            WriteBuiltInWidgetsOverviewLayout(builtInRoot);
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson:
                """
                {
                  "BpWindowSettings": {
                    "CurrentBanLockImageUri": "C:\\legacy\\CurrentBanLock.png",
                    "GlobalBanLockImageUri": "C:\\legacy\\GlobalBanLock.png",
                    "PickingBorderImageUri": "C:\\legacy\\PickingBorder.png",
                    "PickingBorderColor": "#FF112233"
                  },
                  "WidgetsWindowSettings": {
                    "CurrentBanLockImageUri": "C:\\legacy\\WidgetCurrentBanLock.png",
                    "MapBpV2PickingBorderImageUri": "C:\\legacy\\PickingBorder.png",
                    "MapBpV2_PickingBorderColor": "#FF445566"
                  }
                }
                """,
                customResources:
                [
                    "CurrentBanLock.png",
                    "GlobalBanLock.png",
                    "PickingBorder.png",
                    "WidgetCurrentBanLock.png"
                ],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/BpWindowConfig-BaseCanvas.json"] = "{}",
                    ["FrontElementsConfig/WidgetsWindowConfig-BpOverViewCanvas.json"] = "{}"
                });

            var result = await ConvertAsync(builtInRoot, root, archivePath, "converted.legacy.assets");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("CurrentBanLock", StringComparison.Ordinal));

            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var bpLayout = ReadLayout(archive, "FrontedLayouts/BpWindow.json");
            var current = Assert.IsType<ImageFrontedControlConfig>(bpLayout.Controls["SurBanCurrent0"]);
            var global = Assert.IsType<ImageFrontedControlConfig>(bpLayout.Controls["SurGlobalBan0"]);
            var pick = Assert.IsAssignableFrom<ImageFrontedControlConfig>(bpLayout.Controls["SurPick0"]);
            Assert.StartsWith("bpui://converted.legacy.assets/resources/images/CurrentBanLock-", current.LockImagePath);
            Assert.StartsWith("bpui://converted.legacy.assets/resources/images/GlobalBanLock-", global.LockImagePath);
            Assert.StartsWith("bpui://converted.legacy.assets/resources/images/PickingBorder-", pick.PickingBorderImagePath);

            var widgetsLayout = ReadLayout(archive, "FrontedLayouts/BpOverviewWindow.json");
            var widgetsCurrent = Assert.IsType<ImageFrontedControlConfig>(widgetsLayout.Controls["HunBanCurrent0"]);
            Assert.StartsWith("bpui://converted.legacy.assets/resources/images/WidgetCurrentBanLock-", widgetsCurrent.LockImagePath);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConverterSplitsWidgetsWindowWithFixedSizesResourcesAndMapBpWarning()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInWidgetsOverviewLayout(builtInRoot);
            WriteBuiltInMapV2Layout(builtInRoot);
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson:
                """
                {
                  "WidgetsWindowSettings": {
                    "WindowSize": { "Width": 1440, "Height": 716, "IsActive": false },
                    "BackgroundColor": "#00FF00",
                    "AllowsWindowTransparency": false,
                    "BpOverviewBgUri": "C:\\legacy\\overview.png",
                    "MapBpV2BgUri": "C:\\legacy\\mapv2.png",
                    "MapBpV2PickingBorderImageUri": "C:\\legacy\\border.png",
                    "MapBpV2_PickingBorderColor": "#FF445566"
                  }
                }
                """,
                customResources: ["overview.png", "mapv2.png", "border.png"],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/WidgetsWindowConfig-BpOverViewCanvas.json"] =
                        """
                        {
                          "HunBanCurrent0": { "Left": 11, "Top": 22, "Width": 33, "Height": 44 },
                          "SurBanCurrent0": { "Left": 1120, "Top": 170, "Width": 40, "Height": 20 }
                        }
                        """,
                    ["FrontElementsConfig/WidgetsWindowConfig-MapV2Canvas.json"] =
                        """
                        {
                          "Arms_Factory": { "Left": 10, "Top": 20, "Width": 300, "Height": 90 }
                        }
                        """,
                    ["FrontElementsConfig/WidgetsWindowConfig-MapBpCanvas.json"] = "{}"
                });

            var result = await ConvertAsync(builtInRoot, root, archivePath, "converted.legacy.widgets");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(2, result.LayoutCount);
            Assert.Contains(result.Infos, warning => warning.Contains("MapBpV1Skipped", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, warning => warning.Contains("BpOverviewOutOfBounds", StringComparison.Ordinal));

            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var entryNames = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();
            Assert.Contains("FrontedLayouts/BpOverviewWindow.json", entryNames);
            Assert.Contains("FrontedLayouts/MapV2Window.json", entryNames);
            Assert.DoesNotContain("FrontedLayouts/MapBpWindow.json", entryNames);

            var manifest = JsonNode.Parse(ReadZipEntry(archive, "manifest.json"))!.AsObject();
            Assert.Equal(FrontedLayoutConstants.WindowCentricLayoutModel, manifest["LayoutModel"]!.GetValue<string>());
            var layouts = manifest["Content"]!["Layouts"]!.AsArray();
            Assert.All(layouts, layout =>
            {
                var entry = layout!.AsObject();
                Assert.True(entry.ContainsKey("Window"));
                Assert.True(entry.ContainsKey("Path"));
                Assert.False(entry.ContainsKey("Canvas"));
                Assert.StartsWith("FrontedLayouts/", entry["Path"]!.GetValue<string>());
            });

            var overview = ReadWindowConfig(archive, "FrontedLayouts/BpOverviewWindow.json");
            Assert.False(overview.WindowSettings.AllowsTransparency);

            var mapV2 = ReadWindowConfig(archive, "FrontedLayouts/MapV2Window.json");
            var display = Assert.IsType<MapV2DisplayControlConfig>(mapV2.ControlLayout.Controls["Arms_Factory"]);
            Assert.StartsWith("bpui://converted.legacy.widgets/resources/images/border-", display.PickingBorderImagePath);

            var expectedMapKeys = new Dictionary<string, string>(StringComparer.Ordinal)
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
            Assert.Equal(expectedMapKeys.Count, mapV2.ControlLayout.Controls.Values.OfType<MapV2DisplayControlConfig>().Count());
            foreach (var (controlName, mapKey) in expectedMapKeys)
            {
                var mapControl = Assert.IsType<MapV2DisplayControlConfig>(mapV2.ControlLayout.Controls[controlName]);
                Assert.Equal(mapKey, mapControl.MapKey);
            }
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConverterIgnoresLeakedLegacyTextSettingsIsActive()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInTextStyleLayouts(builtInRoot);
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson:
                """
                {
                  "BpWindowSettings": {
                    "TextSettings": {
                      "TeamName": { "IsActive": false, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 30 },
                      "Timer": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 30 }
                    }
                  }
                }
                """,
                customResources: [],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/BpWindowConfig-BaseCanvas.json"] = "{}"
                });

            var result = await ConvertAsync(builtInRoot, root, archivePath, "converted.legacy.text-active");

            Assert.True(result.Success, result.ErrorMessage);
            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var layout = ReadLayout(archive, "FrontedLayouts/BpWindow.json");
            var teamName = Assert.IsType<TextFrontedControlConfig>(layout.Controls["SurTeamName"]);
            var timer = Assert.IsType<TextFrontedControlConfig>(layout.Controls["Timer"]);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task MissingLegacyLockResourceDoesNotCrashConversion()
    {
        var root = CreateTempDirectory();
        try
        {
            var builtInRoot = Path.Combine(root, "builtIn");
            WriteBuiltInBpLayout(builtInRoot);
            var archivePath = Path.Combine(root, "legacy.bpui");
            CreateLegacyArchive(
                archivePath,
                configJson:
                """
                {
                  "BpWindowSettings": {
                    "CurrentBanLockImageUri": "C:\\legacy\\MissingLock.png"
                  }
                }
                """,
                customResources: [],
                layouts: new Dictionary<string, string>
                {
                    ["FrontElementsConfig/BpWindowConfig-BaseCanvas.json"] = "{}"
                });

            var result = await ConvertAsync(builtInRoot, root, archivePath, "converted.legacy.missing-lock");

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("MissingLock.png", StringComparison.Ordinal));
            Assert.False(LegacyConversionMessageFormatter.HasUserFacingWarnings(result));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static Task<FrontedLayoutPackageLegacyConvertResult> ConvertAsync(
        string builtInRoot,
        string root,
        string archivePath,
        string packageId)
    {
        var converter = new FrontedLayoutPackageLegacyConverter(builtInRoot, Path.Combine(root, "temp"));
        return converter.ConvertAsync(new FrontedLayoutPackageLegacyConvertRequest
        {
            LegacyPackagePath = archivePath,
            PackageId = packageId,
            Name = packageId
        }, TestContext.Current.CancellationToken);
    }

    private static void WriteBuiltInScoreGlobalLayout(string builtInRoot)
    {
        WriteFile(
            Path.Combine(builtInRoot, "ScoreGlobalWindow", "BaseCanvas.json"),
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 195,
              "BackgroundImage": "Resources/scoreGlobal.png",
              "HomeGlobalScoreRow": {
                "ControlType": "GlobalScoreRow",
                "Left": 175,
                "Top": 93,
                "TeamType": "HomeTeam",
                "MajorGameGap": 180,
                "HalfGameGap": 90
              },
              "AwayGlobalScoreRow": {
                "ControlType": "GlobalScoreRow",
                "Left": 175,
                "Top": 150,
                "TeamType": "AwayTeam",
                "MajorGameGap": 180,
                "HalfGameGap": 90
              }
            }
            """);
    }

    private static void WriteBuiltInCutSceneLayoutWithIndependentScores(string builtInRoot)
    {
        WriteFile(
            Path.Combine(builtInRoot, "CutSceneWindow", "BaseCanvas.json"),
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "BackgroundImage": "Resources/cutScene.png",
              "SurTeamMajorPoint": {
                "ControlType": "Text",
                "Left": 1,
                "Top": 2,
                "Visibility": "Collapsed",
                "TextBinding": { "Sources": [ { "Path": "CurrentGame.MatchScore.CurrentSurTeamMajorText" } ] }
              },
              "HunTeamMajorPoint": {
                "ControlType": "Text",
                "Left": 3,
                "Top": 4,
                "Visibility": "Collapsed",
                "TextBinding": { "Sources": [ { "Path": "CurrentGame.MatchScore.CurrentHunTeamMajorText" } ] }
              },
              "SurWin": { "ControlType": "Text", "Left": 10, "Top": 10, "TextBinding": { "Sources": [ { "Path": "CurrentGame.MatchScore.CurrentSurTeamMajorWin" } ] } },
              "SurTie": { "ControlType": "Text", "Left": 20, "Top": 10, "TextBinding": { "Sources": [ { "Path": "CurrentGame.MatchScore.CurrentSurTeamMajorTie" } ] } },
              "W1": { "ControlType": "Text", "Left": 30, "Top": 10, "Text": "W" },
              "D1": { "ControlType": "Text", "Left": 40, "Top": 10, "Text": "D" },
              "HunWin": { "ControlType": "Text", "Left": 50, "Top": 10, "TextBinding": { "Sources": [ { "Path": "CurrentGame.MatchScore.CurrentHunTeamMajorWin" } ] } },
              "HunTie": { "ControlType": "Text", "Left": 60, "Top": 10, "TextBinding": { "Sources": [ { "Path": "CurrentGame.MatchScore.CurrentHunTeamMajorTie" } ] } },
              "W2": { "ControlType": "Text", "Left": 70, "Top": 10, "Text": "W" },
              "D2": { "ControlType": "Text", "Left": 80, "Top": 10, "Text": "D" }
            }
            """);
    }

    private static void WriteBuiltInWidgetsOverviewLayout(string builtInRoot)
    {
        WriteFile(
            Path.Combine(builtInRoot, "WidgetsWindow", "BpOverViewCanvas.json"),
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "BackgroundImage": "Resources/bpOverview.png",
              "HunBanCurrent0": {
                "ControlType": "Image",
                "BindingPath": "CurrentGame.CurrentHunBannedList[0].HeaderImageSingleColor",
                "Lockable": true,
                "LockVisibilityBindingPath": "CanCurrentHunBannedList[0]",
                "LockVisibleWhen": "VisibleWhenFalse",
                "Left": 1,
                "Top": 2,
                "Width": 3,
                "Height": 4
              },
              "SurBanCurrent0": {
                "ControlType": "Image",
                "BindingPath": "CurrentGame.CurrentSurBannedList[0].HeaderImageSingleColor",
                "Lockable": true,
                "LockVisibilityBindingPath": "CanCurrentSurBannedList[0]",
                "LockVisibleWhen": "VisibleWhenFalse",
                "Left": 5,
                "Top": 6,
                "Width": 7,
                "Height": 8
              }
            }
            """);
    }

    private static void WriteBuiltInMapV2Layout(string builtInRoot)
    {
        WriteFile(
            Path.Combine(builtInRoot, "WidgetsWindow", "MapV2Canvas.json"),
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 160,
              "BackgroundImage": "Resources/mapBpV2.png",
              "Arms_Factory": {
                "ControlType": "MapV2Display",
                "Left": 1,
                "Top": 2,
                "Width": 3,
                "Height": 4,
                "MapKey": "ArmsFactory"
              }
            }
            """);
    }

    private static void WriteBuiltInBpLayout(string builtInRoot)
    {
        WriteFile(
            Path.Combine(builtInRoot, "BpWindow", "BaseCanvas.json"),
            """
            {
              "Version": 3,
              "CanvasWidth": 1440,
              "CanvasHeight": 810,
              "BackgroundImage": "Resources/bp.png",
              "SurBanCurrent0": {
                "ControlType": "Image",
                "BindingPath": "CurrentGame.CurrentSurBannedList[0].HeaderImageSingleColor",
                "Lockable": true,
                "LockVisibilityBindingPath": "CanCurrentSurBannedList[0]",
                "LockVisibleWhen": "VisibleWhenFalse",
                "Left": 10,
                "Top": 20,
                "Width": 30,
                "Height": 40
              },
              "SurGlobalBan0": {
                "ControlType": "Image",
                "BindingPath": "CurrentGame.SurTeam.GlobalBannedSurList[0].HeaderImageSingleColor",
                "Lockable": true,
                "LockVisibilityBindingPath": "CanGlobalSurBannedList[0]",
                "LockVisibleWhen": "VisibleWhenFalse",
                "Left": 50,
                "Top": 60,
                "Width": 70,
                "Height": 80
              },
              "SurPick0": {
                "ControlType": "Image",
                "Left": 90,
                "Top": 100,
                "Width": 110,
                "Height": 120,
                "BindingPath": "CurrentGame.SurPlayerList[0].PictureShown",
                "PickingBorderAvailable": true,
                "PickingBorderName": "SurPickingBorder0"
              },
              "SurPick1": {
                "ControlType": "Image",
                "Left": 90,
                "Top": 100,
                "Width": 110,
                "Height": 120,
                "BindingPath": "CurrentGame.SurPlayerList[1].PictureShown",
                "PickingBorderAvailable": true,
                "PickingBorderName": "SurPickingBorder1"
              },
              "SurPick2": {
                "ControlType": "Image",
                "Left": 90,
                "Top": 100,
                "Width": 110,
                "Height": 120,
                "BindingPath": "CurrentGame.SurPlayerList[2].PictureShown",
                "PickingBorderAvailable": true,
                "PickingBorderName": "SurPickingBorder2"
              },
              "SurPick3": {
                "ControlType": "Image",
                "Left": 90,
                "Top": 100,
                "Width": 110,
                "Height": 120,
                "BindingPath": "CurrentGame.SurPlayerList[3].PictureShown",
                "PickingBorderAvailable": true,
                "PickingBorderName": "SurPickingBorder3"
              },
              "HunPick": {
                "ControlType": "Image",
                "Left": 90,
                "Top": 100,
                "Width": 110,
                "Height": 120,
                "BindingPath": "CurrentGame.HunPlayer.PictureShown",
                "PickingBorderAvailable": true,
                "PickingBorderName": "HunPickingBorder"
              }
            }
            """);
    }

    private static void WriteBuiltInTextStyleLayouts(string builtInRoot)
    {
        WriteFile(Path.Combine(builtInRoot, "BpWindow", "BaseCanvas.json"),
            TextLayout("""
              "SurTeamName": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Sur", "Color": "#FFFFFFFF", "FontFamily": "Noto Sans", "FontWeight": "Normal", "FontSize": 16 },
              "HunTeamName": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Hun", "Color": "#FFFFFFFF", "FontFamily": "Noto Sans", "FontWeight": "Normal", "FontSize": 16 },
              "Timer": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "00:00", "Color": "#FFFFFFFF", "FontFamily": "Noto Sans", "FontWeight": "Normal", "FontSize": 16 },
              "SurPick0": { "ControlType": "Image", "Left": 1, "Top": 1, "Width": 10, "Height": 10, "BindingPath": "CurrentGame.SurPlayerList[0].PictureShown", "PickingBorderAvailable": true, "PickingBorderName": "SurPickingBorder0" },
              "SurPick1": { "ControlType": "Image", "Left": 1, "Top": 1, "Width": 10, "Height": 10, "BindingPath": "CurrentGame.SurPlayerList[1].PictureShown", "PickingBorderAvailable": true, "PickingBorderName": "SurPickingBorder1" },
              "SurPick2": { "ControlType": "Image", "Left": 1, "Top": 1, "Width": 10, "Height": 10, "BindingPath": "CurrentGame.SurPlayerList[2].PictureShown", "PickingBorderAvailable": true, "PickingBorderName": "SurPickingBorder2" },
              "SurPick3": { "ControlType": "Image", "Left": 1, "Top": 1, "Width": 10, "Height": 10, "BindingPath": "CurrentGame.SurPlayerList[3].PictureShown", "PickingBorderAvailable": true, "PickingBorderName": "SurPickingBorder3" },
              "HunPick": { "ControlType": "Image", "Left": 1, "Top": 1, "Width": 10, "Height": 10, "BindingPath": "CurrentGame.HunPlayer.PictureShown", "PickingBorderAvailable": true, "PickingBorderName": "HunPickingBorder" }
            """));

        WriteFile(Path.Combine(builtInRoot, "CutSceneWindow", "BaseCanvas.json"),
            TextLayout("""
              "SurTeamName": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Sur", "Color": "#FFFFFFFF", "FontFamily": "Noto Sans", "FontWeight": "Normal", "FontSize": 28 },
              "HunTeamName": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Hun", "Color": "#FFFFFFFF", "FontFamily": "Noto Sans", "FontWeight": "Normal", "FontSize": 28 },
              "SurId0": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "S0" },
              "HunId": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "H" }
            """));

        WriteFile(Path.Combine(builtInRoot, "ScoreSurWindow", "BaseCanvas.json"),
            TextLayout("""
              "SurTeamName": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Sur" },
              "GameScoresSur": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "0" },
              "SurTeamMajorPoint": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "0" }
            """));

        WriteFile(Path.Combine(builtInRoot, "ScoreHunWindow", "BaseCanvas.json"),
            TextLayout("""
              "HunTeamName": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Hun" },
              "GameScoresHun": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "0" },
              "HunTeamMajorPoint": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "0" }
            """));

        WriteFile(Path.Combine(builtInRoot, "ScoreGlobalWindow", "BaseCanvas.json"),
            TextLayout("""
              "HomeTeamName": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Home" },
              "AwayTeamName": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Away" },
              "HomeScoreTotal": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "0" },
              "AwayScoreTotal": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "0" },
              "HomeGlobalScoreRow": { "ControlType": "GlobalScoreRow", "Left": 1, "Top": 1, "TeamType": "HomeTeam" },
              "AwayGlobalScoreRow": { "ControlType": "GlobalScoreRow", "Left": 1, "Top": 1, "TeamType": "AwayTeam" }
            """));

        WriteFile(Path.Combine(builtInRoot, "GameDataWindow", "BaseCanvas.json"),
            TextLayout("""
              "SurTeamName": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Sur" },
              "HunTeamName": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Hun" },
              "SurId0": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "S0" },
              "SurDataHeader0": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Header" },
              "SurData0": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "Data" }
            """));

        WriteFile(Path.Combine(builtInRoot, "WidgetsWindow", "BpOverViewCanvas.json"),
            TextLayout("""
              "GameProgress": { "ControlType": "GameProgressText", "Left": 1, "Top": 1 },
              "GameScoresSur": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "0" },
              "GameScoresHun": { "ControlType": "Text", "Left": 1, "Top": 1, "Text": "0" }
            """));

        WriteFile(Path.Combine(builtInRoot, "WidgetsWindow", "MapV2Canvas.json"),
            TextLayout("""
              "Arms_Factory": { "ControlType": "MapV2Display", "Left": 1, "Top": 1, "Width": 100, "Height": 100, "MapKey": "ArmsFactory" }
            """));
    }

    private static string TextLayout(string controls) =>
        $$"""
        {
          "Version": 3,
          "CanvasWidth": 1440,
          "CanvasHeight": 810,
        {{controls}}
        }
        """;

    private static void AssertTextStyle(
        FrontedControlConfigBase control,
        string color,
        string fontWeight,
        string fontFamily,
        double fontSize)
    {
        var text = Assert.IsAssignableFrom<IFrontedTextStyleConfig>(control);
    }

    private static IReadOnlyList<LegacyBlueprintAuditRow> ReadLegacyBlueprintAuditRows()
    {
        var docPath = Path.Combine(FindRepositoryRoot(), "docs", "legacy-v3-control-blueprint-map.md");
        return File.ReadLines(docPath)
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Select(line => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
            .Where(cells => cells[0] != "SourceWindow" && !cells[0].StartsWith("---", StringComparison.Ordinal))
            .Select(cells =>
            {
                Assert.Equal(12, cells.Length);
                return new LegacyBlueprintAuditRow(
                    cells[0],
                    cells[1],
                    cells[2],
                    cells[3],
                    cells[4],
                    cells[5],
                    cells[6],
                    cells[7],
                    cells[8],
                    cells[9],
                    cells[10],
                    cells[11]);
            })
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static IEnumerable<LegacyBlueprintAuditKey> EnumerateExpectedLegacyNamedElements()
    {
        foreach (var key in Keys(
                     "BpWindow",
                     "BaseCanvas",
                     "BaseCanvas",
                     "SurTeamLogo", "SurTeamMajorPoint", "SurTeamName", "GameScoresSur", "Timer", "GameScoresHun",
                     "HunTeamName", "HunTeamMajorPoint", "HunTeamLogo",
                     "HunBanCurrent0", "HunBanCurrent1", "HunBanCurrentLock0", "HunBanCurrentLock1",
                     "SurBanCurrent0", "SurBanCurrent1", "SurBanCurrent2", "SurBanCurrent3",
                     "SurBanCurrentLock0", "SurBanCurrentLock1", "SurBanCurrentLock2", "SurBanCurrentLock3",
                     "SurPick0", "SurPick1", "SurPick2", "SurPick3",
                     "SurPickingBorder0", "SurPickingBorder1", "SurPickingBorder2", "SurPickingBorder3",
                     "Map", "MapName", "GameProgress",
                     "HunGlobalBan0", "HunGlobalBan1", "HunGlobalBan2",
                     "HunGlobalBanLock0", "HunGlobalBanLock1", "HunGlobalBanLock2",
                     "SurGlobalBan0", "SurGlobalBan1", "SurGlobalBan2", "SurGlobalBan3",
                     "SurGlobalBan4", "SurGlobalBan5", "SurGlobalBan6", "SurGlobalBan7",
                     "SurGlobalBan8", "SurGlobalBan9", "SurGlobalBan10", "SurGlobalBan11",
                     "SurGlobalBanLock0", "SurGlobalBanLock1", "SurGlobalBanLock2", "SurGlobalBanLock3",
                     "SurGlobalBanLock4", "SurGlobalBanLock5", "SurGlobalBanLock6", "SurGlobalBanLock7",
                     "SurGlobalBanLock8", "SurGlobalBanLock9", "SurGlobalBanLock10", "SurGlobalBanLock11",
                     "HunPick", "HunPickingBorder", "SurId0", "SurId1", "SurId2", "SurId3", "HunId"))
        {
            yield return key;
        }

        foreach (var key in Keys(
                     "CutSceneWindow",
                     "BaseCanvas",
                     "BaseCanvas",
                     "SurTeamLogo", "SurTeamMajorPoint", "SurTeamName", "HunTeamName", "HunTeamMajorPoint",
                     "HunTeamLogo", "Map", "MapName", "GameProgress",
                     "SurPick0", "SurPick1", "SurPick2", "SurPick3", "HunPick",
                     "SurId0", "SurId1", "SurId2", "SurId3", "HunId",
                     "SurTalent0", "SurTalent1", "SurTalent2", "SurTalent3", "HunTalent", "Trait"))
        {
            yield return key;
        }

        foreach (var key in Keys(
                     "GameDataWindow",
                     "BaseCanvas",
                     "BaseCanvas",
                     "SurTeamLogo", "SurTeamMajorPoint", "SurTeamName", "GameScoresSur",
                     "Map", "MapName", "PickedMapName", "GameProgress", "GameScoresHun",
                     "HunTeamName", "HunTeamMajorPoint", "HunTeamLogo",
                     "Header_Character", "Header_ID", "Header_DecodingProgress", "Header_PalletStrikes",
                     "Header_Rescues", "Header_Heals", "Header_ContainmentTime",
                     "Player0Header", "Player1Header", "Player2Header", "Player3Header",
                     "SurId0", "SurId1", "SurId2", "SurId3",
                     "Sur0MachineDecoded", "Sur1MachineDecoded", "Sur2MachineDecoded", "Sur3MachineDecoded",
                     "Sur0PalletStunTimes", "Sur1PalletStunTimes", "Sur2PalletStunTimes", "Sur3PalletStunTimes",
                     "Sur0RescueTimes", "Sur1RescueTimes", "Sur2RescueTimes", "Sur3RescueTimes",
                     "Sur0HealedTimes", "Sur1HealedTimes", "Sur2HealedTimes", "Sur3HealedTimes",
                     "Sur0KiteTime", "Sur1KiteTime", "Sur2KiteTime", "Sur3KiteTime",
                     "HunImage", "HunId",
                     "Header_RemainingCiphers", "Header_PalletsDestroyed", "Header_SurvivorHits",
                     "Header_TerrorShocks", "Header_Knockdowns",
                     "HunMachineLeft", "HunPalletBroken", "HunHitTimes", "HunTerrorShockTimes", "HunDownTimes"))
        {
            yield return key;
        }

        foreach (var key in Keys("ScoreGlobalWindow", "BaseCanvas", "BaseCanvas", "MainTeamName", "AwayTeamName", "MainScoreTotal", "AwayScoreTotal"))
        {
            yield return key;
        }

        foreach (var key in Keys("ScoreHunWindow", "BaseCanvas", "BaseCanvas", "HunTeamLogo", "HunTeamName", "HunTeamMajorPoint", "GameScoresHun"))
        {
            yield return key;
        }

        foreach (var key in Keys("ScoreSurWindow", "BaseCanvas", "BaseCanvas", "SurTeamLogo", "SurTeamName", "SurTeamMajorPoint", "GameScoresSur"))
        {
            yield return key;
        }

        foreach (var key in Keys(
                     "WidgetsWindow",
                     "MapBpCanvas",
                     "MapBpCanvas",
                     "PickedMap", "PickedMapName", "PickWord", "SurTeamName", "VS_Word", "HunTeamName",
                     "BannedMap", "BannedMapName", "BanWord"))
        {
            yield return key;
        }

        foreach (var key in Keys(
                     "WidgetsWindow",
                     "BpOverViewCanvas",
                     "BpOverViewCanvas",
                     "SurTeamLogo", "SurTeamNameInOverview", "HunTeamNameInOverview", "HunTeamLogo",
                     "HunBanCurrent0", "HunBanCurrent1", "HunBanCurrentLock0", "HunBanCurrentLock1",
                     "SurBanCurrent3", "SurBanCurrent2", "SurBanCurrent1", "SurBanCurrent0",
                     "SurBanCurrentLock0", "SurBanCurrentLock1", "SurBanCurrentLock2", "SurBanCurrentLock3",
                     "SurPick0", "SurPick1", "SurPick2", "SurPick3",
                     "GameProgress", "GameScoresSur", "RatioChar", "GameScoresHun", "HunPick"))
        {
            yield return key;
        }

        foreach (var key in Keys(
                     "WidgetsWindow",
                     "MapV2Canvas",
                     "MapV2Canvas",
                     "Arms_Factory", "The_Red_Church", "Sacred_Heart_Hospital", "Leo_s_Memory",
                     "Moonlit_River_Park", "Lakeside_Village", "Eversleeping_Town", "Chinatown", "Darkwoods"))
        {
            yield return key;
        }
    }

    private static IEnumerable<LegacyBlueprintAuditKey> Keys(string sourceWindow, string sourceCanvas, params string[] legacyNames)
    {
        foreach (var legacyName in legacyNames)
        {
            yield return new LegacyBlueprintAuditKey(sourceWindow, sourceCanvas, legacyName);
        }
    }

    private sealed record LegacyBlueprintAuditKey(string SourceWindow, string SourceCanvas, string LegacyName);

    private sealed record LegacyBlueprintAuditRow(
        string SourceWindow,
        string SourceCanvas,
        string LegacyName,
        string TargetWindow,
        string TargetName,
        string TargetControlType,
        string Binding,
        string StyleSource,
        string ResourceSource,
        string Status,
        string PropertyParityStatus,
        string Notes);

    private const string LegacyTextSettingsConfigJson =
        """
        {
          "BpWindowSettings": {
            "TextSettings": {
              "TeamName": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 16 },
              "Timer": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 30 }
            }
          },
          "CutSceneWindowSettings": {
            "TextSettings": {
              "TeamName": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 28 },
              "SurPlayerId": { "IsActive": true, "Color": "#FFFFFFFF", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 18 },
              "HunPlayerId": { "IsActive": true, "Color": "#FFFFFFFF", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 30 }
            }
          },
          "ScoreWindowSettings": {
            "TextSettings": {
              "GameScores": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 100 },
              "MajorPoints": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 38 },
              "TeamName": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 32 },
              "ScoreGlobal_TeamName": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 24 },
              "ScoreGlobal_Data": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 24 },
              "ScoreGlobal_Total": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 40 }
            }
          },
          "GameDataWindowSettings": {
            "TextSettings": {
              "TeamName": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 32 },
              "PlayerId": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 22 },
              "SurDataHeader": { "IsActive": true, "Color": "#FFFFFFFF", "FontFamilySite": "Noto Sans", "FontWeight": "Normal", "FontSize": 16 },
              "SurData": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 22 }
            }
          },
          "WidgetsWindowSettings": {
            "TextSettings": {
              "BpOverview_GameProgress": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 22 },
              "BpOverview_GameScores": { "IsActive": true, "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 20 },
              "MapBpV2_CampWords": { "IsActive": true, "Color": "#FF060606", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 20 }
            }
          }
        }
        """;

    private static void CreateLegacyArchive(
        string archivePath,
        string configJson,
        IReadOnlyList<string> customResources,
        IReadOnlyDictionary<string, string> layouts)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteZipEntry(archive, "Config.json", configJson);
        foreach (var resourceName in customResources)
        {
            var entry = archive.CreateEntry($"CustomUi/{resourceName}");
            using var stream = entry.Open();
            stream.Write(TinyPngBytes);
        }

        foreach (var (entryName, json) in layouts)
        {
            WriteZipEntry(archive, entryName, json);
        }
    }

    private static FrontedCanvasConfig ReadLayout(ZipArchive archive, string entryName)
    {
        return JsonSerializer.Deserialize<FrontedWindowConfig>(ReadZipEntry(archive, entryName), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!.ToCanvasConfig();
    }

    private static FrontedWindowConfig ReadWindowConfig(ZipArchive archive, string entryName)
    {
        return JsonSerializer.Deserialize<FrontedWindowConfig>(ReadZipEntry(archive, entryName), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private static void WriteFile(string path, string text)
    {
        if (TryMapLegacyBuiltInLayoutPath(path, out var mappedPath))
        {
            var canvasConfig = JsonSerializer.Deserialize<FrontedCanvasConfig>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
            Directory.CreateDirectory(Path.GetDirectoryName(mappedPath)!);
            File.WriteAllText(mappedPath, JsonSerializer.Serialize(neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedWindowConfigCanvasAdapter.FromCanvasConfig(canvasConfig)));
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string text)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(text);
    }

    private static string ReadZipEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException($"Missing zip entry {entryName}.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static bool TryMapLegacyBuiltInLayoutPath(string path, out string mappedPath)
    {
        mappedPath = path;
        var canvas = Path.GetFileNameWithoutExtension(path);
        var window = Path.GetFileName(Path.GetDirectoryName(path));
        var root = Path.GetDirectoryName(Path.GetDirectoryName(path));
        if (string.IsNullOrWhiteSpace(canvas) || string.IsNullOrWhiteSpace(window) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var outputWindow = (window, canvas) switch
        {
            ("WidgetsWindow", "BpOverViewCanvas") => "BpOverviewWindow",
            ("WidgetsWindow", "MapV2Canvas") => "MapV2Window",
            (_, "BaseCanvas") => window,
            _ => null
        };
        if (outputWindow is null)
        {
            return false;
        }

        mappedPath = Path.Combine(root, $"{outputWindow}.json");
        return true;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", Guid.NewGuid().ToString("N"));
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

    private static byte[] TinyPngBytes =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
}
