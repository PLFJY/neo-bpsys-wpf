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

    private readonly FrontedDesignerLayoutCatalog _layoutCatalog;
    private readonly IFrontedLayoutService _layoutService;
    private readonly IFrontedWindowLayoutOptionsService _windowLayoutOptionsService;
    private readonly string _packageRoot;
    private readonly string _tempRoot;
    private readonly ILogger<FrontedLayoutPackageExporter> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public FrontedLayoutPackageExporter(
        FrontedDesignerLayoutCatalog layoutCatalog,
        IFrontedLayoutService layoutService,
        IFrontedWindowLayoutOptionsService windowLayoutOptionsService,
        ILogger<FrontedLayoutPackageExporter> logger)
        : this(
            layoutCatalog,
            layoutService,
            windowLayoutOptionsService,
            AppConstants.FrontedLayoutPackagesPath,
            Path.Combine(AppConstants.AppTempPath, "bpui-export"),
            logger)
    {
    }

    public FrontedLayoutPackageExporter(
        FrontedDesignerLayoutCatalog layoutCatalog,
        IFrontedLayoutService layoutService,
        IFrontedWindowLayoutOptionsService windowLayoutOptionsService,
        string packageRoot,
        string tempRoot,
        ILogger<FrontedLayoutPackageExporter>? logger = null)
    {
        _layoutCatalog = layoutCatalog;
        _layoutService = layoutService;
        _windowLayoutOptionsService = windowLayoutOptionsService;
        _packageRoot = packageRoot;
        _tempRoot = tempRoot;
        _logger = logger ?? NullLogger<FrontedLayoutPackageExporter>.Instance;
    }

    public async Task<FrontedLayoutPackageExportResult> ExportAsync(
        FrontedLayoutPackageExportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateRequest(request);
            var entries = SelectEntries(request);
            if (entries.Count == 0)
            {
                throw new InvalidOperationException("No Designer v3 layouts are available for the selected export scope.");
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
                await ExportWindowOptionsAsync(staging, entries, cancellationToken);
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
        IReadOnlyList<FrontedDesignerLayoutCatalogEntry> entries,
        FrontedLayoutPackageManifest manifest,
        ResourceExportState resourceState,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSafePathSegment(entry.WindowTypeName, nameof(entry.WindowTypeName));
            EnsureSafePathSegment(entry.CanvasName, nameof(entry.CanvasName));

            var loadResult = await _layoutService.LoadCanvasConfigWithMetadataAsync(
                entry.WindowTypeName,
                entry.CanvasName,
                cancellationToken);
            var config = loadResult.Config
                         ?? throw new InvalidOperationException(
                             $"Layout {entry.WindowTypeName}/{entry.CanvasName} could not be loaded.");

            if (config.Version != 3)
            {
                throw new InvalidOperationException(
                    $"Layout {entry.WindowTypeName}/{entry.CanvasName} has unsupported Version {config.Version}.");
            }

            var layoutJson = JsonSerializer.Serialize(config, _jsonSerializerOptions);
            var node = JsonNode.Parse(layoutJson)
                       ?? throw new InvalidOperationException(
                           $"Layout {entry.WindowTypeName}/{entry.CanvasName} serialized to empty JSON.");
            RewriteResourcePaths(node, null, staging, resourceState);

            var relativePath = ToZipPath("layouts", entry.WindowTypeName, $"{entry.CanvasName}.json");
            var targetPath = Path.Combine(staging, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, node.ToJsonString(_jsonSerializerOptions), cancellationToken);

            manifest.Content.Layouts.Add(new FrontedLayoutPackageLayoutEntry
            {
                Window = entry.WindowTypeName,
                Canvas = entry.CanvasName,
                Path = relativePath
            });
        }
    }

    private async Task ExportWindowOptionsAsync(
        string staging,
        IReadOnlyList<FrontedDesignerLayoutCatalogEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (var windowTypeName in entries.Select(entry => entry.WindowTypeName).Distinct(StringComparer.Ordinal))
        {
            var optionsPath = _windowLayoutOptionsService.GetUserOptionsPath(windowTypeName);
            if (!File.Exists(optionsPath))
            {
                continue;
            }

            var options = _windowLayoutOptionsService.LoadOptions(windowTypeName);
            if (options is { Version: 3, AllowTransparency: false })
            {
                continue;
            }

            var relativePath = ToZipPath("layouts", windowTypeName, "window.json");
            var targetPath = Path.Combine(staging, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var json = JsonSerializer.Serialize(options, _jsonSerializerOptions);
            await File.WriteAllTextAsync(targetPath, json, cancellationToken);
        }
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
            return ExportResource(bpuiPath, bpuiRelativePath, staging, resourceState);
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

    private IReadOnlyList<FrontedDesignerLayoutCatalogEntry> SelectEntries(FrontedLayoutPackageExportRequest request)
    {
        var entries = _layoutCatalog.GetEntries().Where(entry => entry is { IsMigrated: true, IsEditable: true });
        return request.ExportScope switch
        {
            FrontedLayoutPackageExportScope.CurrentCanvas => entries
                .Where(entry => string.Equals(entry.WindowTypeName, request.WindowTypeName, StringComparison.Ordinal)
                                && string.Equals(entry.CanvasName, request.CanvasName, StringComparison.Ordinal))
                .ToArray(),
            FrontedLayoutPackageExportScope.CurrentWindow => entries
                .Where(entry => string.Equals(entry.WindowTypeName, request.WindowTypeName, StringComparison.Ordinal))
                .ToArray(),
            _ => entries.ToArray()
        };
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

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Package name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(request));
        }

        if (request.ExportScope == FrontedLayoutPackageExportScope.CurrentCanvas
            && (string.IsNullOrWhiteSpace(request.WindowTypeName) || string.IsNullOrWhiteSpace(request.CanvasName)))
        {
            throw new ArgumentException("Current canvas export requires a selected window and canvas.", nameof(request));
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

        return ImageExtensions.Contains(extension) ? "Image" : "Other";
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

    private sealed class ResourceExportState(string packageId)
    {
        public string PackageId { get; } = packageId;

        public Dictionary<string, string> HashToUri { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<FrontedLayoutPackageResourceEntry> Resources { get; } = [];
    }
}

#pragma warning restore CS1591
