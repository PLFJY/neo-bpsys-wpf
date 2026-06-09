#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Converters;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
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

    private readonly string _builtInLayoutRoot;
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
        _builtInLayoutRoot = builtInLayoutRoot;
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

            var windowConfig = await LoadBuiltInWindowConfigAsync(mapping.TargetWindow!, cancellationToken);
            var config = windowConfig.ToCanvasConfig();
            ApplyFixedSplitCanvasSize(mapping, config);
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

            ApplyLegacyGeometry(file, mapping, config, infos, diagnostics, warnings);
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

    private async Task<FrontedWindowConfig> LoadBuiltInWindowConfigAsync(
        string window,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(_builtInLayoutRoot, $"{window}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Built-in v3 layout was not found: {window}", path);
        }

        if (new FileInfo(path).Length > FrontedLayoutLimits.MaxLayoutJsonBytes)
        {
            throw new InvalidDataException("LayoutJsonTooLarge");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<FrontedWindowConfig>(json, _jsonOptions)
               ?? throw new InvalidOperationException($"Built-in v3 layout could not be read: {window}");
    }

    private static void ApplyLegacyGeometry(
        string legacyFile,
        LegacyLayoutMapping mapping,
        FrontedCanvasConfig config,
        ICollection<string> infos,
        ICollection<string> diagnostics,
        ICollection<string> warnings)
    {
        Dictionary<string, ElementInfo>? legacyPositions;
        try
        {
            if (new FileInfo(legacyFile).Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                warnings.Add($"Legacy layout file is too large and was skipped: {Path.GetFileName(legacyFile)}");
                return;
            }

            legacyPositions = JsonSerializer.Deserialize<Dictionary<string, ElementInfo>>(
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
            return;
        }

        if (legacyPositions is null)
        {
            return;
        }

        var consumedControls = new HashSet<string>(StringComparer.Ordinal);
        ApplyScoreGlobalAggregateGeometry(mapping.SourceWindow, mapping.SourceCanvas, config, legacyPositions, consumedControls, infos, diagnostics);
        ConsumeLegacyLockOverlayGeometry(mapping.SourceWindow, mapping.SourceCanvas, config, legacyPositions, consumedControls, diagnostics);

        foreach (var (controlName, legacy) in legacyPositions)
        {
            if (consumedControls.Contains(controlName))
            {
                continue;
            }

            if (!LegacyFrontedControlNameMapper.TryResolve(
                    mapping.SourceWindow,
                    mapping.SourceCanvas,
                    controlName,
                    config.Controls,
                    out var resolvedName,
                    out var usedFuzzyMatch)
                || !config.Controls.TryGetValue(resolvedName, out var control))
            {
                var candidates = LegacyFrontedControlNameMapper.GetClosestCandidates(controlName, config.Controls.Keys);
                warnings.Add(candidates.Count > 0
                    ? $"Legacy control geometry ignored because no v3 control matches: {mapping.SourceWindow}/{mapping.SourceCanvas}/{controlName}. Closest candidates: {string.Join(", ", candidates)}"
                    : $"Legacy control geometry ignored because no v3 control matches: {mapping.SourceWindow}/{mapping.SourceCanvas}/{controlName}");
                continue;
            }

            if (usedFuzzyMatch)
            {
                infos.Add($"Legacy control geometry fuzzy-matched: {mapping.SourceWindow}/{mapping.SourceCanvas}/{controlName} -> {resolvedName}");
            }

            ApplyGeometry(control, legacy);
        }

        AddBoundsDiagnostics(mapping, legacyPositions.Values, warnings);
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

            if (legacyPositions.ContainsKey(targetName))
            {
                diagnostics.Add("Legacy lock overlay geometry was folded into lockable control and separate geometry is not representable.");
                continue;
            }

            ApplyGeometry(target, legacy);
            diagnostics.Add($"Legacy lock overlay fallback geometry applied: {window}/{canvas}/{legacyName} -> {targetName}");
        }
    }

    private static bool TryMapLegacyLockOverlayName(string legacyName, out string targetName)
    {
        targetName = string.Empty;
        var match = LegacyCurrentBanLockOverlayName.Match(legacyName);
        if (!match.Success)
        {
            return false;
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
                    $"Legacy window size differs from built-in v3 canvas size: {mapping.TargetWindow} Window={windowSize.Width}x{windowSize.Height}, Canvas={target.CanvasSettings.CanvasWidth}x{target.CanvasSettings.CanvasHeight}.");
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

    private static void ApplyFixedSplitCanvasSize(LegacyLayoutMapping mapping, FrontedCanvasConfig config)
    {
        if (!mapping.FixedCanvasWidth.HasValue || !mapping.FixedCanvasHeight.HasValue || !mapping.IsSupported)
        {
            return;
        }

        config.CanvasWidth = mapping.FixedCanvasWidth.Value;
        config.CanvasHeight = mapping.FixedCanvasHeight.Value;
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
