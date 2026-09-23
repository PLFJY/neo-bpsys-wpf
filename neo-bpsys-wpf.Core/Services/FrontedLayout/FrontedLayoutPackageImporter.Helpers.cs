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
/// 前台布局包导入器的通用辅助逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageImporter
{
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
            || !FrontedPackageVersionComparer.TryParseNumeric(minVersion, out var required)
            || !FrontedPackageVersionComparer.TryParseNumeric(AppConstants.AppVersion, out var current))
        {
            return false;
        }

        return required > current;
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

    private static bool TryGetExpectedWindowFromPath(string layoutPath, out string expectedWindow)
    {
        expectedWindow = string.Empty;
        var normalized = layoutPath.Replace('\\', '/');
        var prefix = "FrontedLayouts/";
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativeToLayoutsRoot = normalized[prefix.Length..];
        return FrontedV3LayoutWindowPathHelper.TryToCanonicalWindowIdFromLayoutRelativePath(
            relativeToLayoutsRoot,
            out expectedWindow);
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
