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
using static neo_bpsys_wpf.Core.Services.FrontedLayout.LegacyConvertMessageHelper;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 旧版前台布局包转换器，负责将旧版 .bpui 格式的布局包迁移为 v3 窗口化布局格式。
/// 支持从 .bpui 压缩包、本地 AppData 目录或指定目录的旧版布局进行转换。
/// </summary>
public sealed class FrontedLayoutPackageLegacyConverter : IFrontedLayoutPackageLegacyConverter
{
    private const string ManifestFileName = "manifest.json";
    private const string DefaultOpaqueBackgroundColor = "#FF00FF00";

    private static readonly Regex SafeFileNameChars = new("[^A-Za-z0-9._-]+", RegexOptions.Compiled);

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

    internal static IReadOnlyCollection<string> LegacyLayoutFileNames => LegacyLayoutFileMap.Keys;

    internal static bool IsKnownLegacyLayoutFileName(string fileName) =>
        LegacyLayoutFileMap.ContainsKey(fileName);

    private static readonly IReadOnlyDictionary<LegacyLayoutKey, IReadOnlyList<LegacyControlBlueprint>> LegacyControlBlueprints =
        CreateLegacyControlBlueprints();

    private static readonly IReadOnlyDictionary<string, LegacyScoreGlobalCellBlueprint> LegacyScoreGlobalCells =
        CreateLegacyScoreGlobalCellBlueprints();

    /// <summary>
    /// 旧版控件名 → 蓝图 <see cref="LegacyControlBlueprint.LegacyName"/> 的别名表。
    /// 仅在直接查找失败时使用，用于兼容早期 1.x 包中与蓝图 LegacyName 不一致但语义等价的控件命名，
    /// 不影响已匹配蓝图 LegacyName 的既有包行为。
    /// </summary>
    private static readonly IReadOnlyDictionary<LegacyLayoutKey, IReadOnlyDictionary<string, string>> LegacyBlueprintNameAliases =
        new Dictionary<LegacyLayoutKey, IReadOnlyDictionary<string, string>>
        {
            [new LegacyLayoutKey("ScoreGlobalWindow", "BaseCanvas")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // 1.x 包使用 v3 目标名 HomeTeamName 作为旧版控件名，蓝图定义的 LegacyName 为 MainTeamName。
                ["HomeTeamName"] = "MainTeamName",
                // 1.x 包中 AwayTeamTeamName（重复 Team 前缀）对应蓝图的 AwayTeamName。
                ["AwayTeamTeamName"] = "AwayTeamName"
            },
            [new LegacyLayoutKey("ScoreSurWindow", "BaseCanvas")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // 1.x 包使用 MinorPointsSur 表示求生者小幅分，蓝图定义的 LegacyName 为 GameScoresSur。
                ["MinorPointsSur"] = "GameScoresSur"
            },
            [new LegacyLayoutKey("ScoreHunWindow", "BaseCanvas")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // 1.x 包使用 MinorPointsHun 表示监管者小幅分，蓝图定义的 LegacyName 为 GameScoresHun。
                ["MinorPointsHun"] = "GameScoresHun"
            },
            [new LegacyLayoutKey("WidgetsWindow", "BpOverViewCanvas")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // 1.x 包在 BpOverview 画布中同样使用 MinorPointsSur/MinorPointsHun 表示小幅分。
                ["MinorPointsSur"] = "GameScoresSur",
                ["MinorPointsHun"] = "GameScoresHun"
            }
        };

    /// <summary>
    /// 尝试将旧版控件名解析为蓝图 <see cref="LegacyControlBlueprint.LegacyName"/>。
    /// 仅在直接查找失败时调用，用于兼容早期 1.x 包中与蓝图 LegacyName 不一致但语义等价的控件命名。
    /// </summary>
    /// <param name="sourceWindow">旧版窗口类型名。</param>
    /// <param name="sourceCanvas">旧版画布名。</param>
    /// <param name="controlName">旧版布局文件中出现的控件名。</param>
    /// <param name="blueprintLegacyName">解析后的蓝图 LegacyName。</param>
    /// <returns>是否找到别名映射。</returns>
    private static bool TryResolveBlueprintLegacyNameAlias(
        string sourceWindow,
        string sourceCanvas,
        string controlName,
        out string blueprintLegacyName)
    {
        blueprintLegacyName = controlName;
        if (LegacyBlueprintNameAliases.TryGetValue(new LegacyLayoutKey(sourceWindow, sourceCanvas), out var aliases)
            && aliases.TryGetValue(controlName, out var alias))
        {
            blueprintLegacyName = alias;
            return true;
        }

        return false;
    }

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

    /// <summary>
    /// 使用默认内置布局根路径初始化转换器。
    /// </summary>
    /// <param name="packageImporter">包导入器。</param>
    /// <param name="validator">布局校验器。</param>
    /// <param name="logger">日志记录器。</param>
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

    /// <summary>
    /// 使用自定义内置布局根路径和临时路径初始化转换器。
    /// </summary>
    /// <param name="builtInLayoutRoot">内置布局根目录。</param>
    /// <param name="tempRoot">临时文件根目录。</param>
    /// <param name="packageImporter">包导入器（可选）。</param>
    /// <param name="validator">布局校验器（可选）。</param>
    /// <param name="logger">日志记录器（可选）。</param>
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

    /// <summary>
    /// 执行旧版布局包到 v3 格式的转换。
    /// </summary>
    /// <param name="request">转换请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>转换结果，包含转换后的布局、消息和资源信息。</returns>
    public async Task<FrontedLayoutPackageLegacyConvertResult> ConvertAsync(
        FrontedLayoutPackageLegacyConvertRequest request,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<FrontedLayoutPackageLegacyConvertMessage>();
        var extractionRoot = Path.Combine(_tempRoot, "extract", Guid.NewGuid().ToString("N"));

        try
        {
            if (string.IsNullOrWhiteSpace(request.LegacyPackagePath) || !File.Exists(request.LegacyPackagePath))
            {
                return Fail("Legacy package archive was not found.", messages);
            }

            var packageId = string.IsNullOrWhiteSpace(request.PackageId)
                ? $"converted.legacy.{DateTime.UtcNow:yyyyMMddHHmm}"
                : request.PackageId.Trim();
            if (!FrontedLayoutPackageManager.IsSafePackageId(packageId)
                || string.Equals(packageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(packageId, FrontedLayoutPackageManager.LocalPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("PackageId is invalid.", messages);
            }

            Directory.CreateDirectory(extractionRoot);
            ExtractZipSafely(request.LegacyPackagePath, extractionRoot);
            if (!DetectLegacyPackage(extractionRoot))
            {
                return Fail("Archive is not a legacy .bpui package.", messages);
            }

            return await ConvertLegacyInputAsync(
                new LegacyBpuiDirectoryInputSource(extractionRoot),
                request,
                createArchive: true,
                replaceExisting: false,
                cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid legacy bpui archive.");
            return Fail($"Invalid legacy package archive: {ex.Message}", messages);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert legacy bpui package.");
            return Fail(ex.Message, messages);
        }
        finally
        {
            TryDeleteDirectory(extractionRoot);
        }
    }

    internal Task<FrontedLayoutPackageLegacyConvertResult> ConvertLocalAppDataAsync(
        string appDataRoot,
        FrontedLayoutPackageLegacyConvertRequest request,
        CancellationToken cancellationToken = default)
    {
        return ConvertLegacyInputAsync(
            new LegacyLocalAppDataInputSource(appDataRoot),
            request,
            createArchive: false,
            replaceExisting: true,
            cancellationToken);
    }

    private async Task<FrontedLayoutPackageLegacyConvertResult> ConvertLegacyInputAsync(
        ILegacyFrontendInputSource source,
        FrontedLayoutPackageLegacyConvertRequest request,
        bool createArchive,
        bool replaceExisting,
        CancellationToken cancellationToken)
    {
        var messages = new List<FrontedLayoutPackageLegacyConvertMessage>();
        var stagingRoot = Path.Combine(_tempRoot, "staging", Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(_tempRoot, "converted", $"{Guid.NewGuid():N}.bpui");
        try
        {
            var packageId = string.IsNullOrWhiteSpace(request.PackageId)
                ? $"converted.legacy.{DateTime.UtcNow:yyyyMMddHHmm}"
                : request.PackageId.Trim();
            if (!FrontedLayoutPackageManager.IsSafePackageId(packageId)
                || string.Equals(packageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(packageId, FrontedLayoutPackageManager.LocalPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("PackageId is invalid.", messages);
            }

            Directory.CreateDirectory(stagingRoot);
            var resourceState = CopyCustomUiResources(source.CustomUiRoot, stagingRoot, packageId, messages);
            var manifest = CreateManifest(request, packageId);
            manifest.Content.Resources = resourceState.Resources;

            var configValueMap = ReadFrontendConfigValueMap(source, resourceState, messages);
            var legacyPropertySet = ReadLegacyPropertySet(source, messages);
            var legacySettings = ReadLegacySettings(source, messages);
            var layoutEntries = await ConvertFrontElementsConfigsAsync(
                source,
                stagingRoot,
                manifest,
                resourceState,
                configValueMap,
                legacySettings,
                legacyPropertySet,
                messages,
                cancellationToken);
            if (layoutEntries == 0)
            {
                return Fail("No mappable legacy layout files were converted.", messages);
            }

            await File.WriteAllTextAsync(
                Path.Combine(stagingRoot, ManifestFileName),
                JsonSerializer.Serialize(manifest, _jsonOptions),
                cancellationToken);

            var result = new FrontedLayoutPackageLegacyConvertResult
            {
                Success = true,
                LayoutCount = manifest.Content.Layouts.Count,
                ResourceCount = manifest.Content.Resources.Count
            };

            if (createArchive)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                ZipFile.CreateFromDirectory(stagingRoot, outputPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                EnsureZipEntriesAreSafe(outputPath);
                result.ConvertedPackagePath = outputPath;
            }

            FrontedLayoutPackageLegacyConvertResult.PopulateFromMessages(result, messages);
            if (request.InstallAfterConvert)
            {
                if (_packageImporter is null)
                {
                    return Fail("Package importer is unavailable.", messages);
                }

                var importResult = createArchive
                    ? await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
                    {
                        PackagePath = outputPath,
                        ReplaceExisting = replaceExisting,
                        ActivateAfterImport = request.ActivateAfterInstall
                    }, cancellationToken)
                    : await _packageImporter.ImportDirectoryAsync(
                        stagingRoot,
                        replaceExisting,
                        request.ActivateAfterInstall,
                        cancellationToken);
                result.Success = importResult.Success;
                result.InstalledPackageId = importResult.Success ? importResult.PackageId : null;
                result.ErrorMessage = importResult.ErrorMessage;
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to convert legacy frontend input.");
            return Fail(ex.Message, messages);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private async Task<int> ConvertFrontElementsConfigsAsync(
        ILegacyFrontendInputSource source,
        string stagingRoot,
        FrontedLayoutPackageManifest manifest,
        ResourceConvertState resourceState,
        IReadOnlyDictionary<string, string> configValueMap,
        LegacySettings? legacySettings,
        IReadOnlySet<string> legacyPropertySet,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages,
        CancellationToken cancellationToken)
    {
        var convertedCount = 0;
        var files = source.EnumerateLegacyLayoutFiles().ToArray();
        if (files.Length == 0 && source is LegacyBpuiDirectoryInputSource)
        {
            messages.Add(Error(CodeFrontElementsFolderMissing));
            return 0;
        }

        if (source is LegacyLocalAppDataInputSource)
        {
            var existing = files.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var expected in LegacyLayoutFileMap.Keys.Where(file => !existing.Contains(file)))
            {
                messages.Add(Warning(CodeLayoutFileReadFailed,
                    Args(new { FileName = expected, Reason = "File is missing." })));
            }
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(file);
            if (!TryMapLegacyLayoutFile(fileName, out var mapping))
            {
                messages.Add(Warning(CodeUnknownLayoutFileSkipped,
                    Args(new { FileName = fileName })));
                continue;
            }

            if (!mapping.IsSupported)
            {
                messages.Add(Compat(CodeMapBpV1Skipped,
                    Args(new { SourceWindow = "WidgetsWindow", SourceCanvas = "MapBpCanvas" })));
                continue;
            }

            var legacyPositions = ReadLegacyPositions(source, file, messages);
            if (legacyPositions is null)
            {
                continue;
            }

            var windowConfig = CreateLegacyWindowConfig(mapping);
            var config = FrontedWindowConfigCanvasAdapter.ToCanvasConfig(windowConfig);
            BuildLegacyBlueprintControls(
                mapping,
                config,
                legacyPositions,
                messages);
            ApplyFrontendConfigValues(config, mapping, configValueMap, messages);
            ApplyLegacyTextStyleOverrides(config, mapping, legacySettings, messages);

            RewriteKnownResourceStrings(config, resourceState);
            config.Version = 3;
            ApplyCanvasConfig(windowConfig, config);
            ApplyLegacyWindowSettings(windowConfig, mapping, legacySettings, legacyPropertySet, messages);

            var validationMessages = _validator.Validate(
                mapping.TargetWindow!,
                FrontedLayoutConstants.BaseCanvasName,
                FrontedWindowConfigCanvasAdapter.ToCanvasConfig(windowConfig));
            var validationErrors = validationMessages
                .Where(message => message.Severity == Models.FrontedLayout.Designer.FrontedLayoutValidationSeverity.Error)
                .ToArray();
            if (validationErrors.Length > 0)
            {
                messages.Add(Warning(CodeLayoutValidationError,
                    Args(new
                    {
                        TargetWindow = mapping.TargetWindow,
                        CanvasName = FrontedLayoutConstants.BaseCanvasName,
                        Details = string.Join("; ", validationErrors.Select(error => error.Message))
                    })));
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
        ILegacyFrontendInputSource source,
        string legacyFile,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        try
        {
            using var stream = source.OpenLegacyLayoutFile(legacyFile);
            if (stream.Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                messages.Add(Warning(CodeLayoutFileTooLargeSkipped,
                    Args(new { FileName = Path.GetFileName(legacyFile) })));
                return null;
            }

            return JsonSerializer.Deserialize<Dictionary<string, ElementInfo>>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    MaxDepth = FrontedLayoutLimits.MaxJsonDepth
                });
        }
        catch (Exception ex)
        {
            messages.Add(Warning(CodeLayoutFileReadFailed,
                Args(new { FileName = Path.GetFileName(legacyFile), Reason = ex.Message })));
            return null;
        }
    }

    private void BuildLegacyBlueprintControls(
        LegacyLayoutMapping mapping,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (legacyPositions is null)
        {
            return;
        }

        var key = new LegacyLayoutKey(mapping.SourceWindow, mapping.SourceCanvas);
        if (!LegacyControlBlueprints.TryGetValue(key, out var blueprints))
        {
            messages.Add(Warning(CodeNoBlueprintForLayout,
                Args(new { SourceWindow = mapping.SourceWindow, SourceCanvas = mapping.SourceCanvas })));
            return;
        }

        foreach (var blueprint in blueprints)
        {
            if (blueprint.Status is not LegacyControlBlueprintStatus.Mapped
                and not LegacyControlBlueprintStatus.Aggregated)
            {
                continue;
            }

            if (!blueprint.Required && !legacyPositions.ContainsKey(blueprint.LegacyName))
            {
                continue;
            }

            var control = CreateBlueprintControl(blueprint);
            if (control is null)
            {
                messages.Add(Warning(CodeControlCreateFailed,
                    Args(new
                    {
                        SourceWindow = mapping.SourceWindow,
                        SourceCanvas = mapping.SourceCanvas,
                        LegacyName = blueprint.LegacyName,
                        TargetName = blueprint.TargetName
                    })));
                continue;
            }

            config.Controls[blueprint.TargetName] = control;
        }

        var consumedControls = new HashSet<string>(StringComparer.Ordinal);
        ApplyScoreGlobalAggregateGeometry(mapping.SourceWindow, mapping.SourceCanvas, config, legacyPositions, consumedControls, messages);
        ConsumeExplicitFoldedGeometry(mapping.SourceWindow, mapping.SourceCanvas, blueprints, config, legacyPositions, consumedControls, messages);
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
                if (TryResolveBlueprintLegacyNameAlias(mapping.SourceWindow, mapping.SourceCanvas, controlName, out var aliasName)
                    && blueprintsByLegacyName.TryGetValue(aliasName, out mappedBlueprints))
                {
                    messages.Add(Info(CodeControlGeometryFuzzyMatched,
                        Args(new
                        {
                            SourceWindow = mapping.SourceWindow,
                            SourceCanvas = mapping.SourceCanvas,
                            LegacyName = controlName,
                            ResolvedLegacyName = aliasName
                        })));
                }
                else
                {
                    throw new InvalidDataException(
                        $"Legacy control geometry is not listed in the explicit legacy blueprint map: {mapping.SourceWindow}/{mapping.SourceCanvas}/{controlName}");
                }
            }

            foreach (var blueprint in mappedBlueprints)
            {
                if (blueprint.Status is LegacyControlBlueprintStatus.Folded
                    or LegacyControlBlueprintStatus.Aggregated
                    or LegacyControlBlueprintStatus.Unsupported
                    or LegacyControlBlueprintStatus.RemovedWithReason)
                {
                    continue;
                }

                if (config.Controls.TryGetValue(blueprint.TargetName, out var control))
                {
                    ApplyGeometry(control, legacy);
                }
            }
        }

        AddBoundsDiagnostics(mapping, legacyPositions.Values, messages);
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
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
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
            messages);
        ApplyScoreGlobalRowGeometry(
            "Away",
            "AwayGlobalScoreRow",
            config,
            legacyPositions,
            consumedControls,
            messages);
    }

    private static void ApplyScoreGlobalRowGeometry(
        string teamPrefix,
        string targetControlName,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ISet<string> consumedControls,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
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
            messages.Add(Info(CodeOvertimeScoreCellsAggregated));
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
        messages.Add(Info(CodeGlobalScoreCellsAggregated,
            Args(new { Team = teamPrefix, TargetName = targetControlName })));
        if (approximate)
        {
            messages.Add(Info(CodeIrregularCellSpacingApproximated,
                Args(new { Team = teamPrefix, TargetName = targetControlName })));
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
        if (!LegacyScoreGlobalCells.TryGetValue(controlName, out var blueprint))
        {
            team = string.Empty;
            game = 0;
            half = string.Empty;
            isOvertime = false;
            return false;
        }

        team = blueprint.Team;
        game = blueprint.Game;
        half = blueprint.Half;
        isOvertime = blueprint.IsOvertime;
        return true;
    }

    private static void ConsumeExplicitFoldedGeometry(
        string window,
        string canvas,
        IReadOnlyList<LegacyControlBlueprint> blueprints,
        FrontedCanvasConfig config,
        IReadOnlyDictionary<string, ElementInfo> legacyPositions,
        ISet<string> consumedControls,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        foreach (var blueprint in blueprints.Where(blueprint => blueprint.Status == LegacyControlBlueprintStatus.Folded))
        {
            if (!legacyPositions.ContainsKey(blueprint.LegacyName))
            {
                continue;
            }

            consumedControls.Add(blueprint.LegacyName);
            messages.Add(Info(CodeFoldedControlConsumed,
                Args(new { SourceWindow = window, SourceCanvas = canvas, LegacyName = blueprint.LegacyName, TargetName = blueprint.TargetName })));
            messages.Add(Info(CodeLockOverlayGeometryConsumed,
                Args(new { LegacyName = blueprint.LegacyName, TargetName = blueprint.TargetName })));
            if (string.IsNullOrWhiteSpace(blueprint.TargetName)
                || !config.Controls.TryGetValue(blueprint.TargetName, out var target))
            {
                messages.Add(Info(CodeFoldedControlNoTarget,
                    Args(new { SourceWindow = window, SourceCanvas = canvas, LegacyName = blueprint.LegacyName })));
                continue;
            }

            if (target is ImageFrontedControlConfig image)
            {
                ApplyImageSpecialProperties(image, blueprint);
            }

            messages.Add(Info(CodeFoldedGeometryNotRepresentable));
        }
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
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
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
            messages.Add(Warning(CodeBpOverviewOutOfBounds));
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
        string? customUiRoot,
        string stagingRoot,
        string packageId,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        var state = new ResourceConvertState(packageId);
        if (string.IsNullOrWhiteSpace(customUiRoot) || !Directory.Exists(customUiRoot))
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
            messages.Add(Info(CodeResourceCopied,
                Args(new { FileName = Path.GetFileName(fullFile) })));
        }

        return state;
    }

    private static IReadOnlyDictionary<string, string> ReadFrontendConfigValueMap(
        ILegacyFrontendInputSource source,
        ResourceConvertState resourceState,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(source.ConfigPath))
        {
            return result;
        }

        JsonNode? root;
        try
        {
            using var stream = source.OpenConfig();
            if (stream.Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                messages.Add(Warning(CodeConfigJsonTooLarge));
                return result;
            }

            root = JsonNode.Parse(
                stream,
                nodeOptions: null,
                documentOptions: new JsonDocumentOptions { MaxDepth = FrontedLayoutLimits.MaxJsonDepth });
        }
        catch (Exception ex)
        {
            messages.Add(Warning(CodeConfigJsonReadFailed,
                Args(new { Reason = ex.Message })));
            return result;
        }

        AddMappedImage(root, "BpWindowSettings", "BgImageUri", "BpWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "CutSceneWindowSettings", "BgUri", "CutSceneWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "ScoreWindowSettings", "SurScoreBgImageUri", "ScoreSurWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "ScoreWindowSettings", "HunScoreBgImageUri", "ScoreHunWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "ScoreWindowSettings", "GlobalScoreBgImageUri", "ScoreGlobalWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "ScoreWindowSettings", "GlobalScoreBgImageUriBo3", "ScoreGlobalWindow/BaseCanvas/BoModeStates/Bo3/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "GameDataWindowSettings", "BgImageUri", "GameDataWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpBgUri", "WidgetsWindow/MapBpCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "WidgetsWindowSettings", "BpOverviewBgUri", "BpOverviewWindow/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpV2BgUri", "MapV2Window/BaseCanvas/BackgroundImage", resourceState, result, messages);
        AddMappedImage(root, "BpWindowSettings", "CurrentBanLockImageUri", "BpWindow/BaseCanvas/CurrentBanLockImage", resourceState, result, messages);
        AddMappedImage(root, "BpWindowSettings", "GlobalBanLockImageUri", "BpWindow/BaseCanvas/GlobalBanLockImage", resourceState, result, messages);
        AddMappedImage(root, "BpWindowSettings", "PickingBorderImageUri", "BpWindow/BaseCanvas/PickingBorderImage", resourceState, result, messages);
        AddMappedValue(root, "BpWindowSettings", "PickingBorderColor", "BpWindow/BaseCanvas/PickingBorderColor", result);
        AddMappedImage(root, "WidgetsWindowSettings", "CurrentBanLockImageUri", "BpOverviewWindow/BaseCanvas/CurrentBanLockImage", resourceState, result, messages);
        AddMappedImage(root, "WidgetsWindowSettings", "GlobalBanLockImageUri", "BpOverviewWindow/BaseCanvas/GlobalBanLockImage", resourceState, result, messages);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpV2PickingBorderImageUri", "MapV2Window/BaseCanvas/MapBpV2PickingBorderImage", resourceState, result, messages);
        AddMappedValue(root, "WidgetsWindowSettings", "MapBpV2_PickingBorderColor", "MapV2Window/BaseCanvas/MapBpV2PickingBorderColor", result);

        foreach (var ignored in EnumeratePotentialFrontendImageFields(root)
                     .Where(field => !KnownConfigImageFields.Contains(field, StringComparer.Ordinal)))
        {
            messages.Add(Info(CodeLegacyFieldIgnored,
                Args(new { Field = ignored })));
        }

        return result;
    }

    private LegacySettings? ReadLegacySettings(
        ILegacyFrontendInputSource source,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (!File.Exists(source.ConfigPath))
        {
            return null;
        }

        try
        {
            using var stream = source.OpenConfig();
            if (stream.Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                messages.Add(Warning(CodeConfigJsonTooLarge));
                return null;
            }

            return JsonSerializer.Deserialize<LegacySettings>(stream, _jsonOptions);
        }
        catch (Exception ex)
        {
            messages.Add(Info(CodeTextSettingsReadFailed,
                Args(new { Reason = ex.Message })));
            return null;
        }
    }

    private static IReadOnlySet<string> ReadLegacyPropertySet(
        ILegacyFrontendInputSource source,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (!File.Exists(source.ConfigPath))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            using var stream = source.OpenConfig();
            if (stream.Length > FrontedLayoutLimits.MaxLegacyConfigBytes)
            {
                messages.Add(Warning(CodeConfigJsonTooLarge));
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var root = JsonNode.Parse(stream) as JsonObject;
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
            messages.Add(Info(CodeWindowSettingsInspectFailed,
                Args(new { Reason = ex.Message })));
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static void ApplyLegacyWindowSettings(
        FrontedWindowConfig target,
        LegacyLayoutMapping mapping,
        LegacySettings? legacySettings,
        IReadOnlySet<string> legacyPropertySet,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
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
                messages.Add(Info(CodeWindowSizeDiffersFromCanvas,
                    Args(new
                    {
                        TargetWindow = mapping.TargetWindow,
                        WindowSize = $"{windowSize.Width}x{windowSize.Height}",
                        CanvasSize = $"{target.CanvasSettings.CanvasWidth}x{target.CanvasSettings.CanvasHeight}"
                    })));
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
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
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

        messages.Add(Info(CodeResourceMissing,
            Args(new { Field = field, Value = value })));
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
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
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
            messages.Add(Info(CodeBo3GlobalScoreBackgroundMapped));
        }

        if (mapping.TargetWindow == "BpWindow")
        {
            foreach (var blueprint in GetMappedControlBlueprints(mapping))
            {
                if (!config.Controls.TryGetValue(blueprint.TargetName ?? blueprint.LegacyName, out var rawControl)
                    || rawControl is not ImageFrontedControlConfig control)
                {
                    continue;
                }

                ApplyImageResourceOverride(control, blueprint, prefix, valueMap, messages);
            }
        }

        if (mapping.TargetWindow == "BpOverviewWindow")
        {
            foreach (var blueprint in GetMappedControlBlueprints(mapping))
            {
                if (!config.Controls.TryGetValue(blueprint.TargetName ?? blueprint.LegacyName, out var rawControl)
                    || rawControl is not ImageFrontedControlConfig control)
                {
                    continue;
                }

                ApplyImageResourceOverride(control, blueprint, prefix, valueMap, messages);
            }
        }

        if (mapping.TargetWindow == "MapV2Window")
        {
            foreach (var blueprint in GetMappedControlBlueprints(mapping))
            {
                if (!config.Controls.TryGetValue(blueprint.TargetName ?? blueprint.LegacyName, out var rawControl)
                    || rawControl is not MapV2DisplayControlConfig control)
                {
                    continue;
                }

                if (blueprint.SpecialProperties.TryGetValue("PickingBorderImageResourceSourceKey", out var imageKey)
                    && valueMap.TryGetValue($"{prefix}{imageKey}", out var borderUri))
                {
                    control.PickingBorderImagePath = borderUri;
                }

                if (blueprint.SpecialProperties.TryGetValue("PickingBorderColorResourceSourceKey", out var colorKey)
                    && valueMap.TryGetValue($"{prefix}{colorKey}", out var borderColor))
                {
                    control.PickingBorderFillColor = borderColor;
                }
            }
        }
    }

    private static IEnumerable<LegacyControlBlueprint> GetMappedControlBlueprints(LegacyLayoutMapping mapping)
    {
        if (!LegacyControlBlueprints.TryGetValue(
                new LegacyLayoutKey(mapping.SourceWindow, mapping.SourceCanvas),
                out var blueprints))
        {
            return [];
        }

        return blueprints.Where(blueprint => blueprint.Status == LegacyControlBlueprintStatus.Mapped);
    }

    private static void ApplyImageResourceOverride(
        ImageFrontedControlConfig control,
        LegacyControlBlueprint blueprint,
        string prefix,
        IReadOnlyDictionary<string, string> valueMap,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (blueprint.ResourceSourceKey is null)
        {
            return;
        }

        var key = $"{prefix}{blueprint.ResourceSourceKey}";
        if (!valueMap.TryGetValue(key, out var uri))
        {
            return;
        }

        switch (blueprint.ResourceSourceKey)
        {
            case "CurrentBanLockImage":
            case "GlobalBanLockImage":
                if (control.Lockable)
                {
                    control.LockImagePath = uri;
                    messages.Add(Info(CodeLockImageMapped,
                        Args(new { Key = key })));
                }

                break;
            case "PickingBorderImage":
                if (control.PickingBorderAvailable)
                {
                    control.PickingBorderImagePath = uri;
                    messages.Add(Info(CodePickingBorderImageMapped,
                        Args(new { Key = key })));
                }

                break;
        }
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
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Image("SurTeamLogo", "Image", "CurrentGame.SurTeam.Logo", 615, 670, 50, 50, cornerRadius: 8, stretch: "Fill"),
            Text("SurTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentSurTeamMajorText", "BpWindow.MajorPoints", 607, 776),
            Text("SurTeamName", "Text", "CurrentGame.SurTeam.Name", "BpWindow.TeamName", 580, 720, 120, null, textWrapping: "WrapWithOverflow"),
            Text("GameScoresSur", "Text", "CurrentGame.MatchScore.CurrentSurTeamMinorScoreText", "BpWindow.GameScores", 622, 746, 36, 30),
            Text("Timer", "Text", "RemainingSeconds", "BpWindow.Timer", 671, 672, 100, null, zIndex: 1),
            Text("GameScoresHun", "Text", "CurrentGame.MatchScore.CurrentHunTeamMinorScoreText", "BpWindow.GameScores", 784, 746, 36, 30),
            Text("HunTeamName", "Text", "CurrentGame.HunTeam.Name", "BpWindow.TeamName", 742, 720, 120, null, textWrapping: "WrapWithOverflow"),
            Text("HunTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentHunTeamMajorText", "BpWindow.MajorPoints", 770, 776),
            Image("HunTeamLogo", "Image", "CurrentGame.HunTeam.Logo", 776, 670, 50, 50, cornerRadius: 8, stretch: "Fill"),
            Image("HunBanCurrent0", "Image", "CurrentGame.CurrentHunBannedList[0].HeaderImageSingleColor", 11.5, 562.5, 44.5, 44.5, specialProperties: CurrentBanLock("Hun", 0)),
            Image("HunBanCurrent1", "Image", "CurrentGame.CurrentHunBannedList[1].HeaderImageSingleColor", 64, 562.5, 44.5, 44.5, specialProperties: CurrentBanLock("Hun", 1)),
            Folded("HunBanCurrentLock0", "HunBanCurrent0", "Folded into HunBanCurrent0 lock overlay metadata."),
            Folded("HunBanCurrentLock1", "HunBanCurrent1", "Folded into HunBanCurrent1 lock overlay metadata."),
            Image("SurBanCurrent0", "Image", "CurrentGame.CurrentSurBannedList[0].HeaderImageSingleColor", 1226.5, 563, 44.5, 44.5, specialProperties: CurrentBanLock("Sur", 0)),
            Image("SurBanCurrent1", "Image", "CurrentGame.CurrentSurBannedList[1].HeaderImageSingleColor", 1279, 563, 44.5, 44.5, specialProperties: CurrentBanLock("Sur", 1)),
            Image("SurBanCurrent2", "Image", "CurrentGame.CurrentSurBannedList[2].HeaderImageSingleColor", 1331.5, 563, 44.5, 44.5, specialProperties: CurrentBanLock("Sur", 2)),
            Image("SurBanCurrent3", "Image", "CurrentGame.CurrentSurBannedList[3].HeaderImageSingleColor", 1384, 563, 44.5, 44.5, specialProperties: CurrentBanLock("Sur", 3)),
            Folded("SurBanCurrentLock0", "SurBanCurrent0", "Folded into SurBanCurrent0 lock overlay metadata."),
            Folded("SurBanCurrentLock1", "SurBanCurrent1", "Folded into SurBanCurrent1 lock overlay metadata."),
            Folded("SurBanCurrentLock2", "SurBanCurrent2", "Folded into SurBanCurrent2 lock overlay metadata."),
            Folded("SurBanCurrentLock3", "SurBanCurrent3", "Folded into SurBanCurrent3 lock overlay metadata."),
            Image("SurPick0", "BorderedImage", "CurrentGame.SurPlayerList[0].PictureShown", 0, 620, 141, 160, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, specialProperties: PickingBorder("SurPickingBorder0")),
            Image("SurPick1", "BorderedImage", "CurrentGame.SurPlayerList[1].PictureShown", 143, 620, 141, 160, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, specialProperties: PickingBorder("SurPickingBorder1")),
            Image("SurPick2", "BorderedImage", "CurrentGame.SurPlayerList[2].PictureShown", 286, 620, 141, 160, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, specialProperties: PickingBorder("SurPickingBorder2")),
            Image("SurPick3", "BorderedImage", "CurrentGame.SurPlayerList[3].PictureShown", 428, 620, 140, 160, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, specialProperties: PickingBorder("SurPickingBorder3")),
            Folded("SurPickingBorder0", "SurPick0", "Folded into SurPick0 picking border metadata."),
            Folded("SurPickingBorder1", "SurPick1", "Folded into SurPick1 picking border metadata."),
            Folded("SurPickingBorder2", "SurPick2", "Folded into SurPick2 picking border metadata."),
            Folded("SurPickingBorder3", "SurPick3", "Folded into SurPick3 picking border metadata."),
            Image("Map", "BorderedImage", "CurrentGame.PickedMapImageLarge", 572, 616, 297, 194, zIndex: -1, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill"),
            Text("MapName", "MapNameText", "CurrentGame.PickedMap", "BpWindow.MapName", 572, 616, 296, 30, zIndex: 1),
            Text("GameProgress", "GameProgressText", null, "BpWindow.GameProgress", 572, 646, 296, 20, zIndex: 1),
            Image("HunGlobalBan0", "Image", "CurrentGame.HunTeam.GlobalBannedHunList[0].HeaderImageSingleColor", 1380.5, 50.5, 45, 45, specialProperties: GlobalBanLock("Hun", 0)),
            Image("HunGlobalBan1", "Image", "CurrentGame.HunTeam.GlobalBannedHunList[1].HeaderImageSingleColor", 1380.5, 151.5, 45, 45, specialProperties: GlobalBanLock("Hun", 1)),
            Image("HunGlobalBan2", "Image", "CurrentGame.HunTeam.GlobalBannedHunList[2].HeaderImageSingleColor", 1380.5, 250, 45, 45, specialProperties: GlobalBanLock("Hun", 2)),
            Folded("HunGlobalBanLock0", "HunGlobalBan0", "Folded into HunGlobalBan0 lock overlay metadata."),
            Folded("HunGlobalBanLock1", "HunGlobalBan1", "Folded into HunGlobalBan1 lock overlay metadata."),
            Folded("HunGlobalBanLock2", "HunGlobalBan2", "Folded into HunGlobalBan2 lock overlay metadata."),
            Image("SurGlobalBan0", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[0].HeaderImageSingleColor", 13, 50.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 0)),
            Image("SurGlobalBan1", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[1].HeaderImageSingleColor", 65.5, 50.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 1)),
            Image("SurGlobalBan2", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[2].HeaderImageSingleColor", 118, 50.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 2)),
            Image("SurGlobalBan3", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[3].HeaderImageSingleColor", 169, 50.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 3)),
            Image("SurGlobalBan4", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[4].HeaderImageSingleColor", 13, 150.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 4)),
            Image("SurGlobalBan5", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[5].HeaderImageSingleColor", 65.5, 150.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 5)),
            Image("SurGlobalBan6", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[6].HeaderImageSingleColor", 118, 150.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 6)),
            Image("SurGlobalBan7", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[7].HeaderImageSingleColor", 169, 150.5, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 7)),
            Image("SurGlobalBan8", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[8].HeaderImageSingleColor", 13, 250, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 8)),
            Image("SurGlobalBan9", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[9].HeaderImageSingleColor", 65.5, 250, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 9)),
            Image("SurGlobalBan10", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[10].HeaderImageSingleColor", 118, 250, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 10)),
            Image("SurGlobalBan11", "Image", "CurrentGame.SurTeam.GlobalBannedSurList[11].HeaderImageSingleColor", 169, 250, 44.5, 44.5, specialProperties: GlobalBanLock("Sur", 11)),
            Folded("SurGlobalBanLock0", "SurGlobalBan0", "Folded into SurGlobalBan0 lock overlay metadata."),
            Folded("SurGlobalBanLock1", "SurGlobalBan1", "Folded into SurGlobalBan1 lock overlay metadata."),
            Folded("SurGlobalBanLock2", "SurGlobalBan2", "Folded into SurGlobalBan2 lock overlay metadata."),
            Folded("SurGlobalBanLock3", "SurGlobalBan3", "Folded into SurGlobalBan3 lock overlay metadata."),
            Folded("SurGlobalBanLock4", "SurGlobalBan4", "Folded into SurGlobalBan4 lock overlay metadata."),
            Folded("SurGlobalBanLock5", "SurGlobalBan5", "Folded into SurGlobalBan5 lock overlay metadata."),
            Folded("SurGlobalBanLock6", "SurGlobalBan6", "Folded into SurGlobalBan6 lock overlay metadata."),
            Folded("SurGlobalBanLock7", "SurGlobalBan7", "Folded into SurGlobalBan7 lock overlay metadata."),
            Folded("SurGlobalBanLock8", "SurGlobalBan8", "Folded into SurGlobalBan8 lock overlay metadata."),
            Folded("SurGlobalBanLock9", "SurGlobalBan9", "Folded into SurGlobalBan9 lock overlay metadata."),
            Folded("SurGlobalBanLock10", "SurGlobalBan10", "Folded into SurGlobalBan10 lock overlay metadata."),
            Folded("SurGlobalBanLock11", "SurGlobalBan11", "Folded into SurGlobalBan11 lock overlay metadata."),
            Image("HunPick", "BorderedImage", "CurrentGame.HunPlayer.PictureShown", 872, 620, 568, 161, sizingMode: ImageSizingMode.OverflowCrop, stretch: "Uniform", clipToBounds: true, specialProperties: PickingBorder("HunPickingBorder")),
            Folded("HunPickingBorder", "HunPick", "Folded into HunPick picking border metadata."),
            Text("SurId0", "Text", "CurrentGame.SurPlayerList[0].Member.Name", "BpWindow.PlayerId", 1, 781, 139, 28),
            Text("SurId1", "Text", "CurrentGame.SurPlayerList[1].Member.Name", "BpWindow.PlayerId", 145, 781, 139, 28),
            Text("SurId2", "Text", "CurrentGame.SurPlayerList[2].Member.Name", "BpWindow.PlayerId", 287, 781, 139, 28),
            Text("SurId3", "Text", "CurrentGame.SurPlayerList[3].Member.Name", "BpWindow.PlayerId", 430, 781, 139, 28),
            Text("HunId", "Text", "CurrentGame.HunPlayer.Member.Name", "BpWindow.PlayerId", 871, 781, 569, 28)
        ]);

        AddBlueprints(result, "CutSceneWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Image("SurTeamLogo", "Image", "CurrentGame.SurTeam.Logo", 251, 14, 85, 85, cornerRadius: 8, stretch: "Fill"),
            Text("SurTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentSurTeamMajorText", "CutSceneWindow.MajorPoints", 380, 42),
            Text("SurTeamName", "Text", "CurrentGame.SurTeam.Name", "CutSceneWindow.TeamName", 10, 38, 207, null, textWrapping: "WrapWithOverflow"),
            Text("HunTeamName", "Text", "CurrentGame.HunTeam.Name", "CutSceneWindow.TeamName", 1223, 38, 207, null, textWrapping: "WrapWithOverflow"),
            Text("HunTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentHunTeamMajorText", "CutSceneWindow.MajorPoints", 971, 42),
            Image("HunTeamLogo", "Image", "CurrentGame.HunTeam.Logo", 1104, 14, 84, 85, cornerRadius: 8, stretch: "Fill"),
            Image("Map", "BorderedImage", "CurrentGame.PickedMapImage", 488, 0, 463, 112, zIndex: -1, sizingMode: ImageSizingMode.FillContainer, stretch: "UniformToFill"),
            Text("MapName", "MapNameText", "CurrentGame.PickedMap", "CutSceneWindow.MapName", 488, 51, 463, null),
            Text("GameProgress", "GameProgressText", null, "CutSceneWindow.GameProgress", 488, 82, 463, 30, zIndex: 1),
            Image("SurPick0", "BorderedImage", "CurrentGame.SurPlayerList[0].Character.BigImage", 1, 115, 346, 308.5, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, verticalAlignment: "Top", specialProperties: Props(("ImageWidth", "556.5"))),
            Image("SurPick1", "BorderedImage", "CurrentGame.SurPlayerList[1].Character.BigImage", 351, 115, 346, 308.5, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, verticalAlignment: "Top", specialProperties: Props(("ImageWidth", "556.5"))),
            Image("SurPick2", "BorderedImage", "CurrentGame.SurPlayerList[2].Character.BigImage", 1, 465, 346, 306.5, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, verticalAlignment: "Top", specialProperties: Props(("ImageWidth", "556.5"))),
            Image("SurPick3", "BorderedImage", "CurrentGame.SurPlayerList[3].Character.BigImage", 350, 465, 346, 306.5, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, verticalAlignment: "Top", specialProperties: Props(("ImageWidth", "556.5"))),
            Image("HunPick", "BorderedImage", "CurrentGame.HunPlayer.Character.BigImage", 702, 114.5, 739, 635, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true, verticalAlignment: "Top"),
            Text("SurId0", "Text", "CurrentGame.SurPlayerList[0].Member.Name", "CutSceneWindow.SurPlayerId", 10, 422),
            Text("SurId1", "Text", "CurrentGame.SurPlayerList[1].Member.Name", "CutSceneWindow.SurPlayerId", 353, 425, null, 32),
            Text("SurId2", "Text", "CurrentGame.SurPlayerList[2].Member.Name", "CutSceneWindow.SurPlayerId", 1, 776, null, 31),
            Text("SurId3", "Text", "CurrentGame.SurPlayerList[3].Member.Name", "CutSceneWindow.SurPlayerId", 364, 774, null, 32),
            Text("HunId", "Text", "CurrentGame.HunPlayer.Member.Name", "CutSceneWindow.HunPlayerId", 720, 755, 382, 55),
            Talent("SurTalent0", TalentTraitDisplayKind.SurvivorTalent, 0, 164, 424, 178, 36, "Right"),
            Talent("SurTalent1", TalentTraitDisplayKind.SurvivorTalent, 1, 522, 424, 172, 37, "Right"),
            Talent("SurTalent2", TalentTraitDisplayKind.SurvivorTalent, 2, 160, 774, 182, 37, "Right"),
            Talent("SurTalent3", TalentTraitDisplayKind.SurvivorTalent, 3, 514, 771, null, 37, "Right"),
            Talent("HunTalent", TalentTraitDisplayKind.HunterTalent, null, 1102, 762, 173, 43, "Left"),
            Talent("Trait", TalentTraitDisplayKind.HunterTrait, null, 1290, 753, 56, 56, "Left")
        ]);

        AddBlueprints(result, "GameDataWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Image("SurTeamLogo", "Image", "CurrentGame.SurTeam.Logo", 96, 177, 85, 85, cornerRadius: 8, stretch: "Fill"),
            Text("SurTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentSurTeamMajorText", "GameDataWindow.MajorPoints", 285, 229),
            Text("SurTeamName", "Text", "CurrentGame.SurTeam.Name", "GameDataWindow.TeamName", 186, 176, 290, null, textWrapping: "WrapWithOverflow"),
            Text("GameScoresSur", "Text", "CurrentGame.MatchScore.CurrentSurTeamMinorScoreText", "GameDataWindow.GameScores", 476, 182, 52, 81),
            Image("Map", "BorderedImage", "CurrentGame.PickedMapImage", 556, 151, 328, 132, zIndex: -1, sizingMode: ImageSizingMode.FillContainer, stretch: "UniformToFill"),
            Text("MapName", "MapNameText", "CurrentGame.PickedMap", "GameDataWindow.MapName", 556, 220, 328, 30, zIndex: 1),
            Folded("PickedMapName", "MapName", "Folded into the MapName business control, which renders the picked map name."),
            Text("GameProgress", "GameProgressText", null, "GameDataWindow.GameProgress", 556, 253, 328, 30, zIndex: 1),
            Text("GameScoresHun", "Text", "CurrentGame.MatchScore.CurrentHunTeamMinorScoreText", "GameDataWindow.GameScores", 919, 182, 52, 81),
            Text("HunTeamName", "Text", "CurrentGame.HunTeam.Name", "GameDataWindow.TeamName", 976, 177, 302, null, textWrapping: "WrapWithOverflow"),
            Text("HunTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentHunTeamMajorText", "GameDataWindow.MajorPoints", 1081, 236),
            Image("HunTeamLogo", "Image", "CurrentGame.HunTeam.Logo", 1278, 176, 85, 86, cornerRadius: 8, stretch: "Fill"),
            Header("Header_Character", "Character", 47, 307, 80),
            Header("Header_ID", "ID", 154, 307, 100),
            Header("Header_DecodingProgress", "DecodingProgress", 331, 307, 150),
            Header("Header_PalletStrikes", "PalletStrikes", 485, 307, 150),
            Header("Header_Rescues", "Rescues", 634, 307, 120),
            Header("Header_Heals", "Heals", 774, 307, 120),
            Header("Header_ContainmentTime", "ContainmentTime", 894, 307, 176),
            Image("Player0Header", "BorderedImage", "CurrentGame.SurPlayerList[0].PictureShownHeader", 47, 354, 50, 50, sizingMode: ImageSizingMode.Auto, stretch: "Uniform"),
            Image("Player1Header", "BorderedImage", "CurrentGame.SurPlayerList[1].PictureShownHeader", 47, 414, 50, 50, sizingMode: ImageSizingMode.Auto, stretch: "Uniform"),
            Image("Player2Header", "BorderedImage", "CurrentGame.SurPlayerList[2].PictureShownHeader", 47, 473, 50, 50, sizingMode: ImageSizingMode.Auto, stretch: "Uniform"),
            Image("Player3Header", "BorderedImage", "CurrentGame.SurPlayerList[3].PictureShownHeader", 47, 534, 50, 50, sizingMode: ImageSizingMode.Auto, stretch: "Uniform"),
            Text("SurId0", "Text", "CurrentGame.SurPlayerList[0].Member.Name", "GameDataWindow.PlayerId", 115, 354),
            Text("SurId1", "Text", "CurrentGame.SurPlayerList[1].Member.Name", "GameDataWindow.PlayerId", 115, 414),
            Text("SurId2", "Text", "CurrentGame.SurPlayerList[2].Member.Name", "GameDataWindow.PlayerId", 115, 474),
            Text("SurId3", "Text", "CurrentGame.SurPlayerList[3].Member.Name", "GameDataWindow.PlayerId", 115, 534),
            Data("Sur0MachineDecoded", "CurrentGame.SurPlayerList[0].Data.DecodingProgress", "GameDataWindow.SurData", 377, 354),
            Data("Sur1MachineDecoded", "CurrentGame.SurPlayerList[1].Data.DecodingProgress", "GameDataWindow.SurData", 377, 414),
            Data("Sur2MachineDecoded", "CurrentGame.SurPlayerList[2].Data.DecodingProgress", "GameDataWindow.SurData", 377, 474),
            Data("Sur3MachineDecoded", "CurrentGame.SurPlayerList[3].Data.DecodingProgress", "GameDataWindow.SurData", 377, 534),
            Data("Sur0PalletStunTimes", "CurrentGame.SurPlayerList[0].Data.PalletStrikes", "GameDataWindow.SurData", 531, 354),
            Data("Sur1PalletStunTimes", "CurrentGame.SurPlayerList[1].Data.PalletStrikes", "GameDataWindow.SurData", 531, 413),
            Data("Sur2PalletStunTimes", "CurrentGame.SurPlayerList[2].Data.PalletStrikes", "GameDataWindow.SurData", 531, 474),
            Data("Sur3PalletStunTimes", "CurrentGame.SurPlayerList[3].Data.PalletStrikes", "GameDataWindow.SurData", 531, 534),
            Data("Sur0RescueTimes", "CurrentGame.SurPlayerList[0].Data.Rescues", "GameDataWindow.SurData", 666, 354),
            Data("Sur1RescueTimes", "CurrentGame.SurPlayerList[1].Data.Rescues", "GameDataWindow.SurData", 666, 414),
            Data("Sur2RescueTimes", "CurrentGame.SurPlayerList[2].Data.Rescues", "GameDataWindow.SurData", 666, 474),
            Data("Sur3RescueTimes", "CurrentGame.SurPlayerList[3].Data.Rescues", "GameDataWindow.SurData", 666, 534),
            Data("Sur0HealedTimes", "CurrentGame.SurPlayerList[0].Data.Heals", "GameDataWindow.SurData", 809, 354),
            Data("Sur1HealedTimes", "CurrentGame.SurPlayerList[1].Data.Heals", "GameDataWindow.SurData", 809, 414),
            Data("Sur2HealedTimes", "CurrentGame.SurPlayerList[2].Data.Heals", "GameDataWindow.SurData", 809, 474),
            Data("Sur3HealedTimes", "CurrentGame.SurPlayerList[3].Data.Heals", "GameDataWindow.SurData", 809, 534),
            Data("Sur0KiteTime", "CurrentGame.SurPlayerList[0].Data.ContainmentTime", "GameDataWindow.SurData", 963, 354),
            Data("Sur1KiteTime", "CurrentGame.SurPlayerList[1].Data.ContainmentTime", "GameDataWindow.SurData", 963, 413),
            Data("Sur2KiteTime", "CurrentGame.SurPlayerList[2].Data.ContainmentTime", "GameDataWindow.SurData", 963, 474),
            Data("Sur3KiteTime", "CurrentGame.SurPlayerList[3].Data.ContainmentTime", "GameDataWindow.SurData", 963, 534),
            Image("HunImage", "BorderedImage", "CurrentGame.HunPlayer.PictureShownHeader", 1075, 295, 314, 96, sizingMode: ImageSizingMode.FillContainer, stretch: "UniformToFill"),
            Text("HunId", "Text", "CurrentGame.HunPlayer.Member.Name", "GameDataWindow.PlayerId", 1080, 357, null, 35),
            Header("Header_RemainingCiphers", "RemainingCiphers", 1085, 404, 160, "GameDataWindow.HunDataHeader"),
            Header("Header_PalletsDestroyed", "PalletsDestroyed", 1085, 440, 160, "GameDataWindow.HunDataHeader"),
            Header("Header_SurvivorHits", "SurvivorHits", 1085, 475, 160, "GameDataWindow.HunDataHeader"),
            Header("Header_TerrorShocks", "TerrorShocks", 1085, 511, 160, "GameDataWindow.HunDataHeader"),
            Header("Header_Knockdowns", "Knockdowns", 1085, 548, 160, "GameDataWindow.HunDataHeader"),
            Data("HunMachineLeft", "CurrentGame.HunPlayer.Data.RemainingCipher", "GameDataWindow.HunData", 1280, 405),
            Data("HunPalletBroken", "CurrentGame.HunPlayer.Data.PalletsDestroyed", "GameDataWindow.HunData", 1280, 442),
            Data("HunHitTimes", "CurrentGame.HunPlayer.Data.SurvivorHits", "GameDataWindow.HunData", 1280, 478),
            Data("HunTerrorShockTimes", "CurrentGame.HunPlayer.Data.TerrorShocks", "GameDataWindow.HunData", 1280, 514),
            Data("HunDownTimes", "CurrentGame.HunPlayer.Data.Knockdowns", "GameDataWindow.HunData", 1280, 547)
        ]);

        AddBlueprints(result, "ScoreSurWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Image("SurTeamLogo", "Image", "CurrentGame.SurTeam.Logo", 22, 18, 115, 114, cornerRadius: 8, stretch: "Fill"),
            Text("SurTeamName", "Text", "CurrentGame.SurTeam.Name", "ScoreWindow.TeamName", 153, 34, 231, null),
            Text("SurTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentSurTeamMajorText", "ScoreWindow.MajorPoints", 209, 86),
            Text("GameScoresSur", "Text", "CurrentGame.MatchScore.CurrentSurTeamMinorScoreText", "ScoreWindow.GameScores", 389, 11, 64, 130)
        ]);

        AddBlueprints(result, "ScoreHunWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Image("HunTeamLogo", "Image", "CurrentGame.HunTeam.Logo", 352, 18, 116, 114, cornerRadius: 8, stretch: "Fill"),
            Text("HunTeamName", "Text", "CurrentGame.HunTeam.Name", "ScoreWindow.TeamName", 99, 33, 231, null),
            Text("HunTeamMajorPoint", "Text", "CurrentGame.MatchScore.CurrentHunTeamMajorText", "ScoreWindow.MajorPoints", 167, 85),
            Text("GameScoresHun", "Text", "CurrentGame.MatchScore.CurrentHunTeamMinorScoreText", "ScoreWindow.GameScores", 21, 10, 64, 130)
        ]);

        AddBlueprints(result, "ScoreGlobalWindow", "BaseCanvas",
        [
            Removed("BaseCanvas", "The legacy Canvas is represented by FrontedWindowConfig.CanvasSettings and the fixed v3 BaseCanvas host."),
            Text("MainTeamName", "Text", "CurrentGame.HomeTeam.Name", "ScoreWindow.ScoreGlobal_TeamName", 13, 96, 144, 26, targetName: "HomeTeamName"),
            Text("AwayTeamName", "Text", "CurrentGame.AwayTeam.Name", "ScoreWindow.ScoreGlobal_TeamName", 13, 155, 144, null),
            Text("MainScoreTotal", "Text", "CurrentGame.MatchScore.HomeTotalScore", "ScoreWindow.ScoreGlobal_Total", 1303, 89, 86, null, targetName: "HomeScoreTotal"),
            Text("AwayScoreTotal", "Text", "CurrentGame.MatchScore.AwayTotalScore", "ScoreWindow.ScoreGlobal_Total", 1302, 147, 87, null),
            ScoreRow("HomeGlobalScoreRow", TeamType.HomeTeam, "ScoreWindow.ScoreGlobal_Data"),
            ScoreRow("AwayGlobalScoreRow", TeamType.AwayTeam, "ScoreWindow.ScoreGlobal_Data")
        ]);

        AddBlueprints(result, "WidgetsWindow", "BpOverViewCanvas",
        [
            Removed("BpOverViewCanvas", "The legacy overview Canvas is split into BpOverviewWindow/BaseCanvas."),
            Image("SurTeamLogo", "Image", "CurrentGame.SurTeam.Logo", 42, 30, 85, 85, cornerRadius: 8, stretch: "Fill"),
            Text("SurTeamNameInOverview", "Text", "CurrentGame.SurTeam.Name", "WidgetsWindow.BpOverview_TeamName", 0, 132, 166, null, textWrapping: "WrapWithOverflow"),
            Text("HunTeamNameInOverview", "Text", "CurrentGame.HunTeam.Name", "WidgetsWindow.BpOverview_TeamName", 960, 132, 166, null, textWrapping: "WrapWithOverflow"),
            Image("HunTeamLogo", "Image", "CurrentGame.HunTeam.Logo", 1000, 30, 86, 85, cornerRadius: 8, stretch: "Fill"),
            Image("HunBanCurrent0", "Image", "CurrentGame.CurrentHunBannedList[0].HeaderImageSingleColor", 644, 5, 145, 35, specialProperties: CurrentBanLock("Hun", 0)),
            Image("HunBanCurrent1", "Image", "CurrentGame.CurrentHunBannedList[1].HeaderImageSingleColor", 794, 5, 141, 35, specialProperties: CurrentBanLock("Hun", 1)),
            Folded("HunBanCurrentLock0", "HunBanCurrent0", "Folded into HunBanCurrent0 lock overlay metadata."),
            Folded("HunBanCurrentLock1", "HunBanCurrent1", "Folded into HunBanCurrent1 lock overlay metadata."),
            Image("SurBanCurrent3", "Image", "CurrentGame.CurrentSurBannedList[3].HeaderImageSingleColor", 416, 5, 68, 35, specialProperties: CurrentBanLock("Sur", 3)),
            Image("SurBanCurrent2", "Image", "CurrentGame.CurrentSurBannedList[2].HeaderImageSingleColor", 340, 5, 71, 35, specialProperties: CurrentBanLock("Sur", 2)),
            Image("SurBanCurrent1", "Image", "CurrentGame.CurrentSurBannedList[1].HeaderImageSingleColor", 265, 5, 71, 35, specialProperties: CurrentBanLock("Sur", 1)),
            Image("SurBanCurrent0", "Image", "CurrentGame.CurrentSurBannedList[0].HeaderImageSingleColor", 193, 5, 68, 35, specialProperties: CurrentBanLock("Sur", 0)),
            Folded("SurBanCurrentLock0", "SurBanCurrent0", "Folded into SurBanCurrent0 lock overlay metadata."),
            Folded("SurBanCurrentLock1", "SurBanCurrent1", "Folded into SurBanCurrent1 lock overlay metadata."),
            Folded("SurBanCurrentLock2", "SurBanCurrent2", "Folded into SurBanCurrent2 lock overlay metadata."),
            Folded("SurBanCurrentLock3", "SurBanCurrent3", "Folded into SurBanCurrent3 lock overlay metadata."),
            Image("SurPick0", "BorderedImage", "CurrentGame.SurPlayerList[0].Character.HalfImage", 193, 65, 68, 110, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true),
            Image("SurPick1", "BorderedImage", "CurrentGame.SurPlayerList[1].Character.HalfImage", 265, 65, 71, 110, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true),
            Image("SurPick2", "BorderedImage", "CurrentGame.SurPlayerList[2].Character.HalfImage", 340, 65, 72, 110, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true),
            Image("SurPick3", "BorderedImage", "CurrentGame.SurPlayerList[3].Character.HalfImage", 416, 65, 68, 110, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true),
            Text("GameProgress", "GameProgressText", null, "WidgetsWindow.BpOverview_GameProgress", 471, 0, 178, 50, zIndex: 1),
            Text("GameScoresSur", "Text", "CurrentGame.MatchScore.CurrentSurTeamMinorScoreText", "WidgetsWindow.BpOverview_GameScores", 495, 94, 52, 62),
            Text("RatioChar", "Text", null, "WidgetsWindow.BpOverview_GameScores", 552, 89, 25, 62, staticText: ":"),
            Text("GameScoresHun", "Text", "CurrentGame.MatchScore.CurrentHunTeamMinorScoreText", "WidgetsWindow.BpOverview_GameScores", 583, 94, 52, 62),
            Image("HunPick", "BorderedImage", "CurrentGame.HunPlayer.Character.HalfImage", 644, 45, 291, 130, sizingMode: ImageSizingMode.OverflowCrop, stretch: "UniformToFill", clipToBounds: true)
        ]);

        AddBlueprints(result, "WidgetsWindow", "MapV2Canvas",
        [
            Removed("MapV2Canvas", "The legacy MapV2 Canvas is split into MapV2Window/BaseCanvas."),
            MapV2("Arms_Factory", "ArmsFactory", 50.5),
            MapV2("The_Red_Church", "TheRedChurch", 204),
            MapV2("Sacred_Heart_Hospital", "SacredHeartHospital", 359),
            MapV2("Leo_s_Memory", "LeosMemory", 514),
            MapV2("Moonlit_River_Park", "MoonlitRiverPark", 669),
            MapV2("Lakeside_Village", "LakesideVillage", 824),
            MapV2("Eversleeping_Town", "EversleepingTown", 979),
            MapV2("Chinatown", "ChinaTown", 1134),
            MapV2("Darkwoods", "Darkwoods", 1289)
        ]);

        AddBlueprints(result, "WidgetsWindow", "MapBpCanvas",
        [
            Unsupported("MapBpCanvas", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("PickedMap", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("PickedMapName", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("PickWord", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("SurTeamName", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("VS_Word", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("HunTeamName", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("BannedMap", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("BannedMapName", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped."),
            Unsupported("BanWord", "Legacy MapBpV1 is not supported by Designer v3 converter and was skipped.")
        ]);

        return result;
    }

    private static IReadOnlyDictionary<string, LegacyScoreGlobalCellBlueprint> CreateLegacyScoreGlobalCellBlueprints()
    {
        var result = new Dictionary<string, LegacyScoreGlobalCellBlueprint>(StringComparer.Ordinal);
        foreach (var team in new[] { "Home", "Away" })
        {
            foreach (var game in Enumerable.Range(1, 5))
            {
                AddLegacyScoreGlobalCell(result, team, game, "FirstHalf", isOvertime: false);
                AddLegacyScoreGlobalCell(result, team, game, "SecondHalf", isOvertime: false);
                AddLegacyScoreGlobalCell(result, team, game, "FirstHalf", isOvertime: true);
                AddLegacyScoreGlobalCell(result, team, game, "SecondHalf", isOvertime: true);
            }
        }

        return result;
    }

    private static void AddLegacyScoreGlobalCell(
        IDictionary<string, LegacyScoreGlobalCellBlueprint> result,
        string team,
        int game,
        string half,
        bool isOvertime)
    {
        var name = $"{team}TeamGame{game}{(isOvertime ? "Overtime" : string.Empty)}{half}";
        result.Add(name, new LegacyScoreGlobalCellBlueprint(team, game, half, isOvertime));
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
                SourceCanvas = canvas,
                TargetWindow = GetTargetWindowForBlueprint(window, canvas)
            })
            .ToArray();
    }

    private static string? GetTargetWindowForBlueprint(string window, string canvas) =>
        (window, canvas) switch
        {
            ("WidgetsWindow", "BpOverViewCanvas") => "BpOverviewWindow",
            ("WidgetsWindow", "MapV2Canvas") => "MapV2Window",
            ("WidgetsWindow", "MapBpCanvas") => null,
            (_, "BaseCanvas") => window,
            _ => null
        };

    private static LegacyControlBlueprint Text(
        string legacyName,
        string controlType,
        string? textBinding,
        string textStyleSourceKey,
        double? left,
        double? top,
        double? width = null,
        double? height = null,
        string? targetName = null,
        int zIndex = 0,
        string? staticText = null,
        string? textWrapping = null)
    {
        var style = GetTextStyleDefaults(textStyleSourceKey);
        return new LegacyControlBlueprint
        {
            LegacyName = legacyName,
            TargetName = targetName ?? legacyName,
            TargetControlType = controlType,
            TextBinding = textBinding,
            StaticText = staticText,
            FontFamily = style.FontFamily,
            FontSize = style.FontSize,
            FontWeight = style.FontWeight,
            Color = style.Color,
            HorizontalAlignment = "Center",
            VerticalAlignment = "Center",
            TextAlignment = "Center",
            TextWrapping = textWrapping ?? style.TextWrapping,
            ZIndex = zIndex,
            DefaultLeft = left,
            DefaultTop = top,
            DefaultWidth = width,
            DefaultHeight = height,
            TextStyleSourceKey = textStyleSourceKey,
            Status = LegacyControlBlueprintStatus.Mapped
        };
    }

    private static LegacyControlBlueprint Header(
        string legacyName,
        string staticText,
        double? left,
        double? top,
        double? width,
        string textStyleSourceKey = "GameDataWindow.SurDataHeader") =>
        Text(legacyName, "Text", null, textStyleSourceKey, left, top, width, null, staticText: staticText);

    private static LegacyControlBlueprint Data(
        string legacyName,
        string textBinding,
        string textStyleSourceKey,
        double? left,
        double? top) =>
        Text(legacyName, "Text", textBinding, textStyleSourceKey, left, top);

    private static LegacyControlBlueprint Image(
        string legacyName,
        string controlType,
        string? bindingPath,
        double? left,
        double? top,
        double? width,
        double? height,
        string? targetName = null,
        int zIndex = 0,
        ImageSizingMode? sizingMode = null,
        string? stretch = "Uniform",
        bool clipToBounds = false,
        double? cornerRadius = null,
        string? horizontalAlignment = "Center",
        string? verticalAlignment = "Center",
        string? resourceSourceKey = null,
        IReadOnlyDictionary<string, string>? specialProperties = null) =>
        new()
        {
            LegacyName = legacyName,
            TargetName = targetName ?? legacyName,
            TargetControlType = controlType,
            ImageBindingPath = bindingPath,
            SizingMode = sizingMode,
            Stretch = stretch,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = verticalAlignment,
            ClipToBounds = clipToBounds,
            CornerRadius = cornerRadius,
            ZIndex = zIndex,
            DefaultLeft = left,
            DefaultTop = top,
            DefaultWidth = width,
            DefaultHeight = height,
            ResourceSourceKey = resourceSourceKey
                                ?? (specialProperties is not null
                                    && specialProperties.TryGetValue("ResourceSourceKey", out var value)
                                        ? value
                                        : null),
            SpecialProperties = specialProperties?.ToDictionary(StringComparer.Ordinal) ?? [],
            Status = LegacyControlBlueprintStatus.Mapped
        };

    private static LegacyControlBlueprint Talent(
        string legacyName,
        TalentTraitDisplayKind displayKind,
        int? playerIndex,
        double? left,
        double? top,
        double? width,
        double? height,
        string horizontalAlignment) =>
        new()
        {
            LegacyName = legacyName,
            TargetName = legacyName,
            TargetControlType = "TalentTraitDisplay",
            DefaultLeft = left,
            DefaultTop = top,
            DefaultWidth = width,
            DefaultHeight = height,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = "Center",
            SpecialProperties = playerIndex.HasValue
                ? Props(("DisplayKind", displayKind.ToString()), ("PlayerIndex", playerIndex.Value.ToString()))
                : Props(("DisplayKind", displayKind.ToString())),
            Status = LegacyControlBlueprintStatus.Mapped
        };

    private static LegacyControlBlueprint ScoreRow(
        string targetName,
        TeamType teamType,
        string textStyleSourceKey)
    {
        var style = GetTextStyleDefaults(textStyleSourceKey);
        return new LegacyControlBlueprint
        {
            LegacyName = targetName,
            TargetName = targetName,
            TargetControlType = "GlobalScoreRow",
            FontFamily = style.FontFamily,
            FontWeight = style.FontWeight,
            Color = style.Color,
            FontSize = style.FontSize,
            DefaultWidth = 1,
            DefaultHeight = 1,
            TextStyleSourceKey = textStyleSourceKey,
            SpecialProperties = Props(("TeamType", teamType.ToString())),
            Status = LegacyControlBlueprintStatus.Mapped
        };
    }

    private static LegacyControlBlueprint MapV2(string legacyName, string mapKey, double left) =>
        new()
        {
            LegacyName = legacyName,
            TargetName = legacyName,
            TargetControlType = "MapV2Display",
            DefaultLeft = left,
            DefaultTop = 0,
            DefaultWidth = string.Equals(legacyName, "Arms_Factory", StringComparison.Ordinal) ? 149 : 151,
            DefaultHeight = 160,
            SpecialProperties = Props(
                ("MapKey", mapKey),
                ("MapNameFontFamily", "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简"),
                ("MapNameFontWeight", "Normal"),
                ("MapNameColor", "#FFFFFFFF"),
                ("MapNameFontSize", "14"),
                ("TeamNameFontFamily", "pack://application:,,,/Assets/Fonts/#Noto Sans"),
                ("TeamNameFontWeight", "Normal"),
                ("TeamNameColor", "#FFFFFFFF"),
                ("TeamNameFontSize", "18"),
                ("CampNameFontFamily", "pack://application:,,,/Assets/Fonts/#Noto Sans"),
                ("CampNameFontWeight", "Normal"),
                ("CampNameColor", "#FFFFFFFF"),
                ("CampNameFontSize", "20"),
                ("MapBorderNormalColor", "#FF2B483B"),
                ("MapBorderBannedColor", "#FF9C3E2F"),
                ("PickingBorderImageResourceSourceKey", "MapBpV2PickingBorderImage"),
                ("PickingBorderColorResourceSourceKey", "MapBpV2PickingBorderColor")),
            Status = LegacyControlBlueprintStatus.Mapped
        };

    private static LegacyControlBlueprint Folded(string legacyName, string targetName, string reason) =>
        new()
        {
            LegacyName = legacyName,
            TargetName = targetName,
            Status = LegacyControlBlueprintStatus.Folded,
            UnsupportedReason = reason
        };

    private static LegacyControlBlueprint Removed(string legacyName, string reason) =>
        new()
        {
            LegacyName = legacyName,
            Status = LegacyControlBlueprintStatus.RemovedWithReason,
            UnsupportedReason = reason
        };

    private static LegacyControlBlueprint Unsupported(string legacyName, string reason) =>
        new()
        {
            LegacyName = legacyName,
            Status = LegacyControlBlueprintStatus.Unsupported,
            UnsupportedReason = reason
        };

    private static Dictionary<string, string> Props(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private static Dictionary<string, string> CurrentBanLock(string camp, int index) =>
        Props(
            ("Lockable", "true"),
            ("LockVisibleWhen", FrontedOverlayVisibilityMode.VisibleWhenFalse.ToString()),
            ("LockVisibilityBindingPath", $"CanCurrent{camp}BannedList[{index}]"),
            ("LockImagePath", "Resources/CurrentBanLock.png"),
            ("ResourceSourceKey", "CurrentBanLockImage"));

    private static Dictionary<string, string> GlobalBanLock(string camp, int index) =>
        Props(
            ("Lockable", "true"),
            ("LockVisibleWhen", FrontedOverlayVisibilityMode.VisibleWhenFalse.ToString()),
            ("LockVisibilityBindingPath", $"CanGlobal{camp}BannedList[{index}]"),
            ("LockImagePath", "Resources/GlobalBanLock.png"),
            ("ResourceSourceKey", "GlobalBanLockImage"));

    private static Dictionary<string, string> PickingBorder(string name) =>
        Props(
            ("PickingBorderAvailable", "true"),
            ("PickingBorderName", name),
            ("ResourceSourceKey", "PickingBorderImage"));

    private static LegacyTextStyleDefaults GetTextStyleDefaults(string sourceKey)
    {
        const string white = "#FFFFFFFF";
        const string notoSans = "Noto Sans";
        const string pop = "pack://application:,,,/Assets/Fonts/#华康POP1体W5";
        const string hanyi = "pack://application:,,,/Assets/Fonts/#汉仪第五人格体简";

        return sourceKey switch
        {
            "BpWindow.Timer" => new(null, null, null, null, pop, "Bold", white, 58),
            "BpWindow.TeamName" => new(null, null, null, null, notoSans, "Normal", white, 16),
            "BpWindow.GameScores" => new(null, null, null, null, pop, "Bold", white, 26),
            "BpWindow.MajorPoints" => new(null, null, null, null, "Arial", "Medium", white, 20),
            "BpWindow.PlayerId" => new("Left", null, null, null, notoSans, "Normal", white, 16),
            "BpWindow.MapName" => new(null, null, null, null, hanyi, "Normal", white, 20),
            "BpWindow.GameProgress" => new(null, null, null, null, pop, "Normal", white, 16),
            "CutSceneWindow.TeamName" => new(null, null, null, "WrapWithOverflow", notoSans, "Bold", white, 28),
            "CutSceneWindow.MajorPoints" => new(null, null, null, null, "Arial", "Bold", white, 28),
            "CutSceneWindow.SurPlayerId" => new("Left", null, null, null, notoSans, "Normal", white, 18),
            "CutSceneWindow.HunPlayerId" => new(null, null, null, null, notoSans, "Normal", white, 30),
            "CutSceneWindow.MapName" => new(null, null, null, null, hanyi, "Normal", white, 24),
            "CutSceneWindow.GameProgress" => new(null, null, null, null, pop, "Normal", white, 22),
            "ScoreWindow.GameScores" => new(null, null, null, null, pop, "Normal", white, 100),
            "ScoreWindow.MajorPoints" => new(null, null, null, null, pop, "Normal", white, 38),
            "ScoreWindow.TeamName" => new(null, null, null, null, pop, "Normal", white, 32),
            "ScoreWindow.ScoreGlobal_TeamName" => new(null, null, null, null, pop, "Normal", white, 24),
            "ScoreWindow.ScoreGlobal_Data" => new(null, null, null, null, "Arial", "Bold", white, 24),
            "ScoreWindow.ScoreGlobal_Total" => new(null, null, null, null, pop, "Bold", white, 40),
            "GameDataWindow.TeamName" => new(null, null, null, "WrapWithOverflow", notoSans, "Normal", white, 32),
            "GameDataWindow.GameScores" => new(null, null, null, null, pop, "Bold", white, 80),
            "GameDataWindow.MajorPoints" => new(null, null, null, null, "Arial", "Bold", white, 30),
            "GameDataWindow.PlayerId" => new(null, null, null, null, notoSans, "Normal", white, 22),
            "GameDataWindow.MapName" => new(null, null, null, null, hanyi, "Normal", white, 22),
            "GameDataWindow.GameProgress" => new(null, null, null, null, pop, "Normal", white, 20),
            "GameDataWindow.SurDataHeader" => new(null, null, null, null, notoSans, "Normal", white, 16),
            "GameDataWindow.HunDataHeader" => new(null, null, null, null, notoSans, "Normal", white, 16),
            "GameDataWindow.SurData" => new(null, null, null, null, pop, "Normal", white, 22),
            "GameDataWindow.HunData" => new(null, null, null, null, pop, "Normal", white, 22),
            "WidgetsWindow.BpOverview_TeamName" => new(null, null, null, "WrapWithOverflow", notoSans, "Normal", white, 22),
            "WidgetsWindow.BpOverview_GameProgress" => new(null, null, null, null, pop, "Normal", white, 22),
            "WidgetsWindow.BpOverview_GameScores" => new(null, null, null, null, pop, "Normal", white, 50),
            _ => new(null, null, null, null, notoSans, "Normal", white, 16)
        };
    }

    private static FrontedControlConfigBase CreateDefaultControl(LegacyControlBlueprint blueprint)
    {
        return blueprint.TargetControlType switch
        {
            "Text" => CreateDefaultText(blueprint),
            "MapNameText" => CreateDefaultMapNameText(blueprint),
            "GameProgressText" => CreateDefaultGameProgressText(blueprint),
            "Image" => CreateDefaultImage(blueprint),
            "BorderedImage" => CreateDefaultBorderedImage(blueprint),
            "TalentTraitDisplay" => CreateDefaultTalentTrait(blueprint),
            "GlobalScoreRow" => CreateDefaultGlobalScoreRow(blueprint),
            "MapV2Display" => CreateDefaultMapV2Display(blueprint),
            _ => new FrontedControlConfigBase { ControlType = blueprint.TargetControlType }
        };
    }

    private static TextFrontedControlConfig CreateDefaultText(LegacyControlBlueprint blueprint)
    {
        return new TextFrontedControlConfig
        {
            Text = blueprint.StaticText,
            TextBinding = CreateTextBinding(blueprint.TextBinding),
            BindingPath = blueprint.BindingPath,
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            TextAlignment = blueprint.TextAlignment,
            TextWrapping = blueprint.TextWrapping,
            FontFamily = blueprint.FontFamily,
            FontWeight = blueprint.FontWeight,
            Color = blueprint.Color,
            FontSize = blueprint.FontSize.GetValueOrDefault(),
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
    }

    private static MapNameTextControlConfig CreateDefaultMapNameText(LegacyControlBlueprint blueprint)
    {
        return new MapNameTextControlConfig
        {
            BindingPath = blueprint.BindingPath,
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            TextAlignment = blueprint.TextAlignment,
            FontFamily = blueprint.FontFamily,
            FontWeight = blueprint.FontWeight,
            Color = blueprint.Color,
            FontSize = blueprint.FontSize.GetValueOrDefault(),
            EmptyText = blueprint.StaticText,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
    }

    private static GameProgressTextControlConfig CreateDefaultGameProgressText(LegacyControlBlueprint blueprint)
    {
        return new GameProgressTextControlConfig
        {
            BindingPath = blueprint.BindingPath,
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            TextAlignment = blueprint.TextAlignment,
            FontFamily = blueprint.FontFamily,
            FontWeight = blueprint.FontWeight,
            Color = blueprint.Color,
            FontSize = blueprint.FontSize.GetValueOrDefault(),
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
    }

    private static ImageFrontedControlConfig CreateDefaultImage(LegacyControlBlueprint blueprint)
    {
        var image = new ImageFrontedControlConfig
        {
            BindingPath = blueprint.ImageBindingPath ?? blueprint.BindingPath,
            ImagePath = blueprint.ImagePath,
            SizingMode = blueprint.SizingMode ?? ImageSizingMode.FillContainer,
            Stretch = blueprint.Stretch,
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            ClipToBounds = blueprint.ClipToBounds,
            CornerRadius = blueprint.CornerRadius,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
        ApplyImageSpecialProperties(image, blueprint);
        return image;
    }

    private static BorderedImageFrontedControlConfig CreateDefaultBorderedImage(LegacyControlBlueprint blueprint)
    {
        var image = new BorderedImageFrontedControlConfig
        {
            BindingPath = blueprint.ImageBindingPath ?? blueprint.BindingPath,
            ImagePath = blueprint.ImagePath,
            SizingMode = blueprint.SizingMode ?? ImageSizingMode.OverflowCrop,
            Stretch = blueprint.Stretch,
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            ClipToBounds = blueprint.ClipToBounds,
            CornerRadius = blueprint.CornerRadius,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
        ApplyImageSpecialProperties(image, blueprint);
        return image;
    }

    private static TalentTraitDisplayControlConfig CreateDefaultTalentTrait(LegacyControlBlueprint blueprint)
    {
        var control = new TalentTraitDisplayControlConfig
        {
            HorizontalAlignment = blueprint.HorizontalAlignment,
            VerticalAlignment = blueprint.VerticalAlignment,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };

        if (blueprint.SpecialProperties.TryGetValue("DisplayKind", out var displayKind)
            && Enum.TryParse<TalentTraitDisplayKind>(displayKind, out var parsedKind))
        {
            control.DisplayKind = parsedKind;
        }

        if (blueprint.SpecialProperties.TryGetValue("PlayerIndex", out var indexText)
            && int.TryParse(indexText, out var index))
        {
            control.PlayerIndex = index;
        }

        return control;
    }

    private static GlobalScoreRowControlConfig CreateDefaultGlobalScoreRow(LegacyControlBlueprint blueprint)
    {
        var teamType = TeamType.HomeTeam;
        if (blueprint.SpecialProperties.TryGetValue("TeamType", out var teamTypeText)
            && Enum.TryParse<TeamType>(teamTypeText, out var parsedTeamType))
        {
            teamType = parsedTeamType;
        }

        return new GlobalScoreRowControlConfig
        {
            TeamType = teamType,
            FontFamily = blueprint.FontFamily,
            FontWeight = blueprint.FontWeight,
            Color = blueprint.Color,
            FontSize = blueprint.FontSize.GetValueOrDefault(24D),
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight
        };
    }

    private static MapV2DisplayControlConfig CreateDefaultMapV2Display(LegacyControlBlueprint blueprint)
    {
        return new MapV2DisplayControlConfig
        {
            MapKey = blueprint.SpecialProperties.GetValueOrDefault("MapKey") ?? string.Empty,
            Width = blueprint.DefaultWidth,
            Height = blueprint.DefaultHeight,
            MapNameFontFamily = blueprint.SpecialProperties.GetValueOrDefault("MapNameFontFamily"),
            MapNameFontWeight = blueprint.SpecialProperties.GetValueOrDefault("MapNameFontWeight"),
            MapNameColor = blueprint.SpecialProperties.GetValueOrDefault("MapNameColor"),
            MapNameFontSize = ReadDoubleSpecialProperty(blueprint, "MapNameFontSize"),
            TeamNameFontFamily = blueprint.SpecialProperties.GetValueOrDefault("TeamNameFontFamily"),
            TeamNameFontWeight = blueprint.SpecialProperties.GetValueOrDefault("TeamNameFontWeight"),
            TeamNameColor = blueprint.SpecialProperties.GetValueOrDefault("TeamNameColor"),
            TeamNameFontSize = ReadDoubleSpecialProperty(blueprint, "TeamNameFontSize"),
            CampNameFontFamily = blueprint.SpecialProperties.GetValueOrDefault("CampNameFontFamily"),
            CampNameFontWeight = blueprint.SpecialProperties.GetValueOrDefault("CampNameFontWeight"),
            CampNameColor = blueprint.SpecialProperties.GetValueOrDefault("CampNameColor"),
            CampNameFontSize = ReadDoubleSpecialProperty(blueprint, "CampNameFontSize"),
            MapBorderNormalColor = blueprint.SpecialProperties.GetValueOrDefault("MapBorderNormalColor"),
            MapBorderBannedColor = blueprint.SpecialProperties.GetValueOrDefault("MapBorderBannedColor"),
            PickingBorderImagePath = blueprint.SpecialProperties.GetValueOrDefault("PickingBorderImagePath"),
            PickingBorderFillColor = blueprint.SpecialProperties.GetValueOrDefault("PickingBorderFillColor")
        };
    }

    private static void ApplyBlueprintDefaults(
        LegacyControlBlueprint blueprint,
        FrontedControlConfigBase control)
    {
        if (string.IsNullOrWhiteSpace(control.ControlType))
        {
            control.ControlType = blueprint.TargetControlType;
        }

        if (blueprint.DefaultLeft.HasValue)
        {
            control.Left = blueprint.DefaultLeft.Value;
        }

        if (blueprint.DefaultTop.HasValue)
        {
            control.Top = blueprint.DefaultTop.Value;
        }

        control.ZIndex = blueprint.ZIndex;
    }

    private static void ApplyImageSpecialProperties(ImageFrontedControlConfig image, LegacyControlBlueprint blueprint)
    {
        if (ReadBoolSpecialProperty(blueprint, "Lockable"))
        {
            image.Lockable = true;
        }

        if (blueprint.SpecialProperties.TryGetValue("LockImagePath", out var lockImagePath))
        {
            image.LockImagePath = lockImagePath;
        }

        if (blueprint.SpecialProperties.TryGetValue("LockVisibilityBindingPath", out var lockVisibilityBindingPath))
        {
            image.LockVisibilityBindingPath = lockVisibilityBindingPath;
        }

        if (blueprint.SpecialProperties.TryGetValue("LockVisibleWhen", out var lockVisibleWhen)
            && Enum.TryParse<FrontedOverlayVisibilityMode>(lockVisibleWhen, out var parsedVisibleWhen))
        {
            image.LockVisibleWhen = parsedVisibleWhen;
        }

        if (blueprint.SpecialProperties.TryGetValue("PickingBorderAvailable", out var pickingBorderAvailable)
            && bool.TryParse(pickingBorderAvailable, out var parsedPickingBorderAvailable))
        {
            image.PickingBorderAvailable = parsedPickingBorderAvailable;
        }

        if (blueprint.SpecialProperties.TryGetValue("PickingBorderName", out var pickingBorderName))
        {
            image.PickingBorderName = pickingBorderName;
        }

        if (blueprint.SpecialProperties.TryGetValue("PickingBorderImagePath", out var pickingBorderImagePath))
        {
            image.PickingBorderImagePath = pickingBorderImagePath;
        }

        if (image is BorderedImageFrontedControlConfig bordered)
        {
            bordered.ImageWidth = ReadNullableDoubleSpecialProperty(blueprint, "ImageWidth");
            bordered.ImageHeight = ReadNullableDoubleSpecialProperty(blueprint, "ImageHeight");
        }
    }

    private static void ApplyLegacyTextStyleOverrides(
        FrontedCanvasConfig config,
        LegacyLayoutMapping mapping,
        LegacySettings? legacySettings,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        if (legacySettings is null)
        {
            return;
        }

        var key = new LegacyLayoutKey(mapping.SourceWindow, mapping.SourceCanvas);
        if (!LegacyControlBlueprints.TryGetValue(key, out var blueprints))
        {
            return;
        }

        foreach (var blueprint in blueprints)
        {
            if (blueprint.Status is not LegacyControlBlueprintStatus.Mapped
                and not LegacyControlBlueprintStatus.Aggregated
                || !config.Controls.TryGetValue(blueprint.TargetName, out var control))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(blueprint.TextStyleSourceKey)
                && TryGetLegacyTextStyle(legacySettings, blueprint.TextStyleSourceKey, out var style)
                && style is not null
                && control is IFrontedTextStyleConfig textControl)
            {
                ApplyLegacyTextStyle(textControl, style);
                messages.Add(Info(CodeTextSettingsApplied,
                    Args(new
                    {
                        SourceWindow = mapping.SourceWindow,
                        SourceCanvas = mapping.SourceCanvas,
                        ControlName = blueprint.TargetName,
                        TextStyleKey = blueprint.TextStyleSourceKey
                    })));
            }

            if (control is MapV2DisplayControlConfig map)
            {
                ApplyMapV2LegacyTextStyle(map, legacySettings, blueprint, messages, mapping);
            }
        }
    }

    private static void ApplyMapV2LegacyTextStyle(
        MapV2DisplayControlConfig map,
        LegacySettings legacySettings,
        LegacyControlBlueprint blueprint,
        ICollection<FrontedLayoutPackageLegacyConvertMessage> messages,
        LegacyLayoutMapping mapping)
    {
        if (TryGetLegacyTextStyle(legacySettings, "WidgetsWindow.MapBpV2_MapName", out var mapNameStyle)
            && mapNameStyle is not null)
        {
            map.MapNameColor = FirstNonEmpty(mapNameStyle.Color, map.MapNameColor);
            map.MapNameFontFamily = FirstNonEmpty(
                LegacyFrontedTextStyleMigrator.NormalizeLegacyFontFamilySite(mapNameStyle.FontFamilySite),
                map.MapNameFontFamily);
            map.MapNameFontWeight = mapNameStyle.FontWeight.ToString();
            if (mapNameStyle.FontSize > 0)
            {
                map.MapNameFontSize = mapNameStyle.FontSize;
            }

            messages.Add(Info(CodeTextSettingsApplied,
                Args(new
                {
                    SourceWindow = mapping.SourceWindow,
                    SourceCanvas = mapping.SourceCanvas,
                    ControlName = blueprint.TargetName,
                    TextStyleKey = "WidgetsWindow.MapBpV2_MapName"
                })));
        }

        if (TryGetLegacyTextStyle(legacySettings, "WidgetsWindow.MapBpV2_TeamName", out var teamNameStyle)
            && teamNameStyle is not null)
        {
            map.TeamNameColor = FirstNonEmpty(teamNameStyle.Color, map.TeamNameColor);
            map.TeamNameFontFamily = FirstNonEmpty(
                LegacyFrontedTextStyleMigrator.NormalizeLegacyFontFamilySite(teamNameStyle.FontFamilySite),
                map.TeamNameFontFamily);
            map.TeamNameFontWeight = teamNameStyle.FontWeight.ToString();
            if (teamNameStyle.FontSize > 0)
            {
                map.TeamNameFontSize = teamNameStyle.FontSize;
            }

            messages.Add(Info(CodeTextSettingsApplied,
                Args(new
                {
                    SourceWindow = mapping.SourceWindow,
                    SourceCanvas = mapping.SourceCanvas,
                    ControlName = blueprint.TargetName,
                    TextStyleKey = "WidgetsWindow.MapBpV2_TeamName"
                })));
        }

        if (TryGetLegacyTextStyle(legacySettings, "WidgetsWindow.MapBpV2_CampWords", out var campStyle)
            && campStyle is not null)
        {
            map.CampNameColor = FirstNonEmpty(campStyle.Color, map.CampNameColor);
            map.CampNameFontFamily = FirstNonEmpty(
                LegacyFrontedTextStyleMigrator.NormalizeLegacyFontFamilySite(campStyle.FontFamilySite),
                map.CampNameFontFamily);
            map.CampNameFontWeight = campStyle.FontWeight.ToString();
            if (campStyle.FontSize > 0)
            {
                map.CampNameFontSize = campStyle.FontSize;
            }

            messages.Add(Info(CodeTextSettingsApplied,
                Args(new
                {
                    SourceWindow = mapping.SourceWindow,
                    SourceCanvas = mapping.SourceCanvas,
                    ControlName = blueprint.TargetName,
                    TextStyleKey = "WidgetsWindow.MapBpV2_CampWords"
                })));
        }
    }

    private static bool TryGetLegacyTextStyle(
        LegacySettings legacySettings,
        string sourceKey,
        out LegacyTextSettings? style)
    {
        style = sourceKey switch
        {
            "BpWindow.Timer" => legacySettings.BpWindowSettings?.TextSettings?.Timer,
            "BpWindow.TeamName" => legacySettings.BpWindowSettings?.TextSettings?.TeamName,
            "BpWindow.GameScores" => legacySettings.BpWindowSettings?.TextSettings?.GameScores,
            "BpWindow.MajorPoints" => legacySettings.BpWindowSettings?.TextSettings?.MajorPoints,
            "BpWindow.PlayerId" => legacySettings.BpWindowSettings?.TextSettings?.PlayerId,
            "BpWindow.MapName" => legacySettings.BpWindowSettings?.TextSettings?.MapName,
            "BpWindow.GameProgress" => legacySettings.BpWindowSettings?.TextSettings?.GameProgress,
            "CutSceneWindow.TeamName" => legacySettings.CutSceneWindowSettings?.TextSettings?.TeamName,
            "CutSceneWindow.MajorPoints" => legacySettings.CutSceneWindowSettings?.TextSettings?.MajorPoints,
            "CutSceneWindow.SurPlayerId" => legacySettings.CutSceneWindowSettings?.TextSettings?.SurPlayerId,
            "CutSceneWindow.HunPlayerId" => legacySettings.CutSceneWindowSettings?.TextSettings?.HunPlayerId,
            "CutSceneWindow.MapName" => legacySettings.CutSceneWindowSettings?.TextSettings?.MapName,
            "CutSceneWindow.GameProgress" => legacySettings.CutSceneWindowSettings?.TextSettings?.GameProgress,
            "ScoreWindow.GameScores" => legacySettings.ScoreWindowSettings?.TextSettings?.GameScores,
            "ScoreWindow.MajorPoints" => legacySettings.ScoreWindowSettings?.TextSettings?.MajorPoints,
            "ScoreWindow.TeamName" => legacySettings.ScoreWindowSettings?.TextSettings?.TeamName,
            "ScoreWindow.ScoreGlobal_TeamName" => legacySettings.ScoreWindowSettings?.TextSettings?.ScoreGlobal_TeamName,
            "ScoreWindow.ScoreGlobal_Data" => legacySettings.ScoreWindowSettings?.TextSettings?.ScoreGlobal_Data,
            "ScoreWindow.ScoreGlobal_Total" => legacySettings.ScoreWindowSettings?.TextSettings?.ScoreGlobal_Total,
            "GameDataWindow.TeamName" => legacySettings.GameDataWindowSettings?.TextSettings?.TeamName,
            "GameDataWindow.GameScores" => legacySettings.GameDataWindowSettings?.TextSettings?.GameScores,
            "GameDataWindow.MajorPoints" => legacySettings.GameDataWindowSettings?.TextSettings?.MajorPoints,
            "GameDataWindow.PlayerId" => legacySettings.GameDataWindowSettings?.TextSettings?.PlayerId,
            "GameDataWindow.MapName" => legacySettings.GameDataWindowSettings?.TextSettings?.MapName,
            "GameDataWindow.GameProgress" => legacySettings.GameDataWindowSettings?.TextSettings?.GameProgress,
            "GameDataWindow.SurDataHeader" => legacySettings.GameDataWindowSettings?.TextSettings?.SurDataHeader,
            "GameDataWindow.HunDataHeader" => legacySettings.GameDataWindowSettings?.TextSettings?.HunDataHeader,
            "GameDataWindow.SurData" => legacySettings.GameDataWindowSettings?.TextSettings?.SurData,
            "GameDataWindow.HunData" => legacySettings.GameDataWindowSettings?.TextSettings?.HunData,
            "WidgetsWindow.BpOverview_TeamName" => legacySettings.WidgetsWindowSettings?.TextSettings?.BpOverview_TeamName,
            "WidgetsWindow.BpOverview_GameProgress" => legacySettings.WidgetsWindowSettings?.TextSettings?.BpOverview_GameProgress,
            "WidgetsWindow.BpOverview_GameScores" => legacySettings.WidgetsWindowSettings?.TextSettings?.BpOverview_GameScores,
            "WidgetsWindow.MapBpV2_MapName" => legacySettings.WidgetsWindowSettings?.TextSettings?.MapBpV2_MapName,
            "WidgetsWindow.MapBpV2_TeamName" => legacySettings.WidgetsWindowSettings?.TextSettings?.MapBpV2_TeamName,
            "WidgetsWindow.MapBpV2_CampWords" => legacySettings.WidgetsWindowSettings?.TextSettings?.MapBpV2_CampWords,
            _ => null
        };

        return style is not null;
    }

    private static void ApplyLegacyTextStyle(IFrontedTextStyleConfig target, LegacyTextSettings style)
    {
        if (!string.IsNullOrWhiteSpace(style.Color))
        {
            target.Color = style.Color.Trim();
        }

        var fontFamily = LegacyFrontedTextStyleMigrator.NormalizeLegacyFontFamilySite(style.FontFamilySite);
        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            target.FontFamily = fontFamily;
        }

        if (style.FontSize > 0)
        {
            target.FontSize = style.FontSize;
        }

        target.FontWeight = style.FontWeight.ToString();
    }

    private static string? FirstNonEmpty(string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool ReadBoolSpecialProperty(LegacyControlBlueprint blueprint, string key) =>
        blueprint.SpecialProperties.TryGetValue(key, out var value)
        && bool.TryParse(value, out var result)
        && result;

    private static double ReadDoubleSpecialProperty(LegacyControlBlueprint blueprint, string key) =>
        blueprint.SpecialProperties.TryGetValue(key, out var value)
        && double.TryParse(value, out var result)
            ? result
            : 0D;

    private static double? ReadNullableDoubleSpecialProperty(LegacyControlBlueprint blueprint, string key) =>
        blueprint.SpecialProperties.TryGetValue(key, out var value)
        && double.TryParse(value, out var result)
            ? result
            : null;

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
        List<FrontedLayoutPackageLegacyConvertMessage> messages)
    {
        var result = new FrontedLayoutPackageLegacyConvertResult
        {
            Success = false,
            ErrorMessage = message
        };
        FrontedLayoutPackageLegacyConvertResult.PopulateFromMessages(result, messages);
        return result;
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

    private sealed record LegacyScoreGlobalCellBlueprint(
        string Team,
        int Game,
        string Half,
        bool IsOvertime);

    private readonly record struct LegacyLayoutKey(string SourceWindow, string SourceCanvas);

    private enum LegacyControlBlueprintStatus
    {
        Mapped,
        Folded,
        Aggregated,
        Unsupported,
        RemovedWithReason
    }

    private sealed record LegacyControlBlueprint
    {
        public string SourceWindow { get; init; } = string.Empty;

        public string SourceCanvas { get; init; } = string.Empty;

        public string LegacyName { get; init; } = string.Empty;

        public string? TargetWindow { get; init; }

        public string TargetName { get; init; } = string.Empty;

        public string TargetControlType { get; init; } = string.Empty;

        public string? BindingPath { get; init; }

        public string? TextBinding { get; init; }

        public string? StaticText { get; init; }

        public string? FontFamily { get; init; }

        public double? FontSize { get; init; }

        public string? FontWeight { get; init; }

        public string? Color { get; init; }

        public string? HorizontalAlignment { get; init; }

        public string? VerticalAlignment { get; init; }

        public string? TextAlignment { get; init; }

        public string? TextWrapping { get; init; }

        public string? ImageBindingPath { get; init; }

        public string? ImagePath { get; init; }

        public ImageSizingMode? SizingMode { get; init; }

        public string? Stretch { get; init; }

        public bool ClipToBounds { get; init; }

        public double? CornerRadius { get; init; }

        public int ZIndex { get; init; }

        public double? DefaultLeft { get; init; }

        public double? DefaultTop { get; init; }

        public double? DefaultWidth { get; init; }

        public double? DefaultHeight { get; init; }

        public IReadOnlyDictionary<string, string> SpecialProperties { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public string? TextStyleSourceKey { get; init; }

        public string? ResourceSourceKey { get; init; }

        public LegacyControlBlueprintStatus Status { get; init; } = LegacyControlBlueprintStatus.Mapped;

        public string? UnsupportedReason { get; init; }

        public bool Required { get; init; } = true;
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
