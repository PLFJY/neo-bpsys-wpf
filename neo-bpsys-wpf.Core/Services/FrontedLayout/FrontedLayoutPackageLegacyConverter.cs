#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
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

    private static readonly Dictionary<string, (string Window, string Canvas)> LegacyLayoutFileMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["BpWindowConfig-BaseCanvas.json"] = ("BpWindow", "BaseCanvas"),
            ["CutSceneWindowConfig-BaseCanvas.json"] = ("CutSceneWindow", "BaseCanvas"),
            ["GameDataWindowConfig-BaseCanvas.json"] = ("GameDataWindow", "BaseCanvas"),
            ["ScoreSurWindowConfig-BaseCanvas.json"] = ("ScoreSurWindow", "BaseCanvas"),
            ["ScoreHunWindowConfig-BaseCanvas.json"] = ("ScoreHunWindow", "BaseCanvas"),
            ["ScoreGlobalWindowConfig-BaseCanvas.json"] = ("ScoreGlobalWindow", "BaseCanvas"),
            ["WidgetsWindowConfig-MapBpCanvas.json"] = ("WidgetsWindow", "MapBpCanvas"),
            ["WidgetsWindowConfig-BpOverViewCanvas.json"] = ("WidgetsWindow", "BpOverViewCanvas"),
            ["WidgetsWindowConfig-MapV2Canvas.json"] = ("WidgetsWindow", "MapV2Canvas")
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
        PropertyNameCaseInsensitive = true
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
        var warnings = new List<string>();
        var extractionRoot = Path.Combine(_tempRoot, "extract", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(_tempRoot, "staging", Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(_tempRoot, "converted", $"{Guid.NewGuid():N}.bpui");

        try
        {
            if (string.IsNullOrWhiteSpace(request.LegacyPackagePath) || !File.Exists(request.LegacyPackagePath))
            {
                return Fail("Legacy package archive was not found.", warnings);
            }

            var packageId = string.IsNullOrWhiteSpace(request.PackageId)
                ? $"converted.legacy.{DateTime.UtcNow:yyyyMMddHHmm}"
                : request.PackageId.Trim();
            if (!FrontedLayoutPackageManager.IsSafePackageId(packageId)
                || string.Equals(packageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(packageId, FrontedLayoutPackageManager.LocalPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return Fail("PackageId is invalid.", warnings);
            }

            Directory.CreateDirectory(extractionRoot);
            Directory.CreateDirectory(stagingRoot);
            ExtractZipSafely(request.LegacyPackagePath, extractionRoot);
            if (!DetectLegacyPackage(extractionRoot))
            {
                return Fail("Archive is not a legacy .bpui package.", warnings);
            }

            var resourceState = CopyCustomUiResources(extractionRoot, stagingRoot, packageId, warnings);
            var manifest = CreateManifest(request, packageId);
            manifest.Content.Resources = resourceState.Resources;

            var configImageMap = ReadFrontendConfigImageMap(extractionRoot, resourceState, warnings);
            var layoutEntries = await ConvertFrontElementsConfigsAsync(
                extractionRoot,
                stagingRoot,
                manifest,
                resourceState,
                configImageMap,
                warnings,
                cancellationToken);
            if (layoutEntries == 0)
            {
                return Fail("No mappable legacy FrontElementsConfig files were converted.", warnings);
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
            return Fail($"Invalid legacy package archive: {ex.Message}", warnings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert legacy bpui package.");
            return Fail(ex.Message, warnings);
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
        IReadOnlyDictionary<string, string> configImageMap,
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
            if (!TryMapLegacyLayoutFile(fileName, out var window, out var canvas))
            {
                warnings.Add($"Unknown legacy layout file skipped: {fileName}");
                continue;
            }

            var config = await LoadBuiltInConfigAsync(window, canvas, cancellationToken);
            ApplyFrontendConfigImages(config, window, canvas, configImageMap, warnings);
            ApplyLegacyGeometry(file, config, warnings);
            RewriteKnownResourceStrings(config, resourceState);
            config.Version = 3;

            var validationMessages = _validator.Validate(window, canvas, config);
            var validationErrors = validationMessages
                .Where(message => message.Severity == Models.FrontedLayout.Designer.FrontedLayoutValidationSeverity.Error)
                .ToArray();
            if (validationErrors.Length > 0)
            {
                warnings.Add($"Converted layout {window}/{canvas} has validation errors: {string.Join("; ", validationErrors.Select(error => error.Message))}");
                continue;
            }

            var relativePath = ToZipPath("layouts", window, $"{canvas}.json");
            var targetPath = Path.Combine(stagingRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(targetPath, json, cancellationToken);

            manifest.Content.Layouts.Add(new FrontedLayoutPackageLayoutEntry
            {
                Window = window,
                Canvas = canvas,
                Path = relativePath
            });
            convertedCount++;
        }

        return convertedCount;
    }

    private async Task<FrontedCanvasConfig> LoadBuiltInConfigAsync(
        string window,
        string canvas,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(_builtInLayoutRoot, window, $"{canvas}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Built-in v3 layout was not found: {window}/{canvas}", path);
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<FrontedCanvasConfig>(json, _jsonOptions)
               ?? throw new InvalidOperationException($"Built-in v3 layout could not be read: {window}/{canvas}");
    }

    private static void ApplyLegacyGeometry(
        string legacyFile,
        FrontedCanvasConfig config,
        ICollection<string> warnings)
    {
        Dictionary<string, ElementInfo>? legacyPositions;
        try
        {
            legacyPositions = JsonSerializer.Deserialize<Dictionary<string, ElementInfo>>(
                File.ReadAllText(legacyFile),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

        foreach (var (controlName, legacy) in legacyPositions)
        {
            if (!config.Controls.TryGetValue(controlName, out var control))
            {
                warnings.Add($"Legacy control geometry ignored because no v3 control matches: {controlName}");
                continue;
            }

            if (legacy.Left.HasValue)
            {
                control.Left = legacy.Left.Value;
            }

            if (legacy.Top.HasValue)
            {
                control.Top = legacy.Top.Value;
            }

            if (legacy.Width.HasValue)
            {
                control.Width = legacy.Width.Value;
            }

            if (legacy.Height.HasValue)
            {
                control.Height = legacy.Height.Value;
            }
        }
    }

    private static ResourceConvertState CopyCustomUiResources(
        string extractionRoot,
        string stagingRoot,
        string packageId,
        ICollection<string> warnings)
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
            warnings.Add($"Legacy resource copied: {Path.GetFileName(fullFile)}");
        }

        return state;
    }

    private static IReadOnlyDictionary<string, string> ReadFrontendConfigImageMap(
        string extractionRoot,
        ResourceConvertState resourceState,
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
            root = JsonNode.Parse(File.ReadAllText(configPath));
        }
        catch (Exception ex)
        {
            warnings.Add($"Legacy Config.json could not be read; frontend image settings were ignored. {ex.Message}");
            return result;
        }

        AddMappedImage(root, "BpWindowSettings", "BgImageUri", "BpWindow/BaseCanvas/BackgroundImage", resourceState, result, warnings);
        AddMappedImage(root, "CutSceneWindowSettings", "BgUri", "CutSceneWindow/BaseCanvas/BackgroundImage", resourceState, result, warnings);
        AddMappedImage(root, "ScoreWindowSettings", "SurScoreBgImageUri", "ScoreSurWindow/BaseCanvas/BackgroundImage", resourceState, result, warnings);
        AddMappedImage(root, "ScoreWindowSettings", "HunScoreBgImageUri", "ScoreHunWindow/BaseCanvas/BackgroundImage", resourceState, result, warnings);
        AddMappedImage(root, "ScoreWindowSettings", "GlobalScoreBgImageUri", "ScoreGlobalWindow/BaseCanvas/BackgroundImage", resourceState, result, warnings);
        AddMappedImage(root, "GameDataWindowSettings", "BgImageUri", "GameDataWindow/BaseCanvas/BackgroundImage", resourceState, result, warnings);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpBgUri", "WidgetsWindow/MapBpCanvas/BackgroundImage", resourceState, result, warnings);
        AddMappedImage(root, "WidgetsWindowSettings", "BpOverviewBgUri", "WidgetsWindow/BpOverViewCanvas/BackgroundImage", resourceState, result, warnings);
        AddMappedImage(root, "WidgetsWindowSettings", "MapBpV2BgUri", "WidgetsWindow/MapV2Canvas/BackgroundImage", resourceState, result, warnings);
        AddMappedImage(root, "BpWindowSettings", "CurrentBanLockImageUri", "BpWindow/BaseCanvas/CurrentBanLockImage", resourceState, result, warnings);
        AddMappedImage(root, "BpWindowSettings", "GlobalBanLockImageUri", "BpWindow/BaseCanvas/GlobalBanLockImage", resourceState, result, warnings);
        AddMappedImage(root, "BpWindowSettings", "PickingBorderImageUri", "BpWindow/BaseCanvas/PickingBorderImage", resourceState, result, warnings);

        foreach (var ignored in EnumeratePotentialFrontendImageFields(root)
                     .Where(field => !KnownConfigImageFields.Contains(field, StringComparer.Ordinal)))
        {
            warnings.Add($"Legacy field ignored: {ignored}");
        }

        return result;
    }

    private static void AddMappedImage(
        JsonNode? root,
        string settingsObject,
        string propertyName,
        string key,
        ResourceConvertState resourceState,
        IDictionary<string, string> result,
        ICollection<string> warnings)
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

        warnings.Add($"Legacy resource missing or not packaged for field {field}: {value}");
    }

    private static readonly HashSet<string> KnownConfigImageFields =
    [
        "BpWindowSettings.BgImageUri",
        "CutSceneWindowSettings.BgUri",
        "ScoreWindowSettings.SurScoreBgImageUri",
        "ScoreWindowSettings.HunScoreBgImageUri",
        "ScoreWindowSettings.GlobalScoreBgImageUri",
        "GameDataWindowSettings.BgImageUri",
        "WidgetsWindowSettings.MapBpBgUri",
        "WidgetsWindowSettings.BpOverviewBgUri",
        "WidgetsWindowSettings.MapBpV2BgUri",
        "BpWindowSettings.CurrentBanLockImageUri",
        "BpWindowSettings.GlobalBanLockImageUri",
        "BpWindowSettings.PickingBorderImageUri"
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
                        || property.Key.EndsWith("ImageUri", StringComparison.Ordinal)))
                {
                    yield return $"{settings.Key}.{property.Key}";
                }
            }
        }
    }

    private static void ApplyFrontendConfigImages(
        FrontedCanvasConfig config,
        string window,
        string canvas,
        IReadOnlyDictionary<string, string> imageMap,
        ICollection<string> warnings)
    {
        var prefix = $"{window}/{canvas}/";
        if (imageMap.TryGetValue($"{prefix}BackgroundImage", out var background))
        {
            config.BackgroundImage = background;
        }

        if (window == "BpWindow" && canvas == "BaseCanvas")
        {
            foreach (var control in config.Controls.Values)
            {
                if (control is BanSlotDisplayControlConfig banSlot)
                {
                    var key = banSlot.SlotKind == BanSlotKind.Global
                        ? $"{prefix}GlobalBanLockImage"
                        : $"{prefix}CurrentBanLockImage";
                    if (imageMap.TryGetValue(key, out var lockUri))
                    {
                        banSlot.LockImageSource = lockUri;
                    }
                }
                else if (control is PickingBorderOverlayControlConfig pickingBorder
                         && imageMap.TryGetValue($"{prefix}PickingBorderImage", out var borderUri))
                {
                    pickingBorder.BorderImagePath = borderUri;
                }
            }
        }

        if (imageMap.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal))
            && string.IsNullOrWhiteSpace(config.BackgroundImage))
        {
            warnings.Add($"Legacy frontend image settings found for {window}/{canvas}, but no explicit v3 target was available.");
        }
    }

    private static void RewriteKnownResourceStrings(
        FrontedCanvasConfig config,
        ResourceConvertState resourceState)
    {
        var node = JsonSerializer.SerializeToNode(config) ?? throw new InvalidOperationException("Layout could not be serialized.");
        RewriteResourceStrings(node, resourceState);
        var converted = node.Deserialize<FrontedCanvasConfig>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (converted is null)
        {
            return;
        }

        config.BackgroundImage = converted.BackgroundImage;
        config.Controls = converted.Controls;
    }

    private static void RewriteResourceStrings(JsonNode node, ResourceConvertState resourceState)
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
                    RewriteResourceStrings(child.Value, resourceState);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    RewriteResourceStrings(child, resourceState);
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
            && resourceState.ByFileName.TryGetValue(fileName, out uri))
        {
            return true;
        }

        var normalized = expanded.TrimStart('/');
        return resourceState.ByLegacyRelativePath.TryGetValue(normalized, out uri);
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

    private static bool TryMapLegacyLayoutFile(string fileName, out string window, out string canvas)
    {
        if (LegacyLayoutFileMap.TryGetValue(fileName, out var mapped))
        {
            window = mapped.Window;
            canvas = mapped.Canvas;
            return true;
        }

        window = string.Empty;
        canvas = string.Empty;
        return false;
    }

    private static void ExtractZipSafely(string zipPath, string stagingRoot)
    {
        var fullStagingRoot = EnsureTrailingSeparator(Path.GetFullPath(stagingRoot));
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
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
        IReadOnlyList<string> warnings)
    {
        return new FrontedLayoutPackageLegacyConvertResult
        {
            Success = false,
            ErrorMessage = message,
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
}

#pragma warning restore CS1591
