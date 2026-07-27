#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台布局包导出器，负责将指定的 v3 布局包导出为 .bpui 格式的压缩包。
/// 导出时会收集布局文件中引用的外部资源（图片、字体等）并一同打包。
/// </summary>
public sealed class FrontedLayoutPackageExporter : IFrontedLayoutPackageExporter
{
    private static readonly Regex SafePackageIdRegex = new(
        "^[a-z0-9][a-z0-9._-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SafeFileNameChars = new(
        "[^A-Za-z0-9._-]+",
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

    private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".otf",
        ".ttc"
    };

    private readonly IFrontedLayoutPackageManager _packageManager;
    private readonly string _packageRoot;
    private readonly string _tempRoot;
    private readonly ILogger<FrontedLayoutPackageExporter> _logger;
    private readonly IFrontedImageSafetyService _imageSafetyService;
    private readonly IFrontedV3ControlRegistry? _controlRegistry;
    private readonly IFrontedPluginMetadataProvider? _pluginMetadataProvider;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    private readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    /// <summary>
    /// 使用默认路径初始化导出器。
    /// </summary>
    /// <param name="packageManager">布局包管理器，用于获取活动包状态和路径。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="controlRegistry">控件注册表（可选）。</param>
    /// <param name="pluginMetadataProvider">插件元数据提供者（可选）。</param>
    public FrontedLayoutPackageExporter(
        IFrontedLayoutPackageManager packageManager,
        ILogger<FrontedLayoutPackageExporter> logger,
        IFrontedV3ControlRegistry? controlRegistry = null,
        IFrontedPluginMetadataProvider? pluginMetadataProvider = null)
        : this(
            packageManager,
            AppConstants.FrontedLayoutPackagesPath,
            Path.Combine(AppConstants.AppTempPath, "bpui-export"),
            logger,
            controlRegistry,
            pluginMetadataProvider)
    {
    }

    /// <summary>
    /// 使用自定义包根路径和临时路径初始化导出器。
    /// </summary>
    /// <param name="packageManager">布局包管理器，用于获取活动包状态和路径。</param>
    /// <param name="packageRoot">包存储根目录。</param>
    /// <param name="tempRoot">临时文件根目录。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="controlRegistry">控件注册表（可选）。</param>
    /// <param name="pluginMetadataProvider">插件元数据提供者（可选）。</param>
    public FrontedLayoutPackageExporter(
        IFrontedLayoutPackageManager packageManager,
        string packageRoot,
        string tempRoot,
        ILogger<FrontedLayoutPackageExporter>? logger = null,
        IFrontedV3ControlRegistry? controlRegistry = null,
        IFrontedPluginMetadataProvider? pluginMetadataProvider = null)
    {
        _packageManager = packageManager;
        _packageRoot = packageRoot;
        _tempRoot = tempRoot;
        _logger = logger ?? NullLogger<FrontedLayoutPackageExporter>.Instance;
        _imageSafetyService = new FrontedImageSafetyService();
        _controlRegistry = controlRegistry;
        _pluginMetadataProvider = pluginMetadataProvider;
    }

    /// <summary>
    /// 执行布局包导出，将选定的 v3 布局及其引用的资源打包为 .bpui 文件。
    /// 未指定源包时导出当前活动包；指定源包时从该包磁盘上已有的合法 Layout 文件导出。
    /// 未注册或未安装插件的 layout/behavior 文件原样保留；未保存的 Registry 窗口不会被补成空模板。
    /// </summary>
    /// <param name="request">导出请求参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>导出结果。</returns>
    public async Task<FrontedLayoutPackageExportResult> ExportAsync(
        FrontedLayoutPackageExportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateRequest(request);
            var activeState = await _packageManager.GetActivePackageStateAsync(cancellationToken);
            var sourcePackageId = string.IsNullOrWhiteSpace(request.SourcePackageId)
                ? activeState.PackageId
                : request.SourcePackageId;
            var entries = CollectLayoutEntries(request, sourcePackageId);
            if (entries.Count == 0)
            {
                throw new InvalidOperationException("No on-disk v3 layouts are available in the active package for the selected export scope.");
            }

            var outputPath = NormalizeOutputPath(request.OutputPath);
            var staging = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
            var resourceState = new ResourceExportState(request.PackageId);
            Directory.CreateDirectory(staging);
            Directory.CreateDirectory(Path.Combine(staging, "resources", "images"));
            Directory.CreateDirectory(Path.Combine(staging, "resources", "fonts"));
            Directory.CreateDirectory(Path.Combine(staging, "resources", "other"));

            try
            {
                var manifest = CreateManifest(request);
                await ExportLayoutsAsync(staging, entries, manifest, resourceState, cancellationToken);
                await ExportBehaviorsAsync(staging, entries, sourcePackageId, cancellationToken);
                manifest.Content.Resources = resourceState.Resources;

                var manifestJson = JsonSerializer.Serialize(manifest, _jsonSerializerOptions);
                await File.WriteAllTextAsync(Path.Combine(staging, "manifest.json"), manifestJson, cancellationToken);

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                ZipFile.CreateFromDirectory(staging, outputPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                EnsureZipEntriesAreSafe(outputPath);

                return new FrontedLayoutPackageExportResult
                {
                    Success = true,
                    OutputPath = outputPath,
                    LayoutCount = manifest.Content.Layouts.Count,
                    ResourceCount = manifest.Content.Resources.Count
                };
            }
            finally
            {
                TryDeleteDirectory(staging);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to export fronted layout package {PackageId}.", request.PackageId);
            return new FrontedLayoutPackageExportResult
            {
                Success = false,
                OutputPath = request.OutputPath,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 验证 PackageId 是否安全（仅包含小写字母数字和 <c>._-</c> 字符，不含空白、<c>..</c> 或 <c>%</c>）。
    /// </summary>
    /// <param name="packageId">待验证的包 ID。</param>
    /// <returns>是否安全。</returns>
    public static bool IsSafePackageId(string packageId)
    {
        return !string.IsNullOrWhiteSpace(packageId)
               && SafePackageIdRegex.IsMatch(packageId)
               && !packageId.Contains("..", StringComparison.Ordinal)
               && !packageId.Any(char.IsWhiteSpace)
               && !packageId.Contains('%', StringComparison.Ordinal);
    }

    private async Task ExportLayoutsAsync(
        string staging,
        IReadOnlyList<LayoutExportEntry> entries,
        FrontedLayoutPackageManifest manifest,
        ResourceExportState resourceState,
        CancellationToken cancellationToken)
    {
        var exportedLayouts = new List<(string Window, string Canvas, FrontedCanvasConfig Config)>();
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSafeCanonicalWindowId(entry.CanonicalWindowId, nameof(entry.CanonicalWindowId));

            var originalJson = await File.ReadAllTextAsync(entry.SourcePath, cancellationToken);
            var node = JsonNode.Parse(
                originalJson,
                nodeOptions: null,
                documentOptions: new JsonDocumentOptions { MaxDepth = FrontedLayoutLimits.MaxJsonDepth })
                       ?? throw new InvalidOperationException(
                           $"Layout {entry.CanonicalWindowId} parsed to empty JSON.");

            if (node is not JsonObject obj
                || !obj.TryGetPropertyValue("Version", out var versionNode)
                || !TryGetInt(versionNode, out var version)
                || version != 3)
            {
                throw new InvalidOperationException(
                    $"Layout {entry.CanonicalWindowId} has unsupported Version.");
            }

            // Deserialize the same JSON for dependency scanning only; the original JSON (as JsonNode) is what gets written.
            FrontedWindowConfig? config;
            try
            {
                config = JsonSerializer.Deserialize<FrontedWindowConfig>(originalJson, _readOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Layout {entry.CanonicalWindowId} could not be deserialized for scanning.", ex);
            }

            if (config is null)
            {
                throw new InvalidOperationException(
                    $"Layout {entry.CanonicalWindowId} deserialized to null.");
            }

            var canvasConfig = FrontedWindowConfigCanvasAdapter.ToCanvasConfig(config);
            FrontedLayoutPluginDependencyScanner.SyncCanvasRequiredPlugins(
                canvasConfig,
                entry.CanonicalWindowId,
                FrontedLayoutConstants.BaseCanvasName,
                _controlRegistry,
                _pluginMetadataProvider);
            exportedLayouts.Add((entry.CanonicalWindowId, FrontedLayoutConstants.BaseCanvasName, canvasConfig));

            // Write synced RequiredPlugins back into the JsonNode (host-managed field, safe to replace).
            if (obj["ControlLayout"] is JsonObject controlLayout)
            {
                controlLayout["RequiredPlugins"] = JsonSerializer.SerializeToNode(
                    canvasConfig.RequiredPlugins,
                    _jsonSerializerOptions);
            }

            RewriteResourcePaths(node, null, staging, resourceState);

            var relativePath = ToZipPath("FrontedLayouts", entry.RelativePath.Replace('\\', '/'));
            var targetPath = Path.Combine(staging, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, node.ToJsonString(_jsonSerializerOptions), cancellationToken);

            manifest.Content.Layouts.Add(new FrontedLayoutPackageLayoutEntry
            {
                Window = entry.CanonicalWindowId,
                Path = relativePath
            });
        }

        manifest.PluginDependencies = FrontedLayoutPluginDependencyScanner.MergePackageDependencies(
            exportedLayouts,
            manifest.PluginDependencies,
            _controlRegistry,
            _pluginMetadataProvider);
    }

    private async Task ExportBehaviorsAsync(
        string staging,
        IReadOnlyList<LayoutExportEntry> entries,
        string activePackageId,
        CancellationToken cancellationToken)
    {
        var layoutsRoot = _packageManager.GetPackageLayoutsRootFolder(activePackageId);
        var packageRoot = Path.GetDirectoryName(Path.GetFullPath(layoutsRoot))
                          ?? throw new InvalidOperationException("Package layouts root has no parent.");

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var behaviorSourcePath = GetBehaviorSourcePath(packageRoot, entry.CanonicalWindowId);
            if (!File.Exists(behaviorSourcePath))
            {
                continue;
            }

            try
            {
                var relativePath = ToZipPath(
                    "FrontedBehaviors",
                    Path.ChangeExtension(
                        FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(entry.CanonicalWindowId),
                        ".behaviors.json").Replace('\\', '/'));
                var targetPath = Path.Combine(staging, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                // Copy the behavior file as-is to preserve original format and unknown fields.
                File.Copy(behaviorSourcePath, targetPath, overwrite: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to export behaviors for {Window}.",
                    entry.CanonicalWindowId);
            }
        }
    }

    private static string GetBehaviorSourcePath(string packageRoot, string canonicalWindowId)
    {
        var layoutRelativePath = FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(canonicalWindowId);
        var folder = Path.GetDirectoryName(layoutRelativePath);
        var fileName = $"{Path.GetFileNameWithoutExtension(layoutRelativePath)}.behaviors.json";
        var behaviorRelativePath = string.IsNullOrWhiteSpace(folder)
            ? Path.Combine("FrontedBehaviors", fileName)
            : Path.Combine("FrontedBehaviors", folder, fileName);
        return Path.Combine(packageRoot, behaviorRelativePath);
    }

    private void RewriteResourcePaths(
        JsonNode node,
        string? propertyName,
        string staging,
        ResourceExportState resourceState)
    {
        if (node is JsonObject obj)
        {
            foreach (var child in obj.ToArray())
            {
                if (child.Value is null)
                {
                    continue;
                }

                if (child.Value is JsonValue value
                    && value.TryGetValue<string>(out var text)
                    && ShouldInspectResourceProperty(child.Key))
                {
                    obj[child.Key] = RewriteResourcePath(text, staging, resourceState);
                    continue;
                }

                RewriteResourcePaths(child.Value, child.Key, staging, resourceState);
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    RewriteResourcePaths(child, propertyName, staging, resourceState);
                }
            }
        }
    }

    private string RewriteResourcePath(
        string value,
        string staging,
        ResourceExportState resourceState)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("pack://application:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (TryResolveBpuiResource(value, out var bpuiPath, out var bpuiRelativePath))
        {
            var fragment = GetUriFragment(value);
            return ExportResource(bpuiPath, bpuiRelativePath, staging, resourceState) + fragment;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(value);
        if (Path.IsPathRooted(expandedPath))
        {
            if (!File.Exists(expandedPath))
            {
                throw new FileNotFoundException($"Referenced resource file was not found: {value}", expandedPath);
            }

            return ExportResource(expandedPath, null, staging, resourceState);
        }

        return value;
    }

    private string ExportResource(
        string sourcePath,
        string? originalRelativePath,
        string staging,
        ResourceExportState state)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException($"Referenced resource file was not found: {sourcePath}", fullSourcePath);
        }

        var sha256 = ComputeSha256(fullSourcePath);
        if (state.HashToUri.TryGetValue(sha256, out var existingUri))
        {
            return existingUri;
        }

        var extension = Path.GetExtension(fullSourcePath);
        var kind = GetResourceKind(originalRelativePath, extension);
        if (string.Equals(kind, "Image", StringComparison.Ordinal))
        {
            var validation = _imageSafetyService.ValidateFile(
                fullSourcePath,
                FrontedImagePurpose.PackageResource,
                knownBackgroundImage: originalRelativePath?.Contains("Background", StringComparison.OrdinalIgnoreCase) == true,
                knownUiImage: false);
            if (!validation.IsValid)
            {
                throw new InvalidDataException(validation.ErrorCode ?? "InvalidImageResource");
            }
        }

        var folder = kind switch
        {
            "Font" => "fonts",
            "Image" => "images",
            _ => "other"
        };
        var fileName = CreateResourceFileName(Path.GetFileNameWithoutExtension(fullSourcePath), sha256, extension);
        var relativePath = ToZipPath("resources", folder, fileName);
        var targetPath = Path.Combine(staging, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(fullSourcePath, targetPath, overwrite: false);

        var uri = $"bpui://{state.PackageId}/{relativePath}";
        state.HashToUri[sha256] = uri;
        state.Resources.Add(new FrontedLayoutPackageResourceEntry
        {
            Id = Path.GetFileNameWithoutExtension(fileName),
            Kind = kind,
            Path = relativePath,
            Uri = uri,
            Sha256 = sha256
        });

        return uri;
    }

    private bool TryResolveBpuiResource(
        string value,
        out string resolvedPath,
        out string relativePath)
    {
        resolvedPath = string.Empty;
        relativePath = string.Empty;
        if (!value.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "bpui", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var packageId = Uri.UnescapeDataString(uri.Host);
        if (!FrontedLayoutPackageManager.IsSafePackageId(packageId))
        {
            throw new InvalidOperationException($"Referenced bpui PackageId is not safe: {packageId}");
        }

        relativePath = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        EnsureSafeRelativePath(relativePath);
        var packageRoot = Path.GetFullPath(Path.Combine(_packageRoot, packageId));
        var packageRootWithSeparator = EnsureTrailingSeparator(packageRoot);
        var candidate = Path.GetFullPath(Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(packageRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Referenced bpui resource escaped its package root: {value}");
        }

        resolvedPath = candidate;
        return true;
    }

    private IReadOnlyList<LayoutExportEntry> CollectLayoutEntries(
        FrontedLayoutPackageExportRequest request,
        string sourcePackageId)
    {
        var layoutsRoot = _packageManager.GetPackageLayoutsRootFolder(sourcePackageId);
        if (!Directory.Exists(layoutsRoot))
        {
            return Array.Empty<LayoutExportEntry>();
        }

        var entries = new Dictionary<string, LayoutExportEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(layoutsRoot, "*.json", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(layoutsRoot, file).Replace('\\', '/');
            if (!FrontedV3LayoutWindowPathHelper.TryToCanonicalWindowIdFromLayoutRelativePath(
                    relativePath, out var canonicalWindowId))
            {
                continue;
            }

            entries[canonicalWindowId] = new LayoutExportEntry(canonicalWindowId, file, relativePath);
        }

        if (request.ExportScope == FrontedLayoutPackageExportScope.CurrentWindow)
        {
            if (string.IsNullOrWhiteSpace(request.WindowTypeName))
            {
                return Array.Empty<LayoutExportEntry>();
            }

            if (entries.TryGetValue(request.WindowTypeName, out var entry))
            {
                return [entry];
            }

            return Array.Empty<LayoutExportEntry>();
        }

        return entries.Values.ToArray();
    }

    private static FrontedLayoutPackageManifest CreateManifest(FrontedLayoutPackageExportRequest request)
    {
        return new FrontedLayoutPackageManifest
        {
            PackageId = request.PackageId,
            Name = request.Name,
            Description = request.Description,
            Author = request.Author,
            MinVersion = request.MinVersion,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static void ValidateRequest(FrontedLayoutPackageExportRequest request)
    {
        if (!IsSafePackageId(request.PackageId))
        {
            throw new ArgumentException("PackageId is invalid.", nameof(request));
        }

        if (!string.IsNullOrWhiteSpace(request.SourcePackageId)
            && !FrontedLayoutPackageManager.IsSafePackageId(request.SourcePackageId))
        {
            throw new ArgumentException("Source package ID is invalid.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Package name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(request));
        }

        if (request.ExportScope == FrontedLayoutPackageExportScope.CurrentWindow
            && string.IsNullOrWhiteSpace(request.WindowTypeName))
        {
            throw new ArgumentException("Current window export requires a selected window.", nameof(request));
        }
    }

    private static string NormalizeOutputPath(string outputPath)
    {
        var normalized = Path.GetFullPath(outputPath);
        return string.Equals(Path.GetExtension(normalized), ".bpui", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : Path.ChangeExtension(normalized, ".bpui");
    }

    private static bool ShouldInspectResourceProperty(string propertyName)
    {
        return string.Equals(propertyName, nameof(FrontedCanvasConfig.BackgroundImage), StringComparison.Ordinal)
               || string.Equals(propertyName, "FontFamily", StringComparison.Ordinal)
               || propertyName.EndsWith("FontFamily", StringComparison.Ordinal)
               || propertyName.EndsWith("ImagePath", StringComparison.Ordinal)
               || propertyName.EndsWith("ImageSource", StringComparison.Ordinal)
               || propertyName.EndsWith("ResourcePath", StringComparison.Ordinal)
               || propertyName.EndsWith("BackgroundImage", StringComparison.Ordinal)
               || propertyName.EndsWith("LockImageSource", StringComparison.Ordinal)
               || propertyName.EndsWith("BorderImagePath", StringComparison.Ordinal);
    }

    private static string GetResourceKind(string? originalRelativePath, string extension)
    {
        if (!string.IsNullOrWhiteSpace(originalRelativePath)
            && originalRelativePath.StartsWith("resources/fonts/", StringComparison.OrdinalIgnoreCase))
        {
            return "Font";
        }

        if (FontExtensions.Contains(extension))
        {
            return "Font";
        }

        return ImageExtensions.Contains(extension) ? "Image" : "Other";
    }

    private static string GetUriFragment(string value)
    {
        var index = value.IndexOf('#');
        return index >= 0 ? value[index..] : string.Empty;
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

    private static void EnsureSafePathSegment(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains(':', StringComparison.Ordinal)
            || value.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{name} is not safe: {value}");
        }
    }

    private static void EnsureSafeCanonicalWindowId(string value, string name)
    {
        if (!FrontedV3LayoutWindowPathHelper.IsSafeCanonicalWindowId(value))
        {
            throw new InvalidOperationException($"{name} is not safe: {value}");
        }
    }

    private static void EnsureSafeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\', StringComparison.Ordinal)
            || relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException($"Relative path is not safe: {relativePath}");
        }
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
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

    private sealed record LayoutExportEntry(
        string CanonicalWindowId,
        string SourcePath,
        string RelativePath);

    private sealed class ResourceExportState(string packageId)
    {
        public string PackageId { get; } = packageId;

        public Dictionary<string, string> HashToUri { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<FrontedLayoutPackageResourceEntry> Resources { get; } = [];
    }
}

#pragma warning restore CS1591
