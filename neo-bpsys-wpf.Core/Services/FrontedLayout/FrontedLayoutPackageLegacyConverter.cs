#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Converters;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.Legacy;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class FrontedLayoutPackageLegacyConverter : IFrontedLayoutPackageLegacyConverter
{
    private const string ManifestFileName = "manifest.json";
    private const string DefaultOpaqueBackgroundColor = "#FF00FF00";

    private static readonly Regex SafeFileNameChars = new("[^A-Za-z0-9._-]+", RegexOptions.Compiled);

    private static readonly Regex LegacyScoreGlobalCellName = new(
        @"^(Home|Away)TeamGame(?<game>\d+)(?<overtime>Overtime)?(?<half>FirstHalf|SecondHalf)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LegacyCurrentBanLockOverlayName = new(
        @"^(Hun|Sur)BanCurrentLock(?<index>\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LegacyGlobalBanLockOverlayName = new(
        @"^(Hun|Sur)GlobalBanLock(?<index>\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif",
        ".webp",
        ".ico",
        ".tif",
        ".tiff",
        ".svg"
    };

    private static readonly Dictionary<string, LegacyLayoutMapping> LegacyLayoutFileMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["BpWindowConfig-BaseCanvas.json"] = new("BpWindow", "BaseCanvas", "BpWindow"),
            ["CutSceneWindowConfig-BaseCanvas.json"] = new("CutSceneWindow", "BaseCanvas", "CutSceneWindow"),
            ["GameDataWindowConfig-BaseCanvas.json"] = new("GameDataWindow", "BaseCanvas", "GameDataWindow"),
            ["ScoreSurWindowConfig-BaseCanvas.json"] = new("ScoreSurWindow", "BaseCanvas", "ScoreSurWindow"),
            ["ScoreHunWindowConfig-BaseCanvas.json"] = new("ScoreHunWindow", "BaseCanvas", "ScoreHunWindow"),
            ["ScoreGlobalWindowConfig-BaseCanvas.json"] = new("ScoreGlobalWindow", "BaseCanvas", "ScoreGlobalWindow"),
            ["WidgetsWindowConfig-MapBpCanvas.json"] = new("WidgetsWindow", "MapBpCanvas", null, 308D, 554D),
            ["WidgetsWindowConfig-BpOverViewCanvas.json"] = new("WidgetsWindow", "BpOverViewCanvas", "BpOverviewWindow", 1132D, 182D),
            ["WidgetsWindowConfig-MapV2Canvas.json"] = new("WidgetsWindow", "MapV2Canvas", "MapV2Window", 1440D, 160D)
        };

    private static readonly IReadOnlyDictionary<LegacyLayoutKey, IReadOnlyList<LegacyControlBlueprint>> LegacyControlBlueprints =
        CreateLegacyControlBlueprints();

    private readonly string _tempRoot;
    private readonly IFrontedLayoutPackageImporter? _packageImporter;
    private readonly FrontedLayoutValidator _validator;
    private readonly ILogger<FrontedLayoutPackageLegacyConverter> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth,
        Converters = { new FontWeightJsonConverter() }
    };

    public FrontedLayoutPackageLegacyConverter(
        IFrontedLayoutPackageImporter packageImporter,
        FrontedLayoutValidator validator,
        ILogger<FrontedLayoutPackageLegacyConverter> logger)
        : this(
            Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts"),
            Path.Combine(AppConstants.AppTempPath, "bpui-legacy-convert"),
            packageImporter,
            validator,
            logger)
    {
    }

    public FrontedLayoutPackageLegacyConverter(
        string builtInLayoutRoot,
        string tempRoot,
        IFrontedLayoutPackageImporter? packageImporter = null,
        FrontedLayoutValidator? validator = null,
        ILogger<FrontedLayoutPackageLegacyConverter>? logger = null)
    {
        _tempRoot = tempRoot;
        _packageImporter = packageImporter;
        _validator = validator ?? new FrontedLayoutValidator();
        _logger = logger ?? NullLogger<FrontedLayoutPackageLegacyConverter>.Instance;
    }

    public async Task<FrontedLayoutPackageLegacyConvertResult> ConvertAsync(
        FrontedLayoutPackageLegacyConvertRequest request,
        CancellationToken cancellationToken = default)
    {
        var infos = new List<string>();
        var diagnostics = new List<string>();
        var warnings = new List<string>();
        var extractionRoot = Path.Combine(_tempRoot, "extract", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(_tempRoot, "staging", Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(_tempRoot, "converted", $"{Guid.NewGuid():N}.bpui");

        try
        {
            if (string.IsNullOrWhiteSpace(request.LegacyPackagePath) || !File.Exists(request.LegacyPackagePath))
            {
                return Fail("Legacy package archive was not found.", infos, diagnostics, warnings);
            }

            var packageId = string.IsNullOrWhiteSpace(request.PackageId)
                ? $"converted.legacy.{DateTime.UtcNow:yyyyMMddHHmm}"
                : request.PackageId.Trim();
            if (!FrontedLayoutPackageManager.IsSafePackageId(packageId)
                || string.Equals(packageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(packageId, FrontedLayoutPackageManager.LocalPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("PackageId is invalid.", infos, diagnostics, warnings);
            }

            Directory.CreateDirectory(extractionRoot);
            Directory.CreateDirectory(stagingRoot);
            ExtractZipSafely(request.LegacyPackagePath, extractionRoot);
            if (!DetectLegacyPackage(extractionRoot))
            {
                return Fail("Archive is not a legacy .bpui package.", infos, diagnostics, warnings);
            }

            var resourceState = CopyCustomUiResources(extractionRoot, stagingRoot, packageId, infos);
            var manifest = CreateManifest(request, packageId);
            manifest.Content.Resources = resourceState.Resources;

            var configValueMap = ReadFrontendConfigValueMap(extractionRoot, resourceState, diagnostics, warnings);
            var legacyPropertySet = ReadLegacyPropertySet(extractionRoot, diagnostics, warnings);
            var legacySettings = ReadLegacySettings(extractionRoot, diagnostics, warnings);
            var layoutEntries = await ConvertFrontElementsConfigsAsync(
                extractionRoot,
                stagingRoot,
                manifest,
                resourceState,
                configValueMap,
                legacySettings,
                legacyPropertySet,
                infos,
                diagnostics,
                warnings,
                cancellationToken);
            if (layoutEntries == 0)
            {
                return Fail("No mappable legacy FrontElementsConfig files were converted.", infos, diagnostics, warnings);
            }

            var manifestJson = JsonSerializer.Serialize(manifest, _jsonOptions);
            await File.WriteAllTextAsync(Path.Combine(stagingRoot, ManifestFileName), manifestJson, cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            ZipFile.CreateFromDirectory(stagingRoot, outputPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            EnsureZipEntriesAreSafe(outputPath);

            var result = new FrontedLayoutPackageLegacyConvertResult
            {
                Success = true,
                ConvertedPackagePath = outputPath,
                LayoutCount = manifest.Content.Layouts.Count,
                ResourceCount = manifest.Content.Resources.Count,
                Infos = infos.ToArray(),
                Diagnostics = diagnostics.ToArray(),
                Warnings = warnings.ToArray()
            };

            if (request.InstallAfterConvert && _packageImporter is not null)
            {
                var importResult = await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
                {
                    PackagePath = outputPath,
                    ActivateAfterImport = request.ActivateAfterInstall
                }, cancellationToken);

                result.Success = importResult.Success;
                result.InstalledPackageId = importResult.Success ? importResult.PackageId : null;
                result.ErrorMessage = importResult.ErrorMessage;
            }

            return result;
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid legacy bpui archive.");
            return Fail($"Invalid legacy package archive: {ex.Message}", infos, diagnostics, warnings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert legacy bpui package.");
            return Fail(ex.Message, infos, diagnostics, warnings);
        }
        finally
        {
            TryDeleteDirectory(extractionRoot);
            TryDeleteDirectory(stagingRoot);
        }
    }

    private async Task<int> ConvertFrontElementsConfigsAsync(
        string extractionRoot,
        string stagingRoot,
        FrontedLayoutPackageManifest manifest,
        ResourceConvertState resourceState,
        IReadOnlyDictionary<string, string> configValueMap,
        LegacySettings? legacySettings,
        IReadOnlySet<string> legacyPropertySet,
        ICollection<string> infos,
        ICollection<string> diagnostics,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        var convertedCount = 0;
        var frontElementsRoot = Path.Combine(extractionRoot, "FrontElementsConfig");
        if (!Directory.Exists(frontElementsRoot))
        {
            warnings.Add("FrontElementsConfig folder is missing.");
            return 0;
        }

        foreach (var file in Directory.EnumerateFiles(frontElementsRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(file);
            if (!TryMapLegacyLayoutFile(fileName, out var mapping))
            {
                warnings.Add($"Unknown legacy layout file skipped: {fileName}");
                continue;
            }

            if (!mapping.IsSupported)
            {
                warnings.Add("Legacy MapBpCanvas / MapBpV1 is not supported by Designer v3 converter and was skipped.");
                continue;
            }

            var legacyPositions = ReadLegacyPositions(file, warnings);
            if (legacyPositions is null)
            {
                continue;
            }

            var windowConfig = CreateLegacyWindowConfig(mapping);
            var config = windowConfig.ToCanvasConfig();
            BuildLegacyBlueprintControls(
                mapping,
                config,
                legacyPositions,
                infos,
                diagnostics,
                warnings);
            ApplyFrontendConfigValues(config, mapping, configValueMap, infos);
            if (legacySettings is not null)
            {
                LegacyFrontedTextStyleMigrator.Apply(
                    config,
                    mapping.SourceWindow,
                    mapping.SourceCanvas,
                    legacySettings,
                    diagnostics);
            }

            RewriteKnownResourceStrings(config, resourceState);
            config.Version = 3;
            ApplyCanvasConfig(windowConfig, config);
            ApplyLegacyWindowSettings(windowConfig, mapping, legacySettings, legacyPropertySet, diagnostics);

            var validationMessages = _validator.Validate(
                mapping.TargetWindow!,
                FrontedLayoutConstants.BaseCanvasName,
                windowConfig.ToCanvasConfig());
            var validationErrors = validationMessages
                .Where(message => message.Severity == Models.FrontedLayout.Designer.FrontedLayoutValidationSeverity.Error)
                .ToArray();
            if (validationErrors.Length > 0)
            {
                warnings.Add($"Converted layout {mapping.TargetWindow}/{FrontedLayoutConstants.BaseCanvasName} has validation errors: {string.Join("; ", validationErrors.Select(error => error.Message))}");
                continue;
            }

            var relativePath = mapping.TargetLayoutPath;
            var targetPath = Path.Combine(stagingRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var json = JsonSerializer.Serialize(windowConfig, _jsonOptions);
            await File.WriteAllTextAsync(targetPath, json, cancellationToken);

            manifest.Content.Layouts.Add(new FrontedLayoutPackageLayoutEntry
            {
                Window = mapping.TargetWindow!,
                Path = relativePath
            });
            convertedCount++;
        }

        return convertedCount;
    }

    private static FrontedWindowConfig CreateLegacyWindowConfig(LegacyLayoutMapping mapping)
    {
        var defaults = GetLegacyWindowDefaults(mapping);
        return new FrontedWindowConfig
        {
            Version = 3,
            WindowSettings = new FrontedWindowSettings
            {
                WindowWidth = defaults.WindowWidth,
                WindowHeight = defaults.WindowHeight,
                AllowsTransparency = true,
                BackgroundColor = "#00000000",
                Topmost = false,
                ViewboxStretch = Stretch.Fill
            },
            CanvasSettings = new FrontedCanvasSettings
            {
                CanvasWidth = defaults.CanvasWidth,
                CanvasHeight = defaults.CanvasHeight,
                BackgroundImage = defaults.BackgroundImage,
                EnableBoModeStates = false,
                BoModeStates = []
            },
            ControlLayout = new FrontedControlLayout
            {
                RequiredPlugins = [],
                Controls = []
            }
        };
    }

    private static Dictionary<string, ElementInfo>? ReadLegacyPositions(
        string legacyFile,
        ICollection<string> warnings)
    {
        try
        {
            if (new FileInfo(legacyFile).Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                warnings.Add($"Legacy layout file is too large and was skipped: {Path.GetFileName(legacyFile)}");
                return null;
            }

            return JsonSerializer.Deserialize<Dictionary<string, ElementInfo>>(
                File.ReadAllText(legacyFile),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    MaxDepth = FrontedLayoutLimits.MaxJsonDepth
                });
        }
        catch (Exception ex)
        {
            warnings.Add($"Legacy layout file could not be read and was skipped: {Path.GetFileName(legacyFile)}; {ex.Message}");
            return null;
        }
    }

    private void BuildLegacyBlueprintControls(
        LegacyLayoutMapping mapping,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ICollection<string> infos,
        ICollection<string> diagnostics,
        ICollection<string> warnings)
    {
        if (legacyPositions is null)
        {
            return;
        }

        var key = new LegacyLayoutKey(mapping.SourceWindow, mapping.SourceCanvas);
        if (!LegacyControlBlueprints.TryGetValue(key, out var blueprints))
        {
            warnings.Add($"No legacy control blueprint exists for {mapping.SourceWindow}/{mapping.SourceCanvas}; layout was skipped.");
            return;
        }

        foreach (var blueprint in blueprints)
        {
            if (!blueprint.Required && !legacyPositions.ContainsKey(blueprint.LegacyName))
            {
                continue;
            }

            var control = CreateBlueprintControl(blueprint);
            if (control is null)
            {
                warnings.Add($"Legacy control blueprint could not create target control: {mapping.SourceWindow}/{mapping.SourceCanvas}/{blueprint.LegacyName} -> {blueprint.TargetName}");
                continue;
            }

            config.Controls[blueprint.TargetName] = control;
        }

        var consumedControls = new HashSet<string>(StringComparer.Ordinal);
        ApplyScoreGlobalAggregateGeometry(mapping.SourceWindow, mapping.SourceCanvas, config, legacyPositions, consumedControls, infos, diagnostics);
        ConsumeLegacyLockOverlayGeometry(mapping.SourceWindow, mapping.SourceCanvas, config, legacyPositions, consumedControls, diagnostics);
        var blueprintsByLegacyName = blueprints
            .GroupBy(blueprint => blueprint.LegacyName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        foreach (var (controlName, legacy) in legacyPositions)
        {
            if (consumedControls.Contains(controlName))
            {
                continue;
            }

            if (!blueprintsByLegacyName.TryGetValue(controlName, out var mappedBlueprints))
            {
                var candidates = LegacyFrontedControlNameMapper.GetClosestCandidates(controlName, config.Controls.Keys);
                warnings.Add(candidates.Count > 0
                    ? $"Legacy control geometry ignored because no legacy blueprint maps it: {mapping.SourceWindow}/{mapping.SourceCanvas}/{controlName}. Closest candidates: {string.Join(", ", candidates)}"
                    : $"Legacy control geometry ignored because no legacy blueprint maps it: {mapping.SourceWindow}/{mapping.SourceCanvas}/{controlName}");
                continue;
            }

            foreach (var blueprint in mappedBlueprints)
            {
                if (config.Controls.TryGetValue(blueprint.TargetName, out var control))
                {
                    ApplyGeometry(control, legacy);
                }
            }
        }

        AddBoundsDiagnostics(mapping, legacyPositions.Values, warnings);
    }

    private FrontedControlConfigBase? CreateBlueprintControl(
        LegacyControlBlueprint blueprint)
    {
        var created = CreateDefaultControl(blueprint);
        if (created is null)
        {
            return null;
        }

        ApplyBlueprintDefaults(blueprint, created);
        created.Visibility = FrontedControlVisibility.Visible;
        return created;
    }

    private static void ApplyScoreGlobalAggregateGeometry(
        string window,
        string canvas,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ISet<string> consumedControls,
        ICollection<string> infos,
        ICollection<string> diagnostics)
    {
        if (!string.Equals(window, "ScoreGlobalWindow", StringComparison.Ordinal)
            || !string.Equals(canvas, "BaseCanvas", StringComparison.Ordinal))
        {
            return;
        }

        ApplyScoreGlobalRowGeometry(
            "Home",
            "HomeGlobalScoreRow",
            config,
            legacyPositions,
            consumedControls,
            infos,
            diagnostics);
        ApplyScoreGlobalRowGeometry(
            "Away",
            "AwayGlobalScoreRow",
            config,
            legacyPositions,
            consumedControls,
            infos,
            diagnostics);
    }

    private static void ApplyScoreGlobalRowGeometry(
        string teamPrefix,
        string targetControlName,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ISet<string> consumedControls,
        ICollection<string> infos,
        ICollection<string> diagnostics)
    {
        if (!config.Controls.TryGetValue(targetControlName, out var control)
            || control is not GlobalScoreRowControlConfig row)
        {
            return;
        }

        var cells = legacyPositions
            .Select(item => TryParseScoreGlobalCell(item.Key, out var team, out var game, out var half, out var isOvertime)
                ? new ScoreGlobalCell(item.Key, team, game, half, isOvertime, item.Value)
                : null)
            .Where(cell => cell is not null
                           && string.Equals(cell.Team, teamPrefix, StringComparison.Ordinal))
            .Cast<ScoreGlobalCell>()
            .ToArray();
        if (cells.Length == 0)
        {
            return;
        }

        foreach (var cell in cells)
        {
            consumedControls.Add(cell.ControlName);
        }

        if (cells.Any(cell => cell.IsOvertime))
        {
            diagnostics.Add(
                "Legacy overtime score cells were migrated into GlobalScoreRow child cells.");
        }

        var firstHalfByGame = cells
            .Where(cell => !cell.IsOvertime && cell.Half == "FirstHalf" && cell.Info.Left.HasValue)
            .GroupBy(cell => cell.Game)
            .ToDictionary(group => group.Key, group => group.First().Info);
        var secondHalfByGame = cells
            .Where(cell => !cell.IsOvertime && cell.Half == "SecondHalf" && cell.Info.Left.HasValue)
            .GroupBy(cell => cell.Game)
            .ToDictionary(group => group.Key, group => group.First().Info);

        var gameOneFirstHalf = firstHalfByGame.GetValueOrDefault(1);
        var left = gameOneFirstHalf?.Left
                   ?? cells.Select(cell => cell.Info.Left).Where(value => value.HasValue).Min();
        if (left.HasValue)
        {
            row.Left = FrontedLayoutNumberNormalizer.Normalize(left.Value);
        }

        var top = gameOneFirstHalf?.Top
                  ?? GetMedian(cells.Select(cell => cell.Info.Top).Where(value => value.HasValue).Select(value => value!.Value))
                  ?? cells.Select(cell => cell.Info.Top).FirstOrDefault(value => value.HasValue);
        if (top.HasValue)
        {
            row.Top = FrontedLayoutNumberNormalizer.Normalize(top.Value);
        }

        var halfGaps = firstHalfByGame
            .Where(item => secondHalfByGame.TryGetValue(item.Key, out var secondHalf)
                           && item.Value.Left.HasValue
                           && secondHalf.Left.HasValue)
            .Select(item => secondHalfByGame[item.Key].Left!.Value - item.Value.Left!.Value)
            .Where(gap => gap > 0)
            .ToArray();
        var halfGap = GetMedian(halfGaps);
        if (halfGap.HasValue)
        {
#pragma warning disable CS0618
            row.HalfGameGap = FrontedLayoutNumberNormalizer.Normalize(halfGap.Value);
#pragma warning restore CS0618
        }

        var gameLefts = firstHalfByGame
            .Where(item => item.Value.Left.HasValue)
            .OrderBy(item => item.Key)
            .Select(item => new { Game = item.Key, Left = item.Value.Left!.Value })
            .ToArray();
        var majorGaps = gameLefts
            .Zip(gameLefts.Skip(1), (previous, next) => next.Game == previous.Game + 1
                ? next.Left - previous.Left
                : (double?)null)
            .Where(gap => gap.HasValue && gap.Value > 0)
            .Select(gap => gap!.Value)
            .ToArray();
        var majorGap = GetMedian(majorGaps);
        if (majorGap.HasValue)
        {
#pragma warning disable CS0618
            row.MajorGameGap = FrontedLayoutNumberNormalizer.Normalize(majorGap.Value);
#pragma warning restore CS0618
        }

        MigrateLegacyScoreCellsToRowCells(row, cells);

        var approximate = IsIrregular(halfGaps) || IsIrregular(majorGaps);
        var message =
            $"Legacy global score cells aggregated: ScoreGlobalWindow/BaseCanvas/{teamPrefix}TeamGame* -> {targetControlName}.";
        if (approximate)
        {
            diagnostics.Add(message + " Irregular cell spacing was approximated by median gaps.");
        }
        else
        {
            infos.Add(message);
        }
    }

    private static void MigrateLegacyScoreCellsToRowCells(
        GlobalScoreRowControlConfig row,
        IReadOnlyList<ScoreGlobalCell> legacyCells)
    {
        var existingCells = row.Cells.ToDictionary(
            cell => (cell.GameNumber, cell.GameKind, cell.HalfKind));
        var migrated = new List<GlobalScoreCellConfig>();

        foreach (var legacy in legacyCells
                     .Where(cell => cell.Info.Left.HasValue || cell.Info.Top.HasValue || cell.Info.Width.HasValue || cell.Info.Height.HasValue)
                     .OrderBy(cell => cell.Game)
                     .ThenBy(cell => cell.IsOvertime)
                     .ThenBy(cell => cell.Half == "SecondHalf" ? 1 : 0))
        {
            var gameKind = legacy.IsOvertime ? ScoreGameKind.Overtime : ScoreGameKind.Normal;
            var halfKind = legacy.Half == "SecondHalf" ? ScoreHalfKind.SecondHalf : ScoreHalfKind.FirstHalf;
            var key = (legacy.Game, gameKind, halfKind);
            if (!existingCells.TryGetValue(key, out var cell))
            {
                cell = new GlobalScoreCellConfig
                {
                    Id = $"Game{legacy.Game}{(legacy.IsOvertime ? "Overtime" : string.Empty)}{halfKind}",
                    GameNumber = legacy.Game,
                    GameKind = gameKind,
                    HalfKind = halfKind,
                    Width = 75,
                    Height = 32
                };
            }

            if (legacy.Info.Left.HasValue)
            {
                cell.X = FrontedLayoutNumberNormalizer.Normalize(legacy.Info.Left.Value - row.Left);
            }

            if (legacy.Info.Top.HasValue)
            {
                cell.Y = FrontedLayoutNumberNormalizer.Normalize(legacy.Info.Top.Value - row.Top);
            }

            if (legacy.Info.Width.HasValue)
            {
                cell.Width = FrontedLayoutNumberNormalizer.Normalize(legacy.Info.Width.Value);
            }

            if (legacy.Info.Height.HasValue)
            {
                cell.Height = FrontedLayoutNumberNormalizer.Normalize(legacy.Info.Height.Value);
            }

            migrated.Add(cell);
        }

        if (migrated.Count > 0)
        {
            row.Cells = migrated;
            row.Width = Math.Max(row.Width ?? 0D, migrated.Max(cell => cell.X + cell.Width));
            row.Height = Math.Max(row.Height ?? 0D, migrated.Max(cell => cell.Y + cell.Height));
        }
    }

    private static bool TryParseScoreGlobalCell(
        string controlName,
        out string team,
        out int game,
        out string half,
        out bool isOvertime)
    {
        team = string.Empty;
        game = 0;
        half = string.Empty;
        isOvertime = false;
        var match = LegacyScoreGlobalCellName.Match(controlName);
        if (!match.Success)
        {
            return false;
        }

        team = match.Groups[1].Value;
        game = int.Parse(match.Groups["game"].Value);
        half = match.Groups["half"].Value;
        isOvertime = match.Groups["overtime"].Success;
        return true;
    }

    private static void ConsumeLegacyLockOverlayGeometry(
        string window,
        string canvas,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ISet<string> consumedControls,
        ICollection<string> diagnostics)
    {
        foreach (var (legacyName, legacy) in legacyPositions)
        {
            if (!TryMapLegacyLockOverlayName(legacyName, out var targetName))
            {
                continue;
            }

            consumedControls.Add(legacyName);
            diagnostics.Add($"Legacy lock overlay geometry consumed: {legacyName} -> {targetName}");
            if (!config.Controls.TryGetValue(targetName, out var target))
            {
                diagnostics.Add($"Legacy lock overlay geometry was folded into lockable control metadata, but target body control was not present: {window}/{canvas}/{legacyName} -> {targetName}");
                continue;
            }

            if (target is ImageFrontedControlConfig image)
            {
                image.Lockable = true;
            }

            diagnostics.Add("Legacy lock overlay geometry was folded into lockable control and separate geometry is not representable.");
        }
    }

    private static bool TryMapLegacyLockOverlayName(string legacyName, out string targetName)
    {
        targetName = string.Empty;
        var match = LegacyCurrentBanLockOverlayName.Match(legacyName);
        if (!match.Success)
        {
            match = LegacyGlobalBanLockOverlayName.Match(legacyName);
            if (!match.Success)
            {
                return false;
            }

            targetName = $"{match.Groups[1].Value}GlobalBan{match.Groups["index"].Value}";
            return true;
        }

        targetName = $"{match.Groups[1].Value}BanCurrent{match.Groups["index"].Value}";
        return true;
    }

    private static void ApplyGeometry(FrontedControlConfigBase control, ElementInfo legacy)
    {
        if (legacy.Left.HasValue)
        {
            control.Left = FrontedLayoutNumberNormalizer.Normalize(legacy.Left.Value);
        }

        if (legacy.Top.HasValue)
        {
            control.Top = FrontedLayoutNumberNormalizer.Normalize(legacy.Top.Value);
        }

        if (legacy.Width.HasValue)
        {
            control.Width = FrontedLayoutNumberNormalizer.Normalize(legacy.Width.Value);
        }

        if (legacy.Height.HasValue)
        {
            control.Height = FrontedLayoutNumberNormalizer.Normalize(legacy.Height.Value);
        }
    }

    private static void AddBoundsDiagnostics(
        LegacyLayoutMapping mapping,
        IEnumerable<ElementInfo> legacyPositions,
        ICollection<string> warnings)
    {
        if (!string.Equals(mapping.SourceWindow, "WidgetsWindow", StringComparison.Ordinal)
            || !string.Equals(mapping.SourceCanvas, "BpOverViewCanvas", StringComparison.Ordinal)
            || !mapping.FixedCanvasWidth.HasValue
            || !mapping.FixedCanvasHeight.HasValue)
        {
            return;
        }

        var bounds = PaintedBounds.From(legacyPositions);
        if (bounds is null)
        {
            return;
        }

        const double tolerance = 0.01D;
        if (bounds.Value.MinX < -tolerance
            || bounds.Value.MinY < -tolerance
            || bounds.Value.MaxX > mapping.FixedCanvasWidth.Value + tolerance
            || bounds.Value.MaxY > mapping.FixedCanvasHeight.Value + tolerance)
        {
            warnings.Add("Legacy BpOverViewCanvas content exceeds the fixed source canvas bounds and may be clipped after window-centric split.");
        }
    }

    private static double? GetMedian(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }

    private static bool IsIrregular(IReadOnlyList<double> values)
    {
        return values.Count > 1
               && values.Max() - values.Min() > 1;
    }

    private static ResourceConvertState CopyCustomUiResources(
        string extractionRoot,
        string stagingRoot,
        string packageId,
        ICollection<string> infos)
    {
        var state = new ResourceConvertState(packageId);
        var customUiRoot = Path.Combine(extractionRoot, "CustomUi");
        if (!Directory.Exists(customUiRoot))
        {
            return state;
        }

        var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(customUiRoot));
        foreach (var file in Directory.EnumerateFiles(customUiRoot, "*", SearchOption.AllDirectories))
        {
            var fullFile = Path.GetFullPath(file);
            if (!fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unsafe CustomUi file path: {file}");
            }

            var extension = Path.GetExtension(fullFile);
            var kind = ImageExtensions.Contains(extension) ? "Image" : "Other";
            var sha256 = ComputeSha256(fullFile);
            var safeName = CreateResourceFileName(Path.GetFileNameWithoutExtension(fullFile), sha256, extension);
            var folder = kind == "Image" ? "images" : "other";
            var relativePath = ToZipPath("resources", folder, safeName);
            var targetPath = Path.Combine(stagingRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(fullFile, targetPath, overwrite: false);
            var uri = $"bpui://{packageId}/{relativePath}";

            state.Add(fullFile, uri, relativePath, kind, sha256, safeName);
            infos.Add($"Legacy resource copied: {Path.GetFileName(fullFile)}");
        }

        return state;
    }

    private static IReadOnlyDictionary<string, string> ReadFrontendConfigValueMap(
        string extractionRoot,
        ResourceConvertState resourceState,
        ICollection<string> diagnostics,
        ICollection<string> warnings)
    {
        var configPath = Path.Combine(extractionRoot, "Config.json");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(configPath))
        {
            return result;
        }

        JsonNode? root;
        try
        {
            if (new FileInfo(configPath).Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                warnings.Add("Legacy Config.json is too large; frontend image settings were ignored.");
                return result;
            }

            root = JsonNode.Parse(
                File.ReadAllText(configPath),
                nodeOptions: null,
                documentOptions: new JsonDocumentOptions { MaxDepth = FrontedLayoutLimits.MaxJsonDepth });
        }
        catch (Exception ex)
        {
            warnings.Add($"Legacy Config.json could not be read; frontend image settings were ignored. {ex.Message}");
            return result;
        }

        AddMappedImage(root, "BpWindowSettings", "BgImageUri", "BpWindow/BaseCanvas/BackgroundImage", resourceState, result, diagnostics);
        AddMappedImage(root, "CutSceneWindowSettings", "BgUri", "CutSceneWindow/BaseCanvas/BackgroundImage", resourceState, result, diagnostics);
        AddMappedImage(root, "ScoreWindowSettings", "SurScoreBgImageUri", "ScoreSurWindow/BaseCanvas/BackgroundImage", resourceState, result, diagnostics);
        AddMappedImage(root, "ScoreWindowSettings", "HunScoreBgImageUri", "ScoreHunWindow/BaseCanvas/BackgroundImage", resourceState, result, diagnostics);
        AddMappedImage(root, "ScoreWindowSettings", "GlobalScoreBgImageUri", "ScoreGlobalWindow/BaseCanvas/BackgroundImage", resourceState, result, diagnostics);
        AddMappedImage(root, "ScoreWindowSettings", "GlobalScoreBgImageUriBo3", "ScoreGlobalWindow/BaseCanvas/BoModeStates/Bo3/BackgroundImage", resourceState, result, diagnostics);
        AddMappedImage(root, "GameDataWindowSettings", "BgImageUri", "GameDataWindow/BaseCanvas/BackgroundImage", resourceState, result, diagnostics);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpBgUri", "WidgetsWindow/MapBpCanvas/BackgroundImage", resourceState, result, diagnostics);
        AddMappedImage(root, "WidgetsWindowSettings", "BpOverviewBgUri", "BpOverviewWindow/BaseCanvas/BackgroundImage", resourceState, result, diagnostics);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpV2BgUri", "MapV2Window/BaseCanvas/BackgroundImage", resourceState, result, diagnostics);
        AddMappedImage(root, "BpWindowSettings", "CurrentBanLockImageUri", "BpWindow/BaseCanvas/CurrentBanLockImage", resourceState, result, diagnostics);
        AddMappedImage(root, "BpWindowSettings", "GlobalBanLockImageUri", "BpWindow/BaseCanvas/GlobalBanLockImage", resourceState, result, diagnostics);
        AddMappedImage(root, "BpWindowSettings", "PickingBorderImageUri", "BpWindow/BaseCanvas/PickingBorderImage", resourceState, result, diagnostics);
        AddMappedValue(root, "BpWindowSettings", "PickingBorderColor", "BpWindow/BaseCanvas/PickingBorderColor", result);
        AddMappedImage(root, "WidgetsWindowSettings", "CurrentBanLockImageUri", "BpOverviewWindow/BaseCanvas/CurrentBanLockImage", resourceState, result, diagnostics);
        AddMappedImage(root, "WidgetsWindowSettings", "GlobalBanLockImageUri", "BpOverviewWindow/BaseCanvas/GlobalBanLockImage", resourceState, result, diagnostics);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpV2PickingBorderImageUri", "MapV2Window/BaseCanvas/MapBpV2PickingBorderImage", resourceState, result, diagnostics);
        AddMappedValue(root, "WidgetsWindowSettings", "MapBpV2_PickingBorderColor", "MapV2Window/BaseCanvas/MapBpV2PickingBorderColor", result);

        foreach (var ignored in EnumeratePotentialFrontendImageFields(root)
                     .Where(field => !KnownConfigImageFields.Contains(field, StringComparer.Ordinal)))
        {
            diagnostics.Add($"Legacy field ignored: {ignored}");
        }

        return result;
    }

    private LegacySettings? ReadLegacySettings(
        string extractionRoot,
        ICollection<string> diagnostics,
        ICollection<string> warnings)
    {
        var configPath = Path.Combine(extractionRoot, "Config.json");
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            if (new FileInfo(configPath).Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                warnings.Add("Legacy Config.json is too large; frontend text settings were ignored.");
                return null;
            }

            return JsonSerializer.Deserialize<LegacySettings>(File.ReadAllText(configPath), _jsonOptions);
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Legacy Config.json text settings could not be read: {ex.Message}");
            return null;
        }
    }

    private static IReadOnlySet<string> ReadLegacyPropertySet(
        string extractionRoot,
        ICollection<string> diagnostics,
        ICollection<string> warnings)
    {
        var configPath = Path.Combine(extractionRoot, "Config.json");
        if (!File.Exists(configPath))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            if (new FileInfo(configPath).Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                warnings.Add("Legacy Config.json is too large; frontend window settings were ignored.");
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject;
            var properties = new HashSet<string>(StringComparer.Ordinal);
            if (root is null)
            {
                return properties;
            }

            foreach (var settings in root)
            {
                if (settings.Value is not JsonObject settingsObject)
                {
                    continue;
                }

                foreach (var property in settingsObject)
                {
                    properties.Add($"{settings.Key}.{property.Key}");
                }
            }

            return properties;
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Legacy Config.json window settings could not be inspected: {ex.Message}");
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static void ApplyLegacyWindowSettings(
        FrontedWindowConfig target,
        LegacyLayoutMapping mapping,
        LegacySettings? legacySettings,
        IReadOnlySet<string> legacyPropertySet,
        ICollection<string> diagnostics)
    {
        if (mapping.FixedCanvasWidth.HasValue && mapping.FixedCanvasHeight.HasValue)
        {
            target.WindowSettings.WindowWidth = mapping.FixedCanvasWidth.Value;
            target.WindowSettings.WindowHeight = mapping.FixedCanvasHeight.Value;
            target.CanvasSettings.CanvasWidth = mapping.FixedCanvasWidth.Value;
            target.CanvasSettings.CanvasHeight = mapping.FixedCanvasHeight.Value;
        }

        if (legacySettings is null)
        {
            return;
        }

        var (windowSize, backgroundColor, allowTransparency) = mapping.TargetWindow switch
        {
            "BpWindow" => (
                legacySettings.BpWindowSettings?.WindowSize,
                legacySettings.BpWindowSettings?.BackgroundColor,
                HasLegacyProperty(legacyPropertySet, "BpWindowSettings", "AllowsWindowTransparency")
                    ? legacySettings.BpWindowSettings?.AllowsWindowTransparency
                    : null),
            "ScoreSurWindow" or "ScoreHunWindow" => (
                legacySettings.ScoreWindowSettings?.ScoreInGameWindowSize,
                null,
                null),
            "ScoreGlobalWindow" => (
                legacySettings.ScoreWindowSettings?.ScoreGlobalWindowSize,
                legacySettings.ScoreWindowSettings?.ScoreGlobalWindowBackgroundColor,
                HasLegacyProperty(legacyPropertySet, "ScoreWindowSettings", "AllowsScoreGlobalWindowTransparency")
                    ? legacySettings.ScoreWindowSettings?.AllowsScoreGlobalWindowTransparency
                    : null),
            "CutSceneWindow" => (
                legacySettings.CutSceneWindowSettings?.WindowSize,
                null,
                null),
            "GameDataWindow" => (
                legacySettings.GameDataWindowSettings?.WindowSize,
                null,
                null),
            "BpOverviewWindow" or "MapV2Window" => (
                null,
                legacySettings.WidgetsWindowSettings?.BackgroundColor,
                HasLegacyProperty(legacyPropertySet, "WidgetsWindowSettings", "AllowsWindowTransparency")
                    ? legacySettings.WidgetsWindowSettings?.AllowsWindowTransparency
                    : null),
            _ => (null, null, null)
        };

        if (windowSize is not null && !mapping.FixedCanvasWidth.HasValue)
        {
            if (double.IsFinite(windowSize.Width) && windowSize.Width > 0D)
            {
                target.WindowSettings.WindowWidth = windowSize.Width;
            }

            if (double.IsFinite(windowSize.Height) && windowSize.Height > 0D)
            {
                target.WindowSettings.WindowHeight = windowSize.Height;
            }

            if (IsPositiveFinite(windowSize.Width)
                && IsPositiveFinite(windowSize.Height)
                && (!AreClose(windowSize.Width, target.CanvasSettings.CanvasWidth)
                    || !AreClose(windowSize.Height, target.CanvasSettings.CanvasHeight)))
            {
                diagnostics.Add(
                    $"Legacy window size differs from converter legacy canvas default: {mapping.TargetWindow} Window={windowSize.Width}x{windowSize.Height}, Canvas={target.CanvasSettings.CanvasWidth}x{target.CanvasSettings.CanvasHeight}.");
            }
        }

        if (allowTransparency.HasValue)
        {
            target.WindowSettings.AllowsTransparency = allowTransparency.Value;
        }

        if (!string.IsNullOrWhiteSpace(backgroundColor))
        {
            target.WindowSettings.BackgroundColor = backgroundColor;
        }
        else if (allowTransparency == true)
        {
            target.WindowSettings.BackgroundColor = "#00000000";
        }
        else if (allowTransparency == false)
        {
            target.WindowSettings.BackgroundColor = DefaultOpaqueBackgroundColor;
        }
    }

    private static bool HasLegacyProperty(IReadOnlySet<string> legacyPropertySet, string settingsName, string propertyName)
    {
        return legacyPropertySet.Contains($"{settingsName}.{propertyName}");
    }

    private static bool IsPositiveFinite(double value)
    {
        return double.IsFinite(value) && value > 0D;
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) < 0.01D;
    }

    private static LegacyWindowDefaults GetLegacyWindowDefaults(LegacyLayoutMapping mapping)
    {
        if (mapping.FixedCanvasWidth.HasValue && mapping.FixedCanvasHeight.HasValue)
        {
            return new LegacyWindowDefaults(
                mapping.FixedCanvasWidth.Value,
                mapping.FixedCanvasHeight.Value,
                mapping.FixedCanvasWidth.Value,
                mapping.FixedCanvasHeight.Value,
                GetLegacyBackgroundImage(mapping.TargetWindow));
        }

        return mapping.TargetWindow switch
        {
            "ScoreSurWindow" => new LegacyWindowDefaults(480, 152, 480, 152, "Resources/scoreSur.png"),
            "ScoreHunWindow" => new LegacyWindowDefaults(480, 152, 480, 152, "Resources/scoreHun.png"),
            "ScoreGlobalWindow" => new LegacyWindowDefaults(1440, 195, 1440, 195, "Resources/scoreGlobal.png"),
            "CutSceneWindow" => new LegacyWindowDefaults(1440, 810, 1440, 810, "Resources/cutScene.png"),
            "GameDataWindow" => new LegacyWindowDefaults(1440, 810, 1440, 810, "Resources/gameData.png"),
            "BpWindow" => new LegacyWindowDefaults(1440, 810, 1440, 810, "Resources/bp.png"),
            _ => new LegacyWindowDefaults(1440, 810, 1440, 810, GetLegacyBackgroundImage(mapping.TargetWindow))
        };
    }

    private static string? GetLegacyBackgroundImage(string? targetWindow)
    {
        return targetWindow switch
        {
            "BpOverviewWindow" => "Resources/bpOverview.png",
            "MapV2Window" => "Resources/mapBpV2.png",
            _ => null
        };
    }

    private static void ApplyCanvasConfig(FrontedWindowConfig target, FrontedCanvasConfig source)
    {
        target.Version = 3;
        target.CanvasSettings.CanvasWidth = source.CanvasWidth;
        target.CanvasSettings.CanvasHeight = source.CanvasHeight;
        target.CanvasSettings.BackgroundImage = source.BackgroundImage;
        target.CanvasSettings.EnableBoModeStates = source.EnableBoModeStates;
        target.CanvasSettings.BoModeStates = source.BoModeStates;
        target.ControlLayout.RequiredPlugins = source.RequiredPlugins;
        target.ControlLayout.Controls = source.Controls;
    }

    private static void AddMappedImage(
        JsonNode? root,
        string settingsObject,
        string propertyName,
        string key,
        ResourceConvertState resourceState,
        IDictionary<string, string> result,
        ICollection<string> diagnostics)
    {
        var field = $"{settingsObject}.{propertyName}";
        var value = root?[settingsObject]?[propertyName]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (TryMapLegacyResourceValue(value, resourceState, out var uri))
        {
            result[key] = uri;
            return;
        }

        diagnostics.Add($"Legacy resource missing or not packaged for field {field}: {value}");
    }

    private static void AddMappedValue(
        JsonNode? root,
        string settingsObject,
        string propertyName,
        string key,
        IDictionary<string, string> result)
    {
        var value = root?[settingsObject]?[propertyName]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(value))
        {
            result[key] = value.Trim();
        }
    }

    private static readonly HashSet<string> KnownConfigImageFields =
    [
        "BpWindowSettings.BgImageUri",
        "CutSceneWindowSettings.BgUri",
        "ScoreWindowSettings.SurScoreBgImageUri",
        "ScoreWindowSettings.HunScoreBgImageUri",
        "ScoreWindowSettings.GlobalScoreBgImageUri",
        "ScoreWindowSettings.GlobalScoreBgImageUriBo3",
        "GameDataWindowSettings.BgImageUri",
        "WidgetsWindowSettings.MapBpBgUri",
        "WidgetsWindowSettings.BpOverviewBgUri",
        "WidgetsWindowSettings.MapBpV2BgUri",
        "BpWindowSettings.CurrentBanLockImageUri",
        "BpWindowSettings.GlobalBanLockImageUri",
        "BpWindowSettings.PickingBorderImageUri",
        "BpWindowSettings.PickingBorderColor",
        "WidgetsWindowSettings.CurrentBanLockImageUri",
        "WidgetsWindowSettings.GlobalBanLockImageUri",
        "WidgetsWindowSettings.MapBpV2PickingBorderImageUri",
        "WidgetsWindowSettings.MapBpV2_PickingBorderColor"
    ];

    private static IEnumerable<string> EnumeratePotentialFrontendImageFields(JsonNode? node)
    {
        if (node is not JsonObject root)
        {
            yield break;
        }

        foreach (var settings in root)
        {
            if (settings.Value is not JsonObject obj
                || !settings.Key.EndsWith("WindowSettings", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var property in obj)
            {
                if (property.Value is JsonValue value
                    && value.TryGetValue<string>(out _)
                    && (property.Key.EndsWith("Uri", StringComparison.Ordinal)
                        || property.Key.EndsWith("ImageUri", StringComparison.Ordinal)
                        || property.Key.EndsWith("Color", StringComparison.Ordinal)))
                {
                    yield return $"{settings.Key}.{property.Key}";
                }
            }
        }
    }

    private static void ApplyFrontendConfigValues(
        FrontedCanvasConfig config,
        LegacyLayoutMapping mapping,
        IReadOnlyDictionary<string, string> valueMap,
        ICollection<string> infos)
    {
        var prefix = $"{mapping.TargetWindow}/{FrontedLayoutConstants.BaseCanvasName}/";
        if (valueMap.TryGetValue($"{prefix}BackgroundImage", out var background))
        {
            config.BackgroundImage = background;
        }

        if (valueMap.TryGetValue(
                $"{prefix}BoModeStates/Bo3/BackgroundImage",
                out var scoreGlobalBo3Background))
        {
            config.EnableBoModeStates = true;
            if (!config.BoModeStates.TryGetValue(
                    FrontedCanvasRuntimeStateResolver.Bo3StateKey,
                    out var bo3State))
            {
                bo3State = new FrontedCanvasStateConfig();
                config.BoModeStates[FrontedCanvasRuntimeStateResolver.Bo3StateKey] = bo3State;
            }

            bo3State.BackgroundImage = scoreGlobalBo3Background;
            infos.Add("Legacy BO3 global score background mapped into ScoreGlobal BO3 canvas state.");
        }

        if (mapping.TargetWindow == "BpWindow")
        {
            foreach (var control in config.Controls.Values.OfType<ImageFrontedControlConfig>())
            {
                if (control.Lockable && IsBanImageBinding(control.BindingPath, out var isGlobalBan))
                {
                    var key = isGlobalBan
                        ? $"{prefix}GlobalBanLockImage"
                        : $"{prefix}CurrentBanLockImage";
                    if (valueMap.TryGetValue(key, out var lockUri))
                    {
                        control.LockImagePath = lockUri;
                        infos.Add($"Legacy lock image merged into v3 Image lock overlay: {key}");
                    }
                }

                if (control.PickingBorderAvailable
                    && valueMap.TryGetValue($"{prefix}PickingBorderImage", out var borderUri))
                {
                    control.PickingBorderImagePath = borderUri;
                    infos.Add($"Legacy picking border image merged into v3 Image picking overlay: {prefix}PickingBorderImage");
                }
            }
        }

        if (mapping.TargetWindow == "BpOverviewWindow")
        {
            foreach (var control in config.Controls.Values
                         .OfType<ImageFrontedControlConfig>()
                         .Where(control => control.Lockable
                                           && IsCurrentBanImageBinding(control.BindingPath)))
            {
                var key = $"{prefix}CurrentBanLockImage";
                if (valueMap.TryGetValue(key, out var lockUri))
                {
                    control.LockImagePath = lockUri;
                    infos.Add($"Legacy lock image merged into v3 Image lock overlay: {key}");
                }
            }
        }

        if (mapping.TargetWindow == "MapV2Window")
        {
            foreach (var control in config.Controls.Values.OfType<MapV2DisplayControlConfig>())
            {
                if (valueMap.TryGetValue($"{prefix}MapBpV2PickingBorderImage", out var borderUri))
                {
                    control.PickingBorderImagePath = borderUri;
                }

                if (valueMap.TryGetValue($"{prefix}MapBpV2PickingBorderColor", out var borderColor))
                {
                    control.PickingBorderFillColor = borderColor;
                }
            }
        }
    }

    private static bool IsCurrentBanImageBinding(string? bindingPath)
    {
        return bindingPath?.Contains("CurrentSurBannedList", StringComparison.Ordinal) == true
               || bindingPath?.Contains("CurrentHunBannedList", StringComparison.Ordinal) == true;
    }

    private static bool IsBanImageBinding(string? bindingPath, out bool isGlobalBan)
    {
        isGlobalBan = bindingPath?.Contains("GlobalBanned", StringComparison.Ordinal) == true;
        return IsCurrentBanImageBinding(bindingPath)
               || isGlobalBan;
    }

    private static void RewriteKnownResourceStrings(
        FrontedCanvasConfig config,
        ResourceConvertState resourceState)
    {
        var node = JsonSerializer.SerializeToNode(config) ?? throw new InvalidOperationException("Layout could not be serialized.");
        RewriteResourceStrings(node, resourceState, null);
        var converted = node.Deserialize<FrontedCanvasConfig>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (converted is null)
        {
            return;
        }

        config.BackgroundImage = converted.BackgroundImage;
        config.EnableBoModeStates = converted.EnableBoModeStates;
        config.BoModeStates = converted.BoModeStates;
        config.Controls = converted.Controls;
    }

    private static void RewriteResourceStrings(JsonNode node, ResourceConvertState resourceState, string? propertyName)
    {
        if (node is JsonObject obj)
        {
            foreach (var child in obj.ToArray())
            {
                if (child.Value is JsonValue value
                    && value.TryGetValue<string>(out var text)
                    && ShouldInspectResourceProperty(child.Key)
                    && TryMapLegacyResourceValue(text, resourceState, out var uri))
                {
                    obj[child.Key] = uri;
                    continue;
                }

                if (child.Value is not null)
                {
                    RewriteResourceStrings(child.Value, resourceState, child.Key);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    RewriteResourceStrings(child, resourceState, propertyName);
                }
            }
        }
    }

    private static bool TryMapLegacyResourceValue(string value, ResourceConvertState resourceState, out string uri)
    {
        uri = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("pack://application:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expanded = Environment.ExpandEnvironmentVariables(value).Replace('\\', '/');
        var fileName = Path.GetFileName(expanded);
        if (!string.IsNullOrWhiteSpace(fileName)
            && resourceState.ByFileName.TryGetValue(fileName, out var fileUri))
        {
            uri = fileUri;
            return true;
        }

        var normalized = expanded.TrimStart('/');
        if (resourceState.ByLegacyRelativePath.TryGetValue(normalized, out var relativeUri))
        {
            uri = relativeUri;
            return true;
        }

        return false;
    }

    private static bool ShouldInspectResourceProperty(string propertyName)
    {
        return string.Equals(propertyName, nameof(FrontedCanvasConfig.BackgroundImage), StringComparison.Ordinal)
               || propertyName.EndsWith("ImagePath", StringComparison.Ordinal)
               || propertyName.EndsWith("ImageSource", StringComparison.Ordinal)
               || propertyName.EndsWith("ResourcePath", StringComparison.Ordinal)
               || propertyName.EndsWith("LockImageSource", StringComparison.Ordinal)
               || propertyName.EndsWith("BorderImagePath", StringComparison.Ordinal);
    }

    private static FrontedLayoutPackageManifest CreateManifest(
        FrontedLayoutPackageLegacyConvertRequest request,
        string packageId)
    {
        return new FrontedLayoutPackageManifest
        {
            PackageId = packageId,
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? "Converted Legacy Layout"
                : request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? "Converted from a legacy .bpui package. Some legacy fields may be ignored when no safe Designer v3 mapping exists."
                : request.Description.Trim(),
            Author = string.IsNullOrWhiteSpace(request.Author)
                ? string.Empty
                : request.Author!.Trim(),
            MinVersion = string.IsNullOrWhiteSpace(request.MinVersion)
                ? GetDefaultMinVersion()
                : request.MinVersion!.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            ImportPolicy = new FrontedLayoutPackageImportPolicy
            {
                RequireRestart = false,
                OverwriteExistingUserLayouts = "Ask"
            }
        };
    }

    private static IReadOnlyDictionary<LegacyLayoutKey, IReadOnlyList<LegacyControlBlueprint>> CreateLegacyControlBlueprints()
    {
        var result = new Dictionary<LegacyLayoutKey, IReadOnlyList<LegacyControlBlueprint>>();

        AddBlueprints(result, "BpWindow", "BaseCanvas",
        [
            .. TextNames("Timer", "SurTeamName", "HunTeamName", "GameScoresSur", "GameScoresHun", "SurTeamMajorPoint", "HunTeamMajorPoint", "SurId0", "SurId1", "SurId2", "SurId3", "HunId", "MapName", "GameProgress"),
            .. ImageNames("SurTeamLogo", "HunTeamLogo", "Map", "SurPick0", "SurPick1", "SurPick2", "SurPick3", "HunPick", "SurBanCurrent0", "SurBanCurrent1", "SurBanCurrent2", "SurBanCurrent3", "HunBanCurrent0", "HunBanCurrent1", "SurGlobalBan0", "SurGlobalBan1", "SurGlobalBan2", "SurGlobalBan3", "SurGlobalBan4", "SurGlobalBan5", "SurGlobalBan6", "SurGlobalBan7", "SurGlobalBan8", "SurGlobalBan9", "SurGlobalBan10", "SurGlobalBan11", "HunGlobalBan0", "HunGlobalBan1", "HunGlobalBan2")
        ]);

        AddBlueprints(result, "CutSceneWindow", "BaseCanvas",
        [
            .. TextNames("SurTeamName", "HunTeamName", "SurTeamMajorPoint", "HunTeamMajorPoint", "SurId0", "SurId1", "SurId2", "SurId3", "HunId", "MapName", "GameProgress"),
            .. ImageNames("SurTeamLogo", "HunTeamLogo", "Map", "SurPick0", "SurPick1", "SurPick2", "SurPick3", "HunPick", "SurTalent0", "SurTalent1", "SurTalent2", "SurTalent3", "HunTalent", "Trait")
        ]);

        AddBlueprints(result, "GameDataWindow", "BaseCanvas",
        [
            .. TextNames("SurTeamName", "HunTeamName", "GameScoresSur", "GameScoresHun", "SurTeamMajorPoint", "HunTeamMajorPoint", "SurId0", "SurId1", "SurId2", "SurId3", "HunId", "MapName", "PickedMapName", "GameProgress", "Header_ID", "Header_Character", "Header_DecodingProgress", "Header_ContainmentTime", "Header_Rescues", "Header_Heals", "Header_PalletStrikes", "Header_RemainingCiphers", "Header_Knockdowns", "Header_PalletsDestroyed", "Header_SurvivorHits", "Header_TerrorShocks", "Sur0MachineDecoded", "Sur1MachineDecoded", "Sur2MachineDecoded", "Sur3MachineDecoded", "Sur0KiteTime", "Sur1KiteTime", "Sur2KiteTime", "Sur3KiteTime", "Sur0RescueTimes", "Sur1RescueTimes", "Sur2RescueTimes", "Sur3RescueTimes", "Sur0HealedTimes", "Sur1HealedTimes", "Sur2HealedTimes", "Sur3HealedTimes", "Sur0PalletStunTimes", "Sur1PalletStunTimes", "Sur2PalletStunTimes", "Sur3PalletStunTimes", "HunMachineLeft", "HunDownTimes", "HunPalletBroken", "HunHitTimes", "HunTerrorShockTimes"),
            Blueprint("Header_ID", "SurDataHeader0", "Text"),
            Blueprint("Sur0MachineDecoded", "SurData0", "Text"),
            .. ImageNames("SurTeamLogo", "HunTeamLogo", "Map", "Player0Header", "Player1Header", "Player2Header", "Player3Header", "HunImage")
        ]);

        AddBlueprints(result, "ScoreSurWindow", "BaseCanvas",
        [
            .. TextNames("SurTeamName", "GameScoresSur", "SurTeamMajorPoint"),
            .. ImageNames("SurTeamLogo")
        ]);

        AddBlueprints(result, "ScoreHunWindow", "BaseCanvas",
        [
            .. TextNames("HunTeamName", "GameScoresHun", "HunTeamMajorPoint"),
            .. ImageNames("HunTeamLogo")
        ]);

        AddBlueprints(result, "ScoreGlobalWindow", "BaseCanvas",
        [
            Blueprint("MainTeamName", "HomeTeamName", "Text"),
            Blueprint("AwayTeamName", "AwayTeamName", "Text"),
            Blueprint("MainScoreTotal", "HomeScoreTotal", "Text"),
            Blueprint("AwayScoreTotal", "AwayScoreTotal", "Text"),
            Blueprint("HomeGlobalScoreRow", "HomeGlobalScoreRow", "GlobalScoreRow"),
            Blueprint("AwayGlobalScoreRow", "AwayGlobalScoreRow", "GlobalScoreRow")
        ]);

        AddBlueprints(result, "WidgetsWindow", "BpOverViewCanvas",
        [
            .. TextNames("SurTeamNameInOverview", "HunTeamNameInOverview", "GameProgress", "GameScoresSur", "RatioChar", "GameScoresHun"),
            .. ImageNames("SurTeamLogo", "HunTeamLogo", "SurBanCurrent0", "SurBanCurrent1", "SurBanCurrent2", "SurBanCurrent3", "HunBanCurrent0", "HunBanCurrent1", "SurPick0", "SurPick1", "SurPick2", "SurPick3", "HunPick")
        ]);

        AddBlueprints(result, "WidgetsWindow", "MapV2Canvas",
        [
            Blueprint("Arms_Factory", "Arms_Factory", "MapV2Display"),
            Blueprint("The_Red_Church", "The_Red_Church", "MapV2Display"),
            Blueprint("Sacred_Heart_Hospital", "Sacred_Heart_Hospital", "MapV2Display"),
            Blueprint("Leo_s_Memory", "Leo_s_Memory", "MapV2Display"),
            Blueprint("Moonlit_River_Park", "Moonlit_River_Park", "MapV2Display"),
            Blueprint("Lakeside_Village", "Lakeside_Village", "MapV2Display"),
            Blueprint("Eversleeping_Town", "Eversleeping_Town", "MapV2Display"),
            Blueprint("Chinatown", "Chinatown", "MapV2Display"),
            Blueprint("Darkwoods", "Darkwoods", "MapV2Display")
        ]);

        return result;
    }

    private static void AddBlueprints(
        IDictionary<LegacyLayoutKey, IReadOnlyList<LegacyControlBlueprint>> result,
        string window,
        string canvas,
        IReadOnlyList<LegacyControlBlueprint> blueprints)
    {
        result[new LegacyLayoutKey(window, canvas)] = blueprints
            .Select(blueprint => blueprint with
            {
                SourceWindow = window,
                SourceCanvas = canvas
            })
            .ToArray();
    }

    private static LegacyControlBlueprint[] TextNames(params string[] names) =>
        names.Select(name => Blueprint(name, name, GetTextControlType(name))).ToArray();

    private static LegacyControlBlueprint[] ImageNames(params string[] names) =>
        names.Select(name => Blueprint(name, name, GetImageControlType(name))).ToArray();

    private static LegacyControlBlueprint Blueprint(
        string legacyName,
        string targetName,
        string controlType,
        bool required = true) =>
        new()
        {
            LegacyName = legacyName,
            TargetName = targetName,
            ControlType = controlType,
            Required = required
        };

    private static string GetTextControlType(string name)
    {
        if (string.Equals(name, "GameProgress", StringComparison.Ordinal))
        {
            return "GameProgressText";
        }

        if (name.Contains("MapName", StringComparison.Ordinal)
            || string.Equals(name, "PickedMapName", StringComparison.Ordinal)
            || string.Equals(name, "BannedMapName", StringComparison.Ordinal))
        {
            return "MapNameText";
        }

        return "Text";
    }

    private static string GetImageControlType(string name)
    {
        if (name.Contains("Talent", StringComparison.Ordinal) || string.Equals(name, "Trait", StringComparison.Ordinal))
        {
            return "TalentTraitDisplay";
        }

        if (name.Contains("Pick", StringComparison.Ordinal) || string.Equals(name, "Map", StringComparison.Ordinal) || name.EndsWith("Image", StringComparison.Ordinal))
        {
            return "BorderedImage";
        }

        return "Image";
    }

    private static FrontedControlConfigBase CreateDefaultControl(LegacyControlBlueprint blueprint)
    {
        return blueprint.ControlType switch
        {
            "Text" => CreateDefaultText(blueprint),
            "MapNameText" => CreateDefaultMapNameText(blueprint.TargetName),
            "GameProgressText" => CreateDefaultGameProgressText(),
            "Image" => CreateDefaultImage(blueprint),
            "BorderedImage" => CreateDefaultBorderedImage(blueprint),
            "TalentTraitDisplay" => CreateDefaultTalentTrait(blueprint.TargetName),
            "GlobalScoreRow" => CreateDefaultGlobalScoreRow(blueprint.TargetName),
            "MapV2Display" => CreateDefaultMapV2Display(blueprint.TargetName),
            _ => new FrontedControlConfigBase { ControlType = blueprint.ControlType }
        };
    }

    private static TextFrontedControlConfig CreateDefaultText(LegacyControlBlueprint blueprint)
    {
        var name = blueprint.TargetName;
        var style = GetLegacyTextDefaults(blueprint.SourceWindow, blueprint.SourceCanvas, name);
        return new TextFrontedControlConfig
        {
            Text = GetStaticText(name),
            TextBinding = CreateTextBinding(GetTextBindingPath(name)),
            HorizontalAlignment = style.HorizontalAlignment,
            VerticalAlignment = style.VerticalAlignment,
            TextAlignment = style.TextAlignment,
            TextWrapping = style.TextWrapping,
            FontFamily = style.FontFamily,
            FontWeight = style.FontWeight,
            Color = style.Color,
            FontSize = style.FontSize,
            Width = 40,
            Height = 24
        };
    }

    private static MapNameTextControlConfig CreateDefaultMapNameText(string name)
    {
        return new MapNameTextControlConfig
        {
            BindingPath = name.StartsWith("Banned", StringComparison.Ordinal)
                ? "CurrentGame.BannedMap"
                : "CurrentGame.PickedMap",
            HorizontalAlignment = "Center",
            VerticalAlignment = "Center",
            TextAlignment = "Center",
            FontFamily = "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简",
            FontWeight = "Normal",
            Color = "#FFFFFFFF",
            FontSize = 22,
            Width = 80,
            Height = 24
        };
    }

    private static GameProgressTextControlConfig CreateDefaultGameProgressText()
    {
        return new GameProgressTextControlConfig
        {
            HorizontalAlignment = "Center",
            VerticalAlignment = "Center",
            TextAlignment = "Center",
            FontFamily = "pack://application:,,,/Assets/Fonts/#华康POP1体W5",
            FontWeight = "Normal",
            Color = "#FFFFFFFF",
            FontSize = 22,
            Width = 120,
            Height = 36
        };
    }

    private static ImageFrontedControlConfig CreateDefaultImage(LegacyControlBlueprint blueprint)
    {
        var name = blueprint.TargetName;
        var image = new ImageFrontedControlConfig
        {
            BindingPath = GetImageBindingPath(blueprint.SourceWindow, blueprint.SourceCanvas, name),
            SizingMode = ImageSizingMode.FillContainer,
            Stretch = "Uniform",
            Width = 40,
            Height = 40
        };
        ConfigureLockableImage(name, image);
        return image;
    }

    private static BorderedImageFrontedControlConfig CreateDefaultBorderedImage(LegacyControlBlueprint blueprint)
    {
        var name = blueprint.TargetName;
        var image = new BorderedImageFrontedControlConfig
        {
            BindingPath = GetImageBindingPath(blueprint.SourceWindow, blueprint.SourceCanvas, name),
            SizingMode = ImageSizingMode.OverflowCrop,
            Stretch = "UniformToFill",
            HorizontalAlignment = "Center",
            VerticalAlignment = "Center",
            ClipToBounds = name.Contains("Pick", StringComparison.Ordinal) || name.EndsWith("Image", StringComparison.Ordinal),
            Width = 40,
            Height = 40
        };
        ConfigureLockableImage(name, image);
        ConfigurePickingBorderImage(blueprint, image);
        if (string.Equals(blueprint.SourceWindow, "CutSceneWindow", StringComparison.Ordinal)
            && name.StartsWith("SurPick", StringComparison.Ordinal))
        {
            image.ImageWidth = 556.5;
            image.ImageHeight = null;
        }

        return image;
    }

    private static TalentTraitDisplayControlConfig CreateDefaultTalentTrait(string name)
    {
        if (name.StartsWith("SurTalent", StringComparison.Ordinal)
            && int.TryParse(name["SurTalent".Length..], out var index))
        {
            return new TalentTraitDisplayControlConfig
            {
                DisplayKind = TalentTraitDisplayKind.SurvivorTalent,
                PlayerIndex = index,
                HorizontalAlignment = "Right",
                VerticalAlignment = "Center",
                Width = 180,
                Height = 38
            };
        }

        return new TalentTraitDisplayControlConfig
        {
            DisplayKind = string.Equals(name, "Trait", StringComparison.Ordinal)
                ? TalentTraitDisplayKind.HunterTrait
                : TalentTraitDisplayKind.HunterTalent,
            HorizontalAlignment = "Left",
            VerticalAlignment = "Center",
            Width = 180,
            Height = 38
        };
    }

    private static GlobalScoreRowControlConfig CreateDefaultGlobalScoreRow(string name)
    {
        return new GlobalScoreRowControlConfig
        {
            TeamType = name.StartsWith("Away", StringComparison.Ordinal) ? TeamType.AwayTeam : TeamType.HomeTeam,
            FontFamily = "pack://application:,,,/Assets/Fonts/#华康POP1体W5",
            FontWeight = "Normal",
            Color = "#FFFFFFFF",
            FontSize = 24,
            Width = 1,
            Height = 1
        };
    }

    private static MapV2DisplayControlConfig CreateDefaultMapV2Display(string name)
    {
        return new MapV2DisplayControlConfig
        {
            MapKey = GetMapV2Key(name),
            Width = 151,
            Height = 160,
            MapBorderNormalColor = "#FF2B483B",
            MapBorderBannedColor = "#FF9C3E2F"
        };
    }

    private static void ApplyBlueprintDefaults(
        LegacyControlBlueprint blueprint,
        FrontedControlConfigBase control)
    {
        if (string.IsNullOrWhiteSpace(control.ControlType))
        {
            control.ControlType = blueprint.ControlType;
        }

        switch (control)
        {
            case TextFrontedControlConfig text:
                var textStyle = GetLegacyTextDefaults(blueprint.SourceWindow, blueprint.SourceCanvas, blueprint.TargetName);
                text.TextBinding ??= CreateTextBinding(GetTextBindingPath(blueprint.TargetName));
                text.Text ??= GetStaticText(blueprint.TargetName);
                text.HorizontalAlignment ??= textStyle.HorizontalAlignment;
                text.VerticalAlignment ??= textStyle.VerticalAlignment;
                text.TextAlignment ??= textStyle.TextAlignment;
                text.TextWrapping ??= textStyle.TextWrapping;
                text.FontFamily ??= textStyle.FontFamily;
                text.FontWeight ??= textStyle.FontWeight;
                text.Color ??= textStyle.Color;
                if (text.FontSize <= 0)
                {
                    text.FontSize = textStyle.FontSize;
                }

                break;
            case MapNameTextControlConfig mapName when string.IsNullOrWhiteSpace(mapName.BindingPath):
                mapName.BindingPath = blueprint.TargetName.StartsWith("Banned", StringComparison.Ordinal)
                    ? "CurrentGame.BannedMap"
                    : "CurrentGame.PickedMap";
                break;
            case ImageFrontedControlConfig image:
                image.BindingPath ??= GetImageBindingPath(blueprint.SourceWindow, blueprint.SourceCanvas, blueprint.TargetName);
                ConfigureLockableImage(blueprint.TargetName, image);
                break;
            case GlobalScoreRowControlConfig row:
                row.TeamType = blueprint.TargetName.StartsWith("Away", StringComparison.Ordinal) ? TeamType.AwayTeam : TeamType.HomeTeam;
                break;
            case MapV2DisplayControlConfig map when string.IsNullOrWhiteSpace(map.MapKey):
                map.MapKey = GetMapV2Key(blueprint.TargetName);
                break;
        }
    }

    private static FrontedTextBindingExpression? CreateTextBinding(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : new FrontedTextBindingExpression
            {
                Sources =
                [
                    new FrontedBindingSourceConfig
                    {
                        Path = path
                    }
                ]
            };
    }

    private static string? GetStaticText(string name)
    {
        return name switch
        {
            "RatioChar" => ":",
            "VS_Word" => "VS",
            "PickWord" => "Pick",
            "BanWord" => "Ban",
            _ => null
        };
    }

    private static string? GetTextBindingPath(string name)
    {
        if (string.Equals(name, "SurTeamName", StringComparison.Ordinal)
            || string.Equals(name, "SurTeamNameInOverview", StringComparison.Ordinal))
        {
            return "CurrentGame.SurTeam.Name";
        }

        if (string.Equals(name, "HunTeamName", StringComparison.Ordinal)
            || string.Equals(name, "HunTeamNameInOverview", StringComparison.Ordinal))
        {
            return "CurrentGame.HunTeam.Name";
        }

        if (string.Equals(name, "HomeTeamName", StringComparison.Ordinal))
        {
            return "CurrentGame.HomeTeam.Name";
        }

        if (string.Equals(name, "AwayTeamName", StringComparison.Ordinal))
        {
            return "CurrentGame.AwayTeam.Name";
        }

        if (string.Equals(name, "HomeScoreTotal", StringComparison.Ordinal))
        {
            return "CurrentGame.MatchScore.HomeTotalScore";
        }

        if (string.Equals(name, "AwayScoreTotal", StringComparison.Ordinal))
        {
            return "CurrentGame.MatchScore.AwayTotalScore";
        }

        if (string.Equals(name, "GameScoresSur", StringComparison.Ordinal))
        {
            return "CurrentGame.MatchScore.CurrentSurTeamPreHalfMinorScoreText";
        }

        if (string.Equals(name, "GameScoresHun", StringComparison.Ordinal))
        {
            return "CurrentGame.MatchScore.CurrentHunTeamPreHalfMinorScoreText";
        }

        if (string.Equals(name, "SurTeamMajorPoint", StringComparison.Ordinal))
        {
            return "CurrentGame.MatchScore.CurrentSurTeamMajorText";
        }

        if (string.Equals(name, "HunTeamMajorPoint", StringComparison.Ordinal))
        {
            return "CurrentGame.MatchScore.CurrentHunTeamMajorText";
        }

        if (name.StartsWith("SurId", StringComparison.Ordinal)
            && int.TryParse(name["SurId".Length..], out var index))
        {
            return $"CurrentGame.SurPlayerList[{index}].Member.Name";
        }

        if (string.Equals(name, "HunId", StringComparison.Ordinal))
        {
            return "CurrentGame.HunPlayer.Member.Name";
        }

        return null;
    }

    private static string? GetImageBindingPath(string? window, string? canvas, string name)
    {
        if (string.Equals(name, "SurTeamLogo", StringComparison.Ordinal))
        {
            return "CurrentGame.SurTeam.Logo";
        }

        if (string.Equals(name, "HunTeamLogo", StringComparison.Ordinal))
        {
            return "CurrentGame.HunTeam.Logo";
        }

        if (string.Equals(name, "Map", StringComparison.Ordinal))
        {
            return "CurrentGame.PickedMapImage";
        }

        if (name.StartsWith("SurPick", StringComparison.Ordinal)
            && int.TryParse(name["SurPick".Length..], out var surPickIndex))
        {
            if (string.Equals(window, "BpWindow", StringComparison.Ordinal))
            {
                return $"CurrentGame.SurPlayerList[{surPickIndex}].PictureShown";
            }

            if (string.Equals(window, "CutSceneWindow", StringComparison.Ordinal))
            {
                return $"CurrentGame.SurPlayerList[{surPickIndex}].Character.BigImage";
            }

            return $"CurrentGame.SurPlayerList[{surPickIndex}].Character.HalfImage";
        }

        if (string.Equals(name, "HunPick", StringComparison.Ordinal))
        {
            if (string.Equals(window, "BpWindow", StringComparison.Ordinal))
            {
                return "CurrentGame.HunPlayer.PictureShown";
            }

            if (string.Equals(window, "CutSceneWindow", StringComparison.Ordinal))
            {
                return "CurrentGame.HunPlayer.Character.BigImage";
            }

            return "CurrentGame.HunPlayer.Character.HalfImage";
        }

        if (name.StartsWith("Player", StringComparison.Ordinal)
            && name.EndsWith("Header", StringComparison.Ordinal)
            && int.TryParse(name["Player".Length..^"Header".Length], out var headerIndex))
        {
            return $"CurrentGame.SurPlayerList[{headerIndex}].PictureShownHeader";
        }

        if (string.Equals(name, "HunImage", StringComparison.Ordinal))
        {
            return "CurrentGame.HunPlayer.PictureShownHeader";
        }

        if (name.StartsWith("SurBanCurrent", StringComparison.Ordinal)
            && int.TryParse(name["SurBanCurrent".Length..], out var surCurrentIndex))
        {
            return $"CurrentGame.CurrentSurBannedList[{surCurrentIndex}].HeaderImageSingleColor";
        }

        if (name.StartsWith("HunBanCurrent", StringComparison.Ordinal)
            && int.TryParse(name["HunBanCurrent".Length..], out var hunCurrentIndex))
        {
            return $"CurrentGame.CurrentHunBannedList[{hunCurrentIndex}].HeaderImageSingleColor";
        }

        if (name.StartsWith("SurGlobalBan", StringComparison.Ordinal)
            && int.TryParse(name["SurGlobalBan".Length..], out var surGlobalIndex))
        {
            return $"CurrentGame.SurTeam.GlobalBannedSurList[{surGlobalIndex}].HeaderImageSingleColor";
        }

        if (name.StartsWith("HunGlobalBan", StringComparison.Ordinal)
            && int.TryParse(name["HunGlobalBan".Length..], out var hunGlobalIndex))
        {
            return $"CurrentGame.HunTeam.GlobalBannedHunList[{hunGlobalIndex}].HeaderImageSingleColor";
        }

        return null;
    }

    private static void ConfigureLockableImage(string name, ImageFrontedControlConfig image)
    {
        if (name.StartsWith("SurBanCurrent", StringComparison.Ordinal)
            && int.TryParse(name["SurBanCurrent".Length..], out var surCurrentIndex))
        {
            image.Lockable = true;
            image.LockVisibleWhen = FrontedOverlayVisibilityMode.VisibleWhenFalse;
            image.LockVisibilityBindingPath = $"CanCurrentSurBannedList[{surCurrentIndex}]";
            image.LockImagePath ??= "Resources/CurrentBanLock.png";
        }
        else if (name.StartsWith("HunBanCurrent", StringComparison.Ordinal)
                 && int.TryParse(name["HunBanCurrent".Length..], out var hunCurrentIndex))
        {
            image.Lockable = true;
            image.LockVisibleWhen = FrontedOverlayVisibilityMode.VisibleWhenFalse;
            image.LockVisibilityBindingPath = $"CanCurrentHunBannedList[{hunCurrentIndex}]";
            image.LockImagePath ??= "Resources/CurrentBanLock.png";
        }
        else if (name.StartsWith("SurGlobalBan", StringComparison.Ordinal)
                 && int.TryParse(name["SurGlobalBan".Length..], out var surGlobalIndex))
        {
            image.Lockable = true;
            image.LockVisibleWhen = FrontedOverlayVisibilityMode.VisibleWhenFalse;
            image.LockVisibilityBindingPath = $"CanGlobalSurBannedList[{surGlobalIndex}]";
            image.LockImagePath ??= "Resources/GlobalBanLock.png";
        }
        else if (name.StartsWith("HunGlobalBan", StringComparison.Ordinal)
                 && int.TryParse(name["HunGlobalBan".Length..], out var hunGlobalIndex))
        {
            image.Lockable = true;
            image.LockVisibleWhen = FrontedOverlayVisibilityMode.VisibleWhenFalse;
            image.LockVisibilityBindingPath = $"CanGlobalHunBannedList[{hunGlobalIndex}]";
            image.LockImagePath ??= "Resources/GlobalBanLock.png";
        }
    }

    private static void ConfigurePickingBorderImage(LegacyControlBlueprint blueprint, ImageFrontedControlConfig image)
    {
        if (!string.Equals(blueprint.SourceWindow, "BpWindow", StringComparison.Ordinal)
            || !blueprint.TargetName.Contains("Pick", StringComparison.Ordinal))
        {
            return;
        }

        image.PickingBorderAvailable = true;
        image.PickingBorderName = blueprint.TargetName switch
        {
            "SurPick0" => "SurPickingBorder0",
            "SurPick1" => "SurPickingBorder1",
            "SurPick2" => "SurPickingBorder2",
            "SurPick3" => "SurPickingBorder3",
            "HunPick" => "HunPickingBorder",
            _ => image.PickingBorderName
        };
    }

    private static LegacyTextStyleDefaults GetLegacyTextDefaults(string? window, string? canvas, string name)
    {
        const string white = "#FFFFFFFF";
        const string notoSans = "Noto Sans";
        const string pop = "pack://application:,,,/Assets/Fonts/#华康POP1体W5";
        const string hanyi = "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简";

        var defaults = new LegacyTextStyleDefaults(
            "Center",
            "Center",
            "Center",
            null,
            notoSans,
            "Normal",
            white,
            16);

        if (string.Equals(window, "CutSceneWindow", StringComparison.Ordinal))
        {
            if (IsMajorPointName(name))
            {
                return defaults with { FontFamily = "Arial", FontSize = 28, FontWeight = "Bold" };
            }

            if (IsTeamNameForLegacyDefaults(name))
            {
                return defaults with { FontSize = 30, FontWeight = "Bold", TextWrapping = "WrapWithOverflow" };
            }

            if (name.StartsWith("SurId", StringComparison.Ordinal))
            {
                return defaults with { HorizontalAlignment = "Left", FontSize = 18 };
            }

            if (string.Equals(name, "HunId", StringComparison.Ordinal))
            {
                return defaults with { FontSize = 30 };
            }

            if (IsMapNameForLegacyDefaults(name))
            {
                return defaults with { FontFamily = hanyi, FontSize = 24 };
            }

            if (IsGameProgressForLegacyDefaults(name))
            {
                return defaults with { FontFamily = pop, FontSize = 22 };
            }
        }

        if (string.Equals(window, "BpWindow", StringComparison.Ordinal))
        {
            if (string.Equals(name, "Timer", StringComparison.Ordinal))
            {
                return defaults with { FontFamily = pop, FontSize = 46, FontWeight = "Bold" };
            }

            if (IsTeamNameForLegacyDefaults(name))
            {
                return defaults with { TextWrapping = "WrapWithOverflow" };
            }

            if (IsGameScoreName(name))
            {
                return defaults with { FontFamily = pop, FontSize = 26 };
            }

            if (IsMajorPointName(name))
            {
                return defaults with { FontSize = 20, FontWeight = "Medium" };
            }

            if (name.StartsWith("SurId", StringComparison.Ordinal) || string.Equals(name, "HunId", StringComparison.Ordinal))
            {
                return defaults with { HorizontalAlignment = "Left" };
            }
        }

        if (string.Equals(window, "ScoreSurWindow", StringComparison.Ordinal)
            || string.Equals(window, "ScoreHunWindow", StringComparison.Ordinal))
        {
            if (IsTeamNameForLegacyDefaults(name))
            {
                return defaults with { FontFamily = pop, FontSize = 32 };
            }

            if (IsGameScoreName(name))
            {
                return defaults with { FontFamily = pop, FontSize = 100 };
            }

            if (IsMajorPointName(name))
            {
                return defaults with { FontFamily = pop, FontSize = 38 };
            }
        }

        if (string.Equals(window, "ScoreGlobalWindow", StringComparison.Ordinal))
        {
            if (name.EndsWith("ScoreTotal", StringComparison.Ordinal))
            {
                return defaults with { FontFamily = pop, FontSize = 40, FontWeight = "Bold" };
            }

            return defaults with { FontFamily = pop, FontSize = 24 };
        }

        if (string.Equals(window, "GameDataWindow", StringComparison.Ordinal))
        {
            if (IsMajorPointName(name))
            {
                return defaults with { FontFamily = "Arial", FontSize = 30, FontWeight = "Bold" };
            }

            if (IsTeamNameForLegacyDefaults(name))
            {
                return defaults with { FontSize = 32, TextWrapping = "WrapWithOverflow" };
            }

            if (IsGameScoreName(name))
            {
                return defaults with { FontFamily = pop, FontSize = 80, FontWeight = "Bold" };
            }

            if (name.StartsWith("Header_", StringComparison.Ordinal))
            {
                return defaults with { FontSize = 16 };
            }

            if (name.StartsWith("Sur", StringComparison.Ordinal) || name.StartsWith("Hun", StringComparison.Ordinal))
            {
                return defaults with { FontFamily = pop, FontSize = 22 };
            }
        }

        if (string.Equals(window, "WidgetsWindow", StringComparison.Ordinal)
            && string.Equals(canvas, "BpOverViewCanvas", StringComparison.Ordinal))
        {
            if (name.EndsWith("TeamNameInOverview", StringComparison.Ordinal))
            {
                return defaults with { FontSize = 22, TextWrapping = "WrapWithOverflow" };
            }

            if (IsGameScoreName(name) || string.Equals(name, "RatioChar", StringComparison.Ordinal))
            {
                return defaults with { FontFamily = pop, FontSize = 50, FontWeight = "Bold" };
            }

            if (IsGameProgressForLegacyDefaults(name))
            {
                return defaults with { FontFamily = pop, FontSize = 22 };
            }
        }

        return defaults;
    }

    private static bool IsMajorPointName(string name) =>
        string.Equals(name, "SurTeamMajorPoint", StringComparison.Ordinal)
        || string.Equals(name, "HunTeamMajorPoint", StringComparison.Ordinal);

    private static bool IsGameScoreName(string name) =>
        string.Equals(name, "GameScoresSur", StringComparison.Ordinal)
        || string.Equals(name, "GameScoresHun", StringComparison.Ordinal);

    private static bool IsTeamNameForLegacyDefaults(string name) =>
        name.Contains("TeamName", StringComparison.Ordinal);

    private static bool IsMapNameForLegacyDefaults(string name) =>
        name.Contains("MapName", StringComparison.Ordinal);

    private static bool IsGameProgressForLegacyDefaults(string name) =>
        string.Equals(name, "GameProgress", StringComparison.Ordinal);

    private static string GetMapV2Key(string name)
    {
        return name switch
        {
            "Arms_Factory" => "ArmsFactory",
            "The_Red_Church" => "TheRedChurch",
            "Sacred_Heart_Hospital" => "SacredHeartHospital",
            "Leo_s_Memory" => "LeosMemory",
            "Moonlit_River_Park" => "MoonlitRiverPark",
            "Lakeside_Village" => "LakesideVillage",
            "Eversleeping_Town" => "EversleepingTown",
            "Chinatown" => "ChinaTown",
            "Darkwoods" => "Darkwoods",
            _ => name.Replace("_", string.Empty, StringComparison.Ordinal)
        };
    }

    private static bool TryMapLegacyLayoutFile(string fileName, out LegacyLayoutMapping mapping)
    {
        if (LegacyLayoutFileMap.TryGetValue(fileName, out mapping!))
        {
            return true;
        }

        mapping = LegacyLayoutMapping.Unsupported("Unknown", "Unknown");
        return false;
    }

    private static void ExtractZipSafely(string zipPath, string stagingRoot)
    {
        if (new FileInfo(zipPath).Length > FrontedLayoutLimits.MaxPackageArchiveBytes)
        {
            throw new InvalidDataException("PackageTooLarge");
        }

        var fullStagingRoot = EnsureTrailingSeparator(Path.GetFullPath(stagingRoot));
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > FrontedLayoutLimits.MaxPackageEntries)
        {
            throw new InvalidDataException("PackageTooManyEntries");
        }

        long totalUncompressedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > FrontedLayoutLimits.MaxPackageSingleEntryBytes)
            {
                throw new InvalidDataException("PackageEntryTooLarge");
            }

            totalUncompressedBytes += entry.Length;
            if (totalUncompressedBytes > FrontedLayoutLimits.MaxPackageExtractedBytes)
            {
                throw new InvalidDataException("PackageExtractedTooLarge");
            }

            var entryName = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(entryName)
                || Path.IsPathRooted(entryName)
                || entryName.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            {
                throw new InvalidDataException($"Unsafe zip entry: {entry.FullName}");
            }

            var destinationPath = Path.GetFullPath(Path.Combine(stagingRoot, entryName.Replace('/', Path.DirectorySeparatorChar)));
            if (!destinationPath.StartsWith(fullStagingRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Zip entry escaped staging directory: {entry.FullName}");
            }

            if (entryName.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: false);
        }
    }

    private static bool DetectLegacyPackage(string root)
    {
        return File.Exists(Path.Combine(root, "Config.json"))
               || Directory.Exists(Path.Combine(root, "CustomUi"))
               || Directory.Exists(Path.Combine(root, "FrontElementsConfig"));
    }

    private static string CreateResourceFileName(string originalName, string hash, string extension)
    {
        var safeBaseName = SafeFileNameChars.Replace(originalName, "-")
            .Replace("..", "-", StringComparison.Ordinal)
            .Trim('.', '-', '_');
        if (string.IsNullOrWhiteSpace(safeBaseName))
        {
            safeBaseName = "resource";
        }

        return $"{safeBaseName}-{hash[..12]}{extension.ToLowerInvariant()}";
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }

    private static string GetDefaultMinVersion()
    {
        var appVersion = NormalizeVersion(AppConstants.AppVersion);
        if (!string.IsNullOrWhiteSpace(appVersion))
        {
            return appVersion;
        }

        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
               ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
               ?? "3.0.0";
    }

    private static string? NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)
            || string.Equals(version, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = version.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOfAny(['+', '-']);
        if (metadataIndex > 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return Version.TryParse(normalized, out var parsed)
            ? parsed.ToString(parsed.Build >= 0 ? 3 : 2)
            : null;
    }

    private static void EnsureZipEntriesAreSafe(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (Path.IsPathRooted(name)
                || name.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            {
                throw new InvalidOperationException($"Unsafe zip entry generated: {entry.FullName}");
            }
        }
    }

    private static string ToZipPath(params string[] parts)
    {
        return string.Join("/", parts);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static FrontedLayoutPackageLegacyConvertResult Fail(
        string message,
        IReadOnlyList<string> infos,
        IReadOnlyList<string> diagnostics,
        IReadOnlyList<string> warnings)
    {
        return new FrontedLayoutPackageLegacyConvertResult
        {
            Success = false,
            ErrorMessage = message,
            Infos = infos.ToArray(),
            Diagnostics = diagnostics.ToArray(),
            Warnings = warnings.ToArray()
        };
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private sealed class ResourceConvertState(string packageId)
    {
        public string PackageId { get; } = packageId;

        public List<FrontedLayoutPackageResourceEntry> Resources { get; } = [];

        public Dictionary<string, string> ByFileName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> ByLegacyRelativePath { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string sourcePath, string uri, string relativePath, string kind, string sha256, string safeName)
        {
            Resources.Add(new FrontedLayoutPackageResourceEntry
            {
                Id = Path.GetFileNameWithoutExtension(safeName),
                Kind = kind,
                Path = relativePath,
                Uri = uri,
                Sha256 = sha256
            });

            ByFileName[Path.GetFileName(sourcePath)] = uri;
            ByLegacyRelativePath[Path.GetFileName(sourcePath)] = uri;
            ByLegacyRelativePath[$"CustomUi/{Path.GetFileName(sourcePath)}"] = uri;
        }
    }

    private sealed record ScoreGlobalCell(
        string ControlName,
        string Team,
        int Game,
        string Half,
        bool IsOvertime,
        ElementInfo Info);

    private readonly record struct LegacyLayoutKey(string SourceWindow, string SourceCanvas);

    private sealed record LegacyControlBlueprint
    {
        public string SourceWindow { get; init; } = string.Empty;

        public string SourceCanvas { get; init; } = string.Empty;

        public string LegacyName { get; init; } = string.Empty;

        public string TargetName { get; init; } = string.Empty;

        public string ControlType { get; init; } = string.Empty;

        public bool Required { get; init; }
    }

    private sealed record LegacyWindowDefaults(
        double WindowWidth,
        double WindowHeight,
        double CanvasWidth,
        double CanvasHeight,
        string? BackgroundImage);

    private sealed record LegacyTextStyleDefaults(
        string? HorizontalAlignment,
        string? VerticalAlignment,
        string? TextAlignment,
        string? TextWrapping,
        string? FontFamily,
        string? FontWeight,
        string? Color,
        double FontSize);

    private sealed record LegacyLayoutMapping(
        string SourceWindow,
        string SourceCanvas,
        string? TargetWindow,
        double? FixedCanvasWidth = null,
        double? FixedCanvasHeight = null)
    {
        public bool IsSupported => !string.IsNullOrWhiteSpace(TargetWindow);

        public string TargetLayoutPath => ToZipPath("FrontedLayouts", $"{TargetWindow}.json");

        public static LegacyLayoutMapping Unsupported(string sourceWindow, string sourceCanvas)
        {
            return new LegacyLayoutMapping(sourceWindow, sourceCanvas, null);
        }
    }

    private readonly record struct PaintedBounds(double MinX, double MinY, double MaxX, double MaxY)
    {
        public static PaintedBounds? From(IEnumerable<ElementInfo> elements)
        {
            var hasAny = false;
            var minX = double.PositiveInfinity;
            var minY = double.PositiveInfinity;
            var maxX = double.NegativeInfinity;
            var maxY = double.NegativeInfinity;

            foreach (var element in elements)
            {
                if (!element.Left.HasValue || !element.Top.HasValue)
                {
                    continue;
                }

                var width = element.Width.GetValueOrDefault();
                var height = element.Height.GetValueOrDefault();
                if (width < 0D || height < 0D)
                {
                    continue;
                }

                var left = element.Left.Value;
                var top = element.Top.Value;
                minX = Math.Min(minX, left);
                minY = Math.Min(minY, top);
                maxX = Math.Max(maxX, left + width);
                maxY = Math.Max(maxY, top + height);
                hasAny = true;
            }

            return hasAny ? new PaintedBounds(minX, minY, maxX, maxY) : null;
        }
    }
}

#pragma warning restore CS1591
