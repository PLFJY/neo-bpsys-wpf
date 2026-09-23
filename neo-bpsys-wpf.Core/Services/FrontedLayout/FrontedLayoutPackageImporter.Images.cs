#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台布局包导入器的图片资源校验逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageImporter
{
    private List<FrontedLayoutPackageImageCompression>? CompressOversizedImages(
        string stagingRoot,
        IEnumerable<FrontedLayoutPackageResourceEntry> resources,
        IEnumerable<FrontedLayoutPackageImageIssue> issues,
        CancellationToken cancellationToken)
    {
        var resourcesByPath = resources.ToDictionary(resource => resource.Path, StringComparer.OrdinalIgnoreCase);
        var results = new List<FrontedLayoutPackageImageCompression>();
        foreach (var issue in issues)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resourcePath = CombineInsideRoot(stagingRoot, issue.ResourcePath);
            var compression = _imageCompressionService.CompressIfNeeded(
                resourcePath,
                FrontedImagePurpose.PackageResource);
            if (!compression.WasCompressed
                || !resourcesByPath.TryGetValue(issue.ResourcePath, out var resource))
            {
                return null;
            }

            resource.Sha256 = ComputeSha256(resourcePath);
            results.Add(new FrontedLayoutPackageImageCompression
            {
                ResourcePath = resource.Path,
                OriginalBytes = compression.OriginalBytes,
                CompressedBytes = compression.CompressedBytes
            });
        }

        return results;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
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

        if (FrontedTextLimitHelper.IsTooLong(
                manifest.CreatedVersion,
                FrontedLayoutLimits.MaxPackageCreatedVersionLength))
        {
            return "InputTooLong: CreatedVersion";
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

    private List<FrontedLayoutPackageImageIssue> FindOversizedArchiveImages(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var manifestEntry = archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName, ManifestFileName, StringComparison.OrdinalIgnoreCase));
        if (manifestEntry is null || manifestEntry.Length > FrontedLayoutLimits.MaxManifestBytes)
        {
            return [];
        }

        FrontedLayoutPackageManifest? manifest;
        try
        {
            using var stream = manifestEntry.Open();
            manifest = JsonSerializer.Deserialize<FrontedLayoutPackageManifest>(stream, _jsonSerializerOptions);
        }
        catch (JsonException)
        {
            return [];
        }

        if (manifest is null)
        {
            return [];
        }

        var imageResourcePaths = manifest.Content.Resources
            .Select(resource => resource.Path.Replace('\\', '/'))
            .Where(IsImageResource)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return archive.Entries
            .Select(entry => new { Entry = entry, Path = entry.FullName.Replace('\\', '/') })
            .Where(item => imageResourcePaths.Contains(item.Path)
                           && item.Entry.Length > FrontedLayoutLimits.MaxPackageSingleEntryBytes)
            .Select(item => new FrontedLayoutPackageImageIssue
            {
                ResourcePath = item.Path,
                ErrorCode = "ImageTooLarge",
                FileBytes = item.Entry.Length
            })
            .ToList();
    }
}

#pragma warning restore CS1591
