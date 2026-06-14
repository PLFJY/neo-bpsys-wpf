#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using System.IO;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台布局包导入器，负责将 .bpui 格式的压缩包或目录导入为可用的布局包。
/// 导入时会验证包格式、布局版本、资源完整性和安全性。
/// </summary>
public sealed class FrontedLayoutPackageImporter : IFrontedLayoutPackageImporter
{
    private const string ManifestFileName = "manifest.json";
    private readonly string _packageRoot;
    private readonly string _tempRoot;
    private readonly IFrontedLayoutPackageManager? _packageManager;
    private readonly ILogger<FrontedLayoutPackageImporter> _logger;
    private readonly FrontedLayoutValidator _validator;
    private readonly IFrontedImageSafetyService _imageSafetyService;
    private readonly IFrontedControlRegistry? _controlRegistry;
    private readonly IFrontedPluginMetadataProvider? _pluginMetadataProvider;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };
    private readonly JsonSerializerOptions _writeJsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    /// <summary>
    /// 使用默认路径初始化导入器。
    /// </summary>
    /// <param name="packageManager">包管理器。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="controlRegistry">控件注册表（可选）。</param>
    /// <param name="pluginMetadataProvider">插件元数据提供者（可选）。</param>
    public FrontedLayoutPackageImporter(
        IFrontedLayoutPackageManager packageManager,
        ILogger<FrontedLayoutPackageImporter> logger,
        IFrontedControlRegistry? controlRegistry = null,
        IFrontedPluginMetadataProvider? pluginMetadataProvider = null)
        : this(
            AppConstants.FrontedLayoutPackagesPath,
            Path.Combine(AppConstants.AppTempPath, "bpui-import"),
            packageManager,
            logger,
            controlRegistry,
            pluginMetadataProvider)
    {
    }

    /// <summary>
    /// 使用自定义根路径初始化导入器。
    /// </summary>
    /// <param name="packageRoot">包存储根目录。</param>
    /// <param name="tempRoot">临时文件根目录。</param>
    /// <param name="packageManager">包管理器（可选）。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="controlRegistry">控件注册表（可选）。</param>
    /// <param name="pluginMetadataProvider">插件元数据提供者（可选）。</param>
    public FrontedLayoutPackageImporter(
        string packageRoot,
        string tempRoot,
        IFrontedLayoutPackageManager? packageManager = null,
        ILogger<FrontedLayoutPackageImporter>? logger = null,
        IFrontedControlRegistry? controlRegistry = null,
        IFrontedPluginMetadataProvider? pluginMetadataProvider = null)
    {
        _packageRoot = packageRoot;
        _tempRoot = tempRoot;
        _packageManager = packageManager;
        _logger = logger ?? NullLogger<FrontedLayoutPackageImporter>.Instance;
        _controlRegistry = controlRegistry;
        _pluginMetadataProvider = pluginMetadataProvider;
        _validator = new FrontedLayoutValidator(controlRegistry);
        _imageSafetyService = new FrontedImageSafetyService();
    }

    /// <summary>
    /// 从 .bpui 压缩包文件导入布局包。
    /// </summary>
    /// <param name="request">导入请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导入结果。</returns>
    public async Task<FrontedLayoutPackageImportResult> ImportAsync(
        FrontedLayoutPackageImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var stagingRoot = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            if (string.IsNullOrWhiteSpace(request.PackagePath) || !File.Exists(request.PackagePath))
            {
                return Fail("Package archive was not found.");
            }

            if (new FileInfo(request.PackagePath).Length > FrontedLayoutLimits.MaxPackageArchiveBytes)
            {
                return Fail("PackageTooLarge");
            }

            Directory.CreateDirectory(stagingRoot);
            ExtractZipSafely(request.PackagePath, stagingRoot);
            var result = await ImportStagedDirectoryAsync(
                stagingRoot,
                request.ReplaceExisting,
                request.ActivateAfterImport,
                cancellationToken);
            if (result.Success)
            {
                stagingRoot = string.Empty;
            }

            return result;
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid bpui archive.");
            return Fail($"Invalid package archive: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to import fronted layout package.");
            return Fail(ex.Message);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    /// <summary>
    /// 从本地目录导入布局包。
    /// </summary>
    /// <param name="packageDirectory">包目录路径。</param>
    /// <param name="replaceExisting">是否替换已存在的同名包。</param>
    /// <param name="activateAfterImport">导入后是否立即激活。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导入结果。</returns>
    public async Task<FrontedLayoutPackageImportResult> ImportDirectoryAsync(
        string packageDirectory,
        bool replaceExisting,
        bool activateAfterImport,
        CancellationToken cancellationToken = default)
    {
        var stagingRoot = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        try
        {
            if (string.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory))
            {
                return Fail("Package directory was not found.");
            }

            CopyDirectory(packageDirectory, stagingRoot);
            var result = await ImportStagedDirectoryAsync(
                stagingRoot,
                replaceExisting,
                activateAfterImport,
                cancellationToken);
            if (result.Success)
            {
                stagingRoot = string.Empty;
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to import fronted layout package directory.");
            return Fail(ex.Message);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private async Task<FrontedLayoutPackageImportResult> ImportStagedDirectoryAsync(
        string stagingRoot,
        bool replaceExisting,
        bool activateAfterImport,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(stagingRoot, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return DetectLegacyPackage(stagingRoot)
                ? Legacy()
                : Fail("manifest.json is missing.");
        }

        FrontedLayoutPackageManifest? manifest;
        try
        {
            if (new FileInfo(manifestPath).Length > FrontedLayoutLimits.MaxManifestBytes)
            {
                return Fail("ManifestTooLarge");
            }

            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            manifest = JsonSerializer.Deserialize<FrontedLayoutPackageManifest>(json, _jsonSerializerOptions);
        }
        catch (Exception ex)
        {
            return DetectLegacyPackage(stagingRoot)
                ? Legacy()
                : Fail($"Invalid package manifest: {ex.Message}");
        }

        var validation = await ValidatePackageAsync(stagingRoot, manifest, cancellationToken);
        if (!validation.Success)
        {
            return validation;
        }

        var packageLayouts = await LoadPackageLayoutsAsync(stagingRoot, manifest!, cancellationToken);
        var missingPluginControls = FrontedLayoutPluginDependencyScanner.FindMissingPluginControls(
            packageLayouts.Select(layout => (layout.Window, FrontedLayoutConstants.BaseCanvasName, FrontedWindowConfigCanvasAdapter.ToCanvasConfig(layout.Config))),
            _controlRegistry);
        var unsatisfiedPluginDependencies = FrontedLayoutPluginDependencyScanner.FindUnsatisfiedPluginDependencies(
            packageLayouts.Select(layout => (layout.Window, FrontedLayoutConstants.BaseCanvasName, FrontedWindowConfigCanvasAdapter.ToCanvasConfig(layout.Config))),
            manifest!.PluginDependencies,
            _controlRegistry,
            _pluginMetadataProvider);
        var packageId = manifest.PackageId;
        var installPath = GetInstalledPackagePath(packageId);
        if (Directory.Exists(installPath) && !replaceExisting)
        {
            return new FrontedLayoutPackageImportResult
            {
                Success = false,
                PackageId = packageId,
                PackageAlreadyExists = true,
                ErrorMessage = "Package already exists."
            };
        }

        Directory.CreateDirectory(_packageRoot);
        var packageRoot = EnsureTrailingSeparator(Path.GetFullPath(_packageRoot));
        var fullInstallPath = Path.GetFullPath(installPath);
        if (!fullInstallPath.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Package install path escaped the package root.");
        }

        if (Directory.Exists(installPath))
        {
            Directory.Delete(installPath, recursive: true);
        }

        Directory.Move(stagingRoot, installPath);
        if (activateAfterImport && _packageManager is not null)
        {
            await _packageManager.ActivatePackageAsync(packageId, cancellationToken);
        }

        return new FrontedLayoutPackageImportResult
        {
            Success = true,
            PackageId = packageId,
            InstalledPath = installPath,
            LayoutCount = manifest.Content.Layouts.Count,
            ResourceCount = manifest.Content.Resources.Count,
            MissingPluginControls = missingPluginControls,
            UnsatisfiedPluginDependencies = unsatisfiedPluginDependencies
        };
    }

    private async Task<List<PackageLayoutState>> LoadPackageLayoutsAsync(
        string stagingRoot,
        FrontedLayoutPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        var layouts = new List<PackageLayoutState>();
        foreach (var layout in manifest.Content.Layouts)
        {
            var path = CombineInsideRoot(stagingRoot, layout.Path);
            var config = JsonSerializer.Deserialize<FrontedWindowConfig>(
                await File.ReadAllTextAsync(path, cancellationToken),
                _jsonSerializerOptions)
                ?? throw new FrontedLayoutConfigException($"Layout JSON is invalid: {layout.Path}");
            await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(config, _jsonSerializerOptions),
                cancellationToken);
            layouts.Add(new PackageLayoutState(layout.Window, layout.Path, config));
        }

        manifest.PluginDependencies = FrontedLayoutPluginDependencyScanner.MergePackageDependencies(
            layouts.Select(layout => (layout.Window, FrontedLayoutConstants.BaseCanvasName, FrontedWindowConfigCanvasAdapter.ToCanvasConfig(layout.Config))),
            manifest.PluginDependencies,
            _controlRegistry);
        return layouts;
    }

    private async Task<FrontedLayoutPackageImportResult> ValidatePackageAsync(
        string stagingRoot,
        FrontedLayoutPackageManifest? manifest,
        CancellationToken cancellationToken)
    {
        if (manifest is null)
        {
            return Fail("Invalid package manifest.");
        }

        if (!string.Equals(manifest.Format, "neo-bpsys-bpui", StringComparison.Ordinal)
            || manifest.FormatVersion != 3)
        {
            return manifest.FormatVersion > 3
                ? new FrontedLayoutPackageImportResult { Success = false, RequiresNewerApp = true, ErrorMessage = "Package requires a newer app version." }
                : Fail("Invalid package manifest.");
        }

        if (!string.Equals(
                manifest.LayoutModel,
                FrontedLayoutConstants.WindowCentricLayoutModel,
                StringComparison.Ordinal))
        {
            return Fail("Package layout model is not window-centric.");
        }

        if (!FrontedLayoutPackageManager.IsSafePackageId(manifest.PackageId)
            || string.Equals(manifest.PackageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(manifest.PackageId, FrontedLayoutPackageManager.LocalPackageId, StringComparison.OrdinalIgnoreCase))
        {
            return Fail("PackageId is invalid.");
        }

        var manifestLimitError = ValidateManifestTextLengths(manifest);
        if (!string.IsNullOrWhiteSpace(manifestLimitError))
        {
            return Fail(manifestLimitError);
        }

        if (manifest.LayoutSchemaVersion != 3)
        {
            return Fail("Layout schema version is not supported.");
        }

        if (RequiresNewerApp(manifest.MinVersion))
        {
            return new FrontedLayoutPackageImportResult
            {
                Success = false,
                RequiresNewerApp = true,
                PackageId = manifest.PackageId,
                ErrorMessage = "Package requires a newer app version."
            };
        }

        if (File.Exists(Path.Combine(stagingRoot, "Config.json"))
            || Directory.Exists(Path.Combine(stagingRoot, "CustomUi"))
            || Directory.Exists(Path.Combine(stagingRoot, "FrontElementsConfig")))
        {
            return Fail("v3 packages must not contain legacy Config.json, CustomUi, or FrontElementsConfig content.");
        }

        if (manifest.Content.Layouts.Count == 0)
        {
            return Fail("Package contains no layouts.");
        }

        if (manifest.Content.Layouts.Count > FrontedLayoutLimits.MaxLayoutsPerPackage)
        {
            return Fail("TooManyLayouts");
        }

        if (manifest.Content.Resources.Count > FrontedLayoutLimits.MaxResourcesPerPackage)
        {
            return Fail("TooManyResources");
        }

        foreach (var layout in manifest.Content.Layouts)
            {
                if (!IsSafeRelativePath(layout.Path)
                || !FrontedLayoutWindowPathHelper.IsSafeFullWindowType(layout.Window))
            {
                return Fail("Layout path is not safe.");
            }

            var layoutPath = CombineInsideRoot(stagingRoot, layout.Path);
            if (!File.Exists(layoutPath))
            {
                return Fail($"Layout file is missing: {layout.Path}");
            }

            if (new FileInfo(layoutPath).Length > FrontedLayoutLimits.MaxLayoutJsonBytes)
            {
                return Fail("LayoutJsonTooLarge");
            }

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(
                    await File.ReadAllTextAsync(layoutPath, cancellationToken),
                    nodeOptions: null,
                    documentOptions: new JsonDocumentOptions { MaxDepth = FrontedLayoutLimits.MaxJsonDepth });
            }
            catch (Exception ex)
            {
                return Fail($"Layout JSON is invalid: {layout.Path}; {ex.Message}");
            }

            if (node is not JsonObject obj
                || !obj.TryGetPropertyValue("Version", out var versionNode)
                || !TryGetInt(versionNode, out var version)
                || version != 3)
            {
                return Fail($"Layout Version must be 3: {layout.Path}");
            }

            var resourceError = ValidateLayoutResourceReferences(obj, stagingRoot, manifest.PackageId);
            if (!string.IsNullOrWhiteSpace(resourceError))
            {
                return Fail(resourceError);
            }

            try
            {
                var config = JsonSerializer.Deserialize<FrontedWindowConfig>(
                    await File.ReadAllTextAsync(layoutPath, cancellationToken),
                    _jsonSerializerOptions);
                if (config is null)
                {
                    return Fail($"Layout JSON is invalid: {layout.Path}");
                }

                var validationMessages = _validator.Validate(
                    layout.Window,
                    FrontedLayoutConstants.BaseCanvasName,
                    FrontedWindowConfigCanvasAdapter.ToCanvasConfig(config));
                var error = validationMessages.FirstOrDefault(message =>
                    message.Severity == global::neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.FrontedLayoutValidationSeverity.Error
                    && !string.Equals(message.Code, "RuntimeCriticalRenameOrDelete", StringComparison.Ordinal));
                if (error is not null)
                {
                    return Fail($"Layout validation failed: {layout.Path}; {error.Message}");
                }
            }
            catch (Exception ex)
            {
                return Fail($"Layout JSON is invalid: {layout.Path}; {ex.Message}");
            }
        }

        foreach (var resource in manifest.Content.Resources)
        {
            if (!IsSafeRelativePath(resource.Path))
            {
                return Fail("Resource path is not safe.");
            }

            var resourcePath = CombineInsideRoot(stagingRoot, resource.Path);
            if (!File.Exists(resourcePath))
            {
                return Fail($"Missing package resource: {resource.Path}");
            }

            if (IsImageResource(resource.Path))
            {
                var validation = _imageSafetyService.ValidateFile(
                    resourcePath,
                    FrontedImagePurpose.PackageResource,
                    knownBackgroundImage: false,
                    knownUiImage: false);
                if (!validation.IsValid)
                {
                    return Fail(validation.ErrorCode ?? "InvalidImageResource");
                }
            }
        }

        return new FrontedLayoutPackageImportResult { Success = true };
    }

    private static string? ValidateManifestTextLengths(FrontedLayoutPackageManifest manifest)
    {
        if (FrontedTextLimitHelper.IsTooLong(manifest.PackageId, FrontedLayoutLimits.MaxPackageIdLength))
        {
            return "InputTooLong: PackageId";
        }

        if (FrontedTextLimitHelper.IsTooLong(manifest.Name, FrontedLayoutLimits.MaxPackageNameLength))
        {
            return "InputTooLong: Name";
        }

        if (FrontedTextLimitHelper.IsTooLong(manifest.Author, FrontedLayoutLimits.MaxPackageAuthorLength))
        {
            return "InputTooLong: Author";
        }

        if (FrontedTextLimitHelper.IsTooLong(manifest.MinVersion, FrontedLayoutLimits.MaxPackageMinVersionLength))
        {
            return "InputTooLong: MinVersion";
        }

        return FrontedTextLimitHelper.IsTooLong(manifest.Description, FrontedLayoutLimits.MaxPackageDescriptionLength)
            ? "InputTooLong: Description"
            : null;
    }

    private static string? ValidateLayoutResourceReferences(JsonNode node, string stagingRoot, string packageId)
    {
        foreach (var value in EnumerateResourceStrings(node, null))
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("pack://application:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Path.IsPathRooted(Environment.ExpandEnvironmentVariables(value)))
            {
                return $"Absolute resource paths are not allowed in package layouts: {value}";
            }

            if (!value.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, "bpui", StringComparison.OrdinalIgnoreCase))
            {
                return $"Invalid bpui resource reference: {value}";
            }

            var referencedPackageId = Uri.UnescapeDataString(uri.Host);
            if (string.Equals(referencedPackageId, FrontedLayoutPackageManager.LocalPackageId, StringComparison.OrdinalIgnoreCase))
            {
                return $"bpui://local resource reference is not allowed in imported packages: {value}";
            }

            if (!string.Equals(referencedPackageId, packageId, StringComparison.OrdinalIgnoreCase))
            {
                return $"Cross-package resource reference is not allowed: {value}";
            }

            var relativePath = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            if (!IsSafeRelativePath(relativePath))
            {
                return $"Package resource path is not safe: {value}";
            }

            if (!File.Exists(CombineInsideRoot(stagingRoot, relativePath)))
            {
                return $"Missing package resource: {relativePath}";
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateResourceStrings(JsonNode node, string? propertyName)
    {
        if (node is JsonObject obj)
        {
            foreach (var child in obj)
            {
                if (child.Value is null)
                {
                    continue;
                }

                if (child.Value is JsonValue value
                    && value.TryGetValue<string>(out var text)
                    && ShouldInspectResourceProperty(child.Key))
                {
                    yield return text;
                    continue;
                }

                foreach (var nested in EnumerateResourceStrings(child.Value, child.Key))
                {
                    yield return nested;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is null)
                {
                    continue;
                }

                foreach (var nested in EnumerateResourceStrings(child, propertyName))
                {
                    yield return nested;
                }
            }
        }
    }

    private static bool ShouldInspectResourceProperty(string propertyName)
    {
        return string.Equals(propertyName, "BackgroundImage", StringComparison.Ordinal)
               || string.Equals(propertyName, "ImagePath", StringComparison.Ordinal)
               || string.Equals(propertyName, "ImageSource", StringComparison.Ordinal)
               || string.Equals(propertyName, "ResourcePath", StringComparison.Ordinal)
               || string.Equals(propertyName, "LockImageSource", StringComparison.Ordinal)
               || string.Equals(propertyName, "BorderImagePath", StringComparison.Ordinal)
               || string.Equals(propertyName, "FontFamily", StringComparison.Ordinal)
               || propertyName.EndsWith("ImagePath", StringComparison.Ordinal)
               || propertyName.EndsWith("ImageSource", StringComparison.Ordinal)
               || propertyName.EndsWith("ResourcePath", StringComparison.Ordinal)
               || propertyName.EndsWith("LockImageSource", StringComparison.Ordinal)
               || propertyName.EndsWith("BorderImagePath", StringComparison.Ordinal);
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

            if (IsForbiddenPluginPayloadEntry(entryName))
            {
                throw new InvalidDataException($"Forbidden plugin or executable payload in bpui package: {entry.FullName}");
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

    private static bool IsForbiddenPluginPayloadEntry(string entryName)
    {
        var segments = entryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment =>
                string.Equals(segment, "Plugins", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "Plugin", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (entryName.EndsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var extension = Path.GetExtension(entryName);
        if (ForbiddenExecutableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var pluginLookingFolder = segments.Take(Math.Max(0, segments.Length - 1))
            .Any(segment => segment.Contains("plugin", StringComparison.OrdinalIgnoreCase));
        return pluginLookingFolder
               && ForbiddenPluginArchiveExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static readonly string[] ForbiddenExecutableExtensions =
    [
        ".dll",
        ".exe",
        ".msi",
        ".ps1",
        ".bat",
        ".cmd",
        ".sh",
        ".vbs",
        ".js",
        ".jar"
    ];

    private static readonly string[] ForbiddenPluginArchiveExtensions =
    [
        ".zip",
        ".nupkg",
        ".7z",
        ".rar",
        ".tar",
        ".gz"
    ];

    private static bool IsImageResource(string path)
    {
        return path.StartsWith("resources/images/", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".tif", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DetectLegacyPackage(string root)
    {
        return File.Exists(Path.Combine(root, "Config.json"))
               || Directory.Exists(Path.Combine(root, "CustomUi"))
               || Directory.Exists(Path.Combine(root, "FrontElementsConfig"));
    }

    private static bool TryGetInt(JsonNode? node, out int value)
    {
        try
        {
            if (node is JsonValue jsonValue)
            {
                return jsonValue.TryGetValue<int>(out value);
            }
        }
        catch
        {
            // Invalid numeric value.
        }

        value = 0;
        return false;
    }

    private static bool RequiresNewerApp(string minVersion)
    {
        if (string.IsNullOrWhiteSpace(minVersion)
            || !Version.TryParse(NormalizeVersion(minVersion), out var required)
            || !Version.TryParse(NormalizeVersion(AppConstants.AppVersion), out var current))
        {
            return false;
        }

        return required > current;
    }

    private static string NormalizeVersion(string version)
    {
        var normalized = version.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOfAny(['+', '-']);
        return metadataIndex > 0 ? normalized[..metadataIndex] : normalized;
    }

    private string GetInstalledPackagePath(string packageId)
    {
        return Path.Combine(_packageRoot, packageId);
    }

    private static bool IsSafePathSegment(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && !value.Contains('/', StringComparison.Ordinal)
               && !value.Contains('\\', StringComparison.Ordinal)
               && !value.Contains(':', StringComparison.Ordinal)
               && !value.Contains("..", StringComparison.Ordinal);
    }

    private static bool IsSafeRelativePath(string relativePath)
    {
        return !string.IsNullOrWhiteSpace(relativePath)
               && !Path.IsPathRooted(relativePath)
               && !relativePath.Contains('\\', StringComparison.Ordinal)
               && relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).All(segment => segment is not "." and not "..");
    }

    private static string CombineInsideRoot(string root, string relativePath)
    {
        var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path escaped package root.");
        }

        return path;
    }

    private static FrontedLayoutPackageImportResult Fail(string message)
    {
        return new FrontedLayoutPackageImportResult
        {
            Success = false,
            ErrorMessage = message
        };
    }

    private static FrontedLayoutPackageImportResult Legacy()
    {
        return new FrontedLayoutPackageImportResult
        {
            Success = false,
            IsLegacyPackage = true,
            ErrorMessage = "Legacy .bpui conversion is not implemented yet."
        };
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static void CopyDirectory(string sourceRoot, string targetRoot)
    {
        var fullSourceRoot = EnsureTrailingSeparator(Path.GetFullPath(sourceRoot));
        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var fullSourceFile = Path.GetFullPath(sourceFile);
            if (!fullSourceFile.StartsWith(fullSourceRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Package file escaped source directory: {sourceFile}");
            }

            var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            var targetFile = Path.Combine(targetRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: false);
        }
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

    private sealed record PackageLayoutState(
        string Window,
        string Path,
        FrontedWindowConfig Config);
}

#pragma warning restore CS1591
