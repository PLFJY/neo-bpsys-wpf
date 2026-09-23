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
/// 前台布局包导入器的压缩包安全处理逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageImporter
{
    private static void ExtractZipSafely(string zipPath, string stagingRoot, bool allowImageCompression)
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
            var entryName = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(entryName)
                || Path.IsPathRooted(entryName)
                || entryName.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            {
                throw new InvalidDataException($"Unsafe zip entry: {entry.FullName}");
            }

            var maxEntryBytes = allowImageCompression && IsImageResource(entryName)
                ? FrontedLayoutLimits.MaxCompressiblePackageImageSourceBytes
                : FrontedLayoutLimits.MaxPackageSingleEntryBytes;
            if (entry.Length > maxEntryBytes)
            {
                throw new InvalidDataException("PackageEntryTooLarge");
            }

            totalUncompressedBytes += entry.Length;
            if (totalUncompressedBytes > FrontedLayoutLimits.MaxPackageExtractedBytes)
            {
                throw new InvalidDataException("PackageExtractedTooLarge");
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
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (!string.Equals(segment, "Plugins", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(segment, "Plugin", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // v3 window-centric 布局路径约定在 FrontedLayouts/ 和 FrontedBehaviors/ 下使用
            // "plugin" 作为结构性目录名，例如：
            //   FrontedLayouts/plugin/{PackageId}/{LocalId}.json
            //   FrontedBehaviors/plugin/{PackageId}/{LocalId}.behaviors.json
            // 这些是 JSON 布局/行为文件，不是可执行插件载荷，应当允许。
            // 只有非结构位置（例如根级 Plugins/ 安装目录）才被视为威胁。
            if (index == 1
                && (string.Equals(segments[0], "FrontedLayouts", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segments[0], "FrontedBehaviors", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

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

    private static bool IsFontResource(string path)
    {
        return path.StartsWith("resources/fonts/", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase);
    }
}

#pragma warning restore CS1591
