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

public sealed class LegacyFrontedLayoutConversionPolishTest
{
    private const string LegacyFont = "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简";
    private const string NotoSansFont = "pack://application:,,,/Assets/Fonts/#Noto Sans";

    [Theory]
    [InlineData("./#汉仪第五人格体简", "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简")]
    [InlineData("./#华康POP1体W5", "pack://application:,,,/Assets/Fonts/#华康POP1体W5")]
    [InlineData("pack://application:,,,/Assets/Fonts/#Noto Sans", "pack://application:,,,/Assets/Fonts/#Noto Sans")]
    [InlineData("Arial", "Arial")]
    public void LegacyFontFamilySiteIsNormalized(string value, string expected)
    {
        Assert.Equal(expected, LegacyFrontedTextStyleMigrator.NormalizeLegacyFontFamilySite(value));
    }

    [Fact]
    public void FormatterKeepsBenignDiagnosticsOutOfUserWarnings()
    {
        var result = new FrontedLayoutPackageLegacyConvertResult
        {
            Success = true,
            Infos =
            [
                "Legacy resource copied: CurrentBanLock.png",
                "Legacy global score cells aggregated: ScoreGlobalWindow/BaseCanvas/HomeTeamGame* -> HomeGlobalScoreRow.",
                "Legacy control geometry fuzzy-matched: A -> B"
            ],
            Diagnostics =
            [
                "Legacy overtime score cells were migrated into GlobalScoreRow child cells.",
                "Legacy lock overlay geometry consumed: HunBanCurrentLock0 -> HunBanCurrent0",
                "Legacy global score cells aggregated: ScoreGlobalWindow/BaseCanvas/HomeTeamGame* -> HomeGlobalScoreRow. Irregular cell spacing was approximated by median gaps."
            ]
        };

        Assert.False(LegacyConversionMessageFormatter.HasUserFacingWarnings(result));
        Assert.Equal(string.Empty, LegacyConversionMessageFormatter.BuildUserSummary(result));
        Assert.Contains("Legacy lock overlay geometry consumed", LegacyConversionMessageFormatter.BuildTechnicalDetails(result));
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
            Assert.Equal(380, sur.Left);
            Assert.Equal(42, sur.Top);
            Assert.Equal(120, sur.Width);
            Assert.Equal(36, sur.Height);
            Assert.Equal(971, hun.Left);
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
            Assert.Equal("Arial", sur.FontFamily);
            Assert.Equal("Arial", hun.FontFamily);
            Assert.Equal(28, sur.FontSize);
            Assert.Equal(28, hun.FontSize);
            Assert.Equal("Bold", sur.FontWeight);
            Assert.Equal("Bold", hun.FontWeight);
            Assert.Equal("#FFFFFFFF", sur.Color);
            Assert.Equal("#FFFFFFFF", hun.Color);
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
            Assert.Contains(result.Diagnostics, item => item.Contains("Legacy text style applied", StringComparison.Ordinal));

            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var bp = ReadLayout(archive, "FrontedLayouts/BpWindow.json");
            var surTeamName = Assert.IsType<TextFrontedControlConfig>(bp.Controls["SurTeamName"]);
            Assert.Equal("#FF000000", surTeamName.Color);
            Assert.Equal("Bold", surTeamName.FontWeight);
            Assert.Equal(LegacyFont, surTeamName.FontFamily);
            Assert.Equal(101, surTeamName.Left);
            var hunTeamName = Assert.IsType<TextFrontedControlConfig>(bp.Controls["HunTeamName"]);
            Assert.Equal("#FF000000", hunTeamName.Color);
            Assert.Equal(LegacyFont, hunTeamName.FontFamily);
            var timer = Assert.IsType<TextFrontedControlConfig>(bp.Controls["Timer"]);
            Assert.Equal("#FF000000", timer.Color);
            Assert.Equal(30, timer.FontSize);

            var cutScene = ReadLayout(archive, "FrontedLayouts/CutSceneWindow.json");
            var cutSurTeamName = Assert.IsType<TextFrontedControlConfig>(cutScene.Controls["SurTeamName"]);
            Assert.Equal("#FF000000", cutSurTeamName.Color);
            Assert.Equal(LegacyFont, cutSurTeamName.FontFamily);
            var cutHunTeamName = Assert.IsType<TextFrontedControlConfig>(cutScene.Controls["HunTeamName"]);
            Assert.Equal("#FF000000", cutHunTeamName.Color);
            Assert.Equal(LegacyFont, cutHunTeamName.FontFamily);

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
            AssertTextStyle(gameData.Controls["SurDataHeader0"], "#FFFFFFFF", "Normal", NotoSansFont, 16);
            AssertTextStyle(gameData.Controls["SurData0"], "#FF000000", "Bold", LegacyFont, 22);

            var overview = ReadLayout(archive, "FrontedLayouts/BpOverviewWindow.json");
            AssertTextStyle(overview.Controls["GameProgress"], "#FF000000", "Bold", LegacyFont, 22);
            AssertTextStyle(overview.Controls["GameScoresSur"], "#FF000000", "Bold", LegacyFont, 20);
            AssertTextStyle(overview.Controls["GameScoresHun"], "#FF000000", "Bold", LegacyFont, 20);

            var mapV2 = ReadLayout(archive, "FrontedLayouts/MapV2Window.json");
            var map = Assert.IsType<MapV2DisplayControlConfig>(mapV2.Controls["Arms_Factory"]);
            Assert.Equal("#FF060606", map.CampNameColor);
            Assert.Equal("Bold", map.CampNameFontWeight);
            Assert.Equal(LegacyFont, map.CampNameFontFamily);
            Assert.Equal(20, map.CampNameFontSize);
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

            Assert.StartsWith("bpui://converted.legacy.bo3-bg/resources/images/scoreGlobal-", layout.BackgroundImage);
            Assert.True(layout.EnableBoModeStates);
            Assert.StartsWith(
                "bpui://converted.legacy.bo3-bg/resources/images/scoreGlobalBo3-",
                layout.BoModeStates["Bo3"].BackgroundImage);
            Assert.Equal(2, archive.Entries.Count(entry => entry.FullName.StartsWith("resources/images/", StringComparison.Ordinal)));
            Assert.DoesNotContain(".0000000006", layoutJson, StringComparison.Ordinal);
            var row = Assert.IsType<GlobalScoreRowControlConfig>(layout.Controls["HomeGlobalScoreRow"]);
            Assert.Equal(100, row.Left);
            Assert.Equal(12.5, row.Top);
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
            Warnings =
            [
                "Legacy resource missing or not packaged for field BpWindowSettings.CurrentBanLockImageUri: C:\\legacy\\missing.png",
                "Legacy control geometry ignored because no v3 control matches: WidgetsWindow/BpOverViewCanvas/LegacyOnly. Closest candidates: A, B, C",
                "Unknown legacy layout file skipped: UnknownWindowConfig-BaseCanvas.json",
                "Converted layout BpWindow/BaseCanvas has validation errors: bad"
            ]
        };

        var summary = LegacyConversionMessageFormatter.BuildUserSummary(result);

        Assert.True(LegacyConversionMessageFormatter.HasUserFacingWarnings(result));
        Assert.DoesNotContain("Closest candidates", summary, StringComparison.OrdinalIgnoreCase);
        Assert.True(summary.Split(Environment.NewLine).Count(line => line.StartsWith("- ", StringComparison.Ordinal)) <= 3);
        Assert.Contains("Closest candidates", LegacyConversionMessageFormatter.BuildTechnicalDetails(result));
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
            Assert.Contains(result.Diagnostics, item => item.Contains("Legacy overtime score cells were migrated", StringComparison.Ordinal));
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
            Assert.Contains(result.Diagnostics, item => item.Contains("HunBanCurrentLock0 -> HunBanCurrent0", StringComparison.Ordinal));
            Assert.False(LegacyConversionMessageFormatter.HasUserFacingWarnings(result));

            using var archive = ZipFile.OpenRead(result.ConvertedPackagePath!);
            var layout = ReadLayout(archive, "FrontedLayouts/BpOverviewWindow.json");
            var hun = Assert.IsType<ImageFrontedControlConfig>(layout.Controls["HunBanCurrent0"]);
            Assert.True(hun.Lockable);
            Assert.Contains(result.Diagnostics, item => item.Contains("separate geometry is not representable", StringComparison.Ordinal));
            var sur = Assert.IsType<ImageFrontedControlConfig>(layout.Controls["SurBanCurrent0"]);
            Assert.Equal(100, sur.Left);
            Assert.Equal(20, sur.Top);
            Assert.Equal(30, sur.Width);
            Assert.Equal(40, sur.Height);
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
            Assert.Contains(result.Warnings, warning => warning.Contains("MapBpV1", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, warning => warning.Contains("BpOverViewCanvas content exceeds", StringComparison.Ordinal));

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
            Assert.Equal(1132, overview.WindowSettings.WindowWidth);
            Assert.Equal(182, overview.WindowSettings.WindowHeight);
            Assert.Equal(1132, overview.CanvasSettings.CanvasWidth);
            Assert.Equal(182, overview.CanvasSettings.CanvasHeight);
            Assert.Equal("#00FF00", overview.WindowSettings.BackgroundColor);
            Assert.False(overview.WindowSettings.AllowsTransparency);
            Assert.StartsWith("bpui://converted.legacy.widgets/resources/images/overview-", overview.CanvasSettings.BackgroundImage);
            Assert.Equal(11, overview.ControlLayout.Controls["HunBanCurrent0"].Left);

            var mapV2 = ReadWindowConfig(archive, "FrontedLayouts/MapV2Window.json");
            Assert.Equal(1440, mapV2.WindowSettings.WindowWidth);
            Assert.Equal(160, mapV2.WindowSettings.WindowHeight);
            Assert.Equal(1440, mapV2.CanvasSettings.CanvasWidth);
            Assert.Equal(160, mapV2.CanvasSettings.CanvasHeight);
            Assert.StartsWith("bpui://converted.legacy.widgets/resources/images/mapv2-", mapV2.CanvasSettings.BackgroundImage);
            var display = Assert.IsType<MapV2DisplayControlConfig>(mapV2.ControlLayout.Controls["Arms_Factory"]);
            Assert.Equal(10, display.Left);
            Assert.Equal(20, display.Top);
            Assert.StartsWith("bpui://converted.legacy.widgets/resources/images/border-", display.PickingBorderImagePath);
            Assert.Equal("#FF445566", display.PickingBorderFillColor);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task ConverterRespectsLegacyTextSettingsIsActive()
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
            Assert.Equal("#FFFFFFFF", teamName.Color);
            Assert.Equal("Noto Sans", teamName.FontFamily);
            var timer = Assert.IsType<TextFrontedControlConfig>(layout.Controls["Timer"]);
            Assert.Equal("#FF000000", timer.Color);
            Assert.Equal("Bold", timer.FontWeight);
            Assert.Equal(30, timer.FontSize);
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
        Assert.Equal(color, text.Color);
        Assert.Equal(fontWeight, text.FontWeight);
        Assert.Equal(fontFamily, text.FontFamily);
        Assert.Equal(fontSize, text.FontSize);
    }

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
            File.WriteAllText(mappedPath, JsonSerializer.Serialize(FrontedWindowConfig.FromCanvasConfig(canvasConfig)));
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
