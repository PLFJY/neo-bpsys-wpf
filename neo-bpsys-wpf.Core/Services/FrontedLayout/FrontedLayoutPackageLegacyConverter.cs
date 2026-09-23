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
public sealed partial class FrontedLayoutPackageLegacyConverter : IFrontedLayoutPackageLegacyConverter
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

    private static readonly IReadOnlyDictionary<string, FrontedImagePurpose> LegacyConfigImagePurposes =
        new Dictionary<string, FrontedImagePurpose>(StringComparer.Ordinal)
        {
            ["BpWindowSettings.BgImageUri"] = FrontedImagePurpose.Background,
            ["CutSceneWindowSettings.BgUri"] = FrontedImagePurpose.Background,
            ["ScoreWindowSettings.SurScoreBgImageUri"] = FrontedImagePurpose.Background,
            ["ScoreWindowSettings.HunScoreBgImageUri"] = FrontedImagePurpose.Background,
            ["ScoreWindowSettings.GlobalScoreBgImageUri"] = FrontedImagePurpose.Background,
            ["ScoreWindowSettings.GlobalScoreBgImageUriBo3"] = FrontedImagePurpose.Background,
            ["GameDataWindowSettings.BgImageUri"] = FrontedImagePurpose.Background,
            ["WidgetsWindowSettings.MapBpBgUri"] = FrontedImagePurpose.Background,
            ["WidgetsWindowSettings.BpOverviewBgUri"] = FrontedImagePurpose.Background,
            ["WidgetsWindowSettings.MapBpV2BgUri"] = FrontedImagePurpose.Background,
            ["BpWindowSettings.CurrentBanLockImageUri"] = FrontedImagePurpose.UiElement,
            ["BpWindowSettings.GlobalBanLockImageUri"] = FrontedImagePurpose.UiElement,
            ["BpWindowSettings.PickingBorderImageUri"] = FrontedImagePurpose.UiElement,
            ["WidgetsWindowSettings.CurrentBanLockImageUri"] = FrontedImagePurpose.UiElement,
            ["WidgetsWindowSettings.GlobalBanLockImageUri"] = FrontedImagePurpose.UiElement,
            ["WidgetsWindowSettings.MapBpV2PickingBorderImageUri"] = FrontedImagePurpose.UiElement
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
            [new LegacyLayoutKey("BpWindow", "BaseCanvas")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // 部分 1.x 包沿用比分窗口的 MinorPoints 命名。
                ["MinorPointsSur"] = "GameScoresSur",
                ["MinorPointsHun"] = "GameScoresHun"
            },
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
            [new LegacyLayoutKey("GameDataWindow", "BaseCanvas")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // 部分旧包沿用比分窗口的 MinorPoints 命名。
                ["MinorPointsSur"] = "GameScoresSur",
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
    private readonly FrontedImageCompressionService _imageCompressionService;
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
    /// <param name="imageCompressionService">旧版图片压缩服务（可选）。</param>
    public FrontedLayoutPackageLegacyConverter(
        string builtInLayoutRoot,
        string tempRoot,
        IFrontedLayoutPackageImporter? packageImporter = null,
        FrontedLayoutValidator? validator = null,
        ILogger<FrontedLayoutPackageLegacyConverter>? logger = null,
        FrontedImageCompressionService? imageCompressionService = null)
    {
        _tempRoot = tempRoot;
        _packageImporter = packageImporter;
        _validator = validator ?? new FrontedLayoutValidator();
        _imageCompressionService = imageCompressionService ?? new FrontedImageCompressionService();
        _logger = logger ?? NullLogger<FrontedLayoutPackageLegacyConverter>.Instance;
    }

    /// <summary>
    /// 执行旧版布局包到 v3 格式的转换。
    /// </summary>
    /// <param name="request">转换请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>转换结果，包含转换后的布局、消息和资源信息。</returns>
    public Task<FrontedLayoutPackageLegacyConvertResult> ConvertAsync(
        FrontedLayoutPackageLegacyConvertRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => ConvertArchiveCoreAsync(request, cancellationToken),
            cancellationToken);
    }

    private async Task<FrontedLayoutPackageLegacyConvertResult> ConvertArchiveCoreAsync(
        FrontedLayoutPackageLegacyConvertRequest request,
        CancellationToken cancellationToken)
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
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        return Task.Run(
            () => ConvertLegacyInputAsync(
                new LegacyLocalAppDataInputSource(appDataRoot),
                request,
                createArchive: false,
                replaceExisting: true,
                cancellationToken),
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
            var imagePurposes = ReadLegacyImagePurposes(source);
            var resourceState = CopyCustomUiResources(
                source.CustomUiRoot,
                stagingRoot,
                packageId,
                imagePurposes,
                messages);
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

}

#pragma warning restore CS1591
