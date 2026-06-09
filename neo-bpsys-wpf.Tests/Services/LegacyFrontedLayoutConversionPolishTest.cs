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
            var bp = ReadLayout(archive, "layouts/BpWindow/BaseCanvas.json");
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

            var cutScene = ReadLayout(archive, "layouts/CutSceneWindow/BaseCanvas.json");
            var cutSurTeamName = Assert.IsType<TextFrontedControlConfig>(cutScene.Controls["SurTeamName"]);
            Assert.Equal("#FF000000", cutSurTeamName.Color);
            Assert.Equal(LegacyFont, cutSurTeamName.FontFamily);
            var cutHunTeamName = Assert.IsType<TextFrontedControlConfig>(cutScene.Controls["HunTeamName"]);
            Assert.Equal("#FF000000", cutHunTeamName.Color);
            Assert.Equal(LegacyFont, cutHunTeamName.FontFamily);

            var scoreSur = ReadLayout(archive, "layouts/ScoreSurWindow/BaseCanvas.json");
            AssertTextStyle(scoreSur.Controls["GameScoresSur"], "#FF000000", "Bold", LegacyFont, 100);
            var scoreHun = ReadLayout(archive, "layouts/ScoreHunWindow/BaseCanvas.json");
            AssertTextStyle(scoreHun.Controls["GameScoresHun"], "#FF000000", "Bold", LegacyFont, 100);

            var scoreGlobal = ReadLayout(archive, "layouts/ScoreGlobalWindow/BaseCanvas.json");
            AssertTextStyle(scoreGlobal.Controls["HomeTeamName"], "#FF000000", "Bold", LegacyFont, 24);
            AssertTextStyle(scoreGlobal.Controls["AwayTeamName"], "#FF000000", "Bold", LegacyFont, 24);
            AssertTextStyle(scoreGlobal.Controls["HomeScoreTotal"], "#FF000000", "Bold", LegacyFont, 40);
            AssertTextStyle(scoreGlobal.Controls["AwayScoreTotal"], "#FF000000", "Bold", LegacyFont, 40);
            AssertTextStyle(scoreGlobal.Controls["HomeGlobalScoreRow"], "#FF000000", "Bold", LegacyFont, 24);
            AssertTextStyle(scoreGlobal.Controls["AwayGlobalScoreRow"], "#FF000000", "Bold", LegacyFont, 24);

            var gameData = ReadLayout(archive, "layouts/GameDataWindow/BaseCanvas.json");
            AssertTextStyle(gameData.Controls["SurId0"], "#FF000000", "Bold", LegacyFont, 22);
            AssertTextStyle(gameData.Controls["SurDataHeader0"], "#FFFFFFFF", "Normal", NotoSansFont, 16);
            AssertTextStyle(gameData.Controls["SurData0"], "#FF000000", "Bold", LegacyFont, 22);

            var overview = ReadLayout(archive, "layouts/WidgetsWindow/BpOverViewCanvas.json");
            AssertTextStyle(overview.Controls["GameProgress"], "#FF000000", "Bold", LegacyFont, 22);
            AssertTextStyle(overview.Controls["GameScoresSur"], "#FF000000", "Bold", LegacyFont, 20);
            AssertTextStyle(overview.Controls["GameScoresHun"], "#FF000000", "Bold", LegacyFont, 20);

            var mapV2 = ReadLayout(archive, "layouts/WidgetsWindow/MapV2Canvas.json");
            var map = Assert.IsType<MapV2DisplayControlConfig>(mapV2.Controls["MapV2Display0"]);
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
            var layoutJson = ReadZipEntry(archive, "layouts/ScoreGlobalWindow/BaseCanvas.json");
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
                          "SurBanCurrent0": { "Left": 100, "Top": 200, "Width": 300, "Height": 400 },
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
            var layout = ReadLayout(archive, "layouts/WidgetsWindow/BpOverViewCanvas.json");
            var hun = Assert.IsType<ImageFrontedControlConfig>(layout.Controls["HunBanCurrent0"]);
            Assert.Equal(11, hun.Left);
            Assert.Equal(22, hun.Top);
            Assert.Equal(33, hun.Width);
            Assert.Equal(44, hun.Height);
            var sur = Assert.IsType<ImageFrontedControlConfig>(layout.Controls["SurBanCurrent0"]);
            Assert.Equal(100, sur.Left);
            Assert.Equal(200, sur.Top);
            Assert.Equal(300, sur.Width);
            Assert.Equal(400, sur.Height);
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
            var bpLayout = ReadLayout(archive, "layouts/BpWindow/BaseCanvas.json");
            var current = Assert.IsType<ImageFrontedControlConfig>(bpLayout.Controls["SurBanCurrent0"]);
            var global = Assert.IsType<ImageFrontedControlConfig>(bpLayout.Controls["SurBanGlobal0"]);
            var pick = Assert.IsType<ImageFrontedControlConfig>(bpLayout.Controls["SurPick0"]);
            Assert.StartsWith("bpui://converted.legacy.assets/resources/images/CurrentBanLock-", current.LockImagePath);
            Assert.StartsWith("bpui://converted.legacy.assets/resources/images/GlobalBanLock-", global.LockImagePath);
            Assert.StartsWith("bpui://converted.legacy.assets/resources/images/PickingBorder-", pick.PickingBorderImagePath);

            var widgetsLayout = ReadLayout(archive, "layouts/WidgetsWindow/BpOverViewCanvas.json");
            var widgetsCurrent = Assert.IsType<ImageFrontedControlConfig>(widgetsLayout.Controls["HunBanCurrent0"]);
            Assert.StartsWith("bpui://converted.legacy.assets/resources/images/WidgetCurrentBanLock-", widgetsCurrent.LockImagePath);
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
            Assert.Contains(result.Warnings, warning => warning.Contains("MissingLock.png", StringComparison.Ordinal));
            Assert.True(LegacyConversionMessageFormatter.HasUserFacingWarnings(result));
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
              "SurBanGlobal0": {
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
              "MapV2Display0": { "ControlType": "MapV2Display", "Left": 1, "Top": 1, "Width": 100, "Height": 100, "MapKey": "arms" }
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
              "TeamName": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 16 },
              "Timer": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 30 }
            }
          },
          "CutSceneWindowSettings": {
            "TextSettings": {
              "TeamName": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 28 },
              "SurPlayerId": { "Color": "#FFFFFFFF", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 18 },
              "HunPlayerId": { "Color": "#FFFFFFFF", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 30 }
            }
          },
          "ScoreWindowSettings": {
            "TextSettings": {
              "GameScores": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 100 },
              "MajorPoints": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 38 },
              "TeamName": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 32 },
              "ScoreGlobal_TeamName": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 24 },
              "ScoreGlobal_Data": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 24 },
              "ScoreGlobal_Total": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 40 }
            }
          },
          "GameDataWindowSettings": {
            "TextSettings": {
              "TeamName": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 32 },
              "PlayerId": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 22 },
              "SurDataHeader": { "Color": "#FFFFFFFF", "FontFamilySite": "Noto Sans", "FontWeight": "Normal", "FontSize": 16 },
              "SurData": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 22 }
            }
          },
          "WidgetsWindowSettings": {
            "TextSettings": {
              "BpOverview_GameProgress": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 22 },
              "BpOverview_GameScores": { "Color": "#FF000000", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 20 },
              "MapBpV2_CampWords": { "Color": "#FF060606", "FontFamilySite": "./#汉仪第五人格体简", "FontWeight": "Bold", "FontSize": 20 }
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
        entryName = MapLegacyPackageLayoutEntry(entryName);
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

    private static string MapLegacyPackageLayoutEntry(string entryName)
    {
        var normalized = entryName.Replace('\\', '/');
        if (!normalized.StartsWith("layouts/", StringComparison.OrdinalIgnoreCase)
            || !normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return entryName;
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            return entryName;
        }

        var window = parts[1];
        var canvas = Path.GetFileNameWithoutExtension(parts[2]);
        var outputWindow = (window, canvas) switch
        {
            ("WidgetsWindow", "BpOverViewCanvas") => "BpOverviewWindow",
            ("WidgetsWindow", "MapV2Canvas") => "MapV2Window",
            (_, "BaseCanvas") => window,
            _ => null
        };

        return outputWindow is null ? entryName : $"FrontedLayouts/{outputWindow}.json";
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
