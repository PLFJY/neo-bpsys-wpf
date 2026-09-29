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
/// 旧版前台布局包转换器的Archive逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageLegacyConverter
{
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
            CreatedVersion = AppConstants.AppVersion,
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

}

#pragma warning restore CS1591
