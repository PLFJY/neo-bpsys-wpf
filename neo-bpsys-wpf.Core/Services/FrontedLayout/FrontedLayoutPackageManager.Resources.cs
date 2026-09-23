#pragma warning disable CS1591

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台布局包管理器的Resources逻辑。
/// </summary>
public sealed partial class FrontedLayoutPackageManager
{
    private async Task WriteManifestAsync(
        string packagePath,
        FrontedLayoutPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(packagePath);
        var json = JsonSerializer.Serialize(manifest, _jsonSerializerOptions);
        await File.WriteAllTextAsync(Path.Combine(packagePath, ManifestFileName), json, cancellationToken);
    }

    private static IEnumerable<FrontedLayoutPackageLayoutEntry> EnumerateLayoutEntries(string packagePath)
    {
        var layoutsRoot = Path.Combine(packagePath, "FrontedLayouts");
        if (!Directory.Exists(layoutsRoot))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(layoutsRoot, "*.json", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(layoutsRoot, file);
            // 路径反解析统一委托给 FrontedV3LayoutWindowPathHelper，PackageManager 不再自实现。
            if (!FrontedV3LayoutWindowPathHelper.TryToCanonicalWindowIdFromLayoutRelativePath(
                    relativePath,
                    out var window))
            {
                continue;
            }

            yield return new FrontedLayoutPackageLayoutEntry
            {
                Window = window,
                Path = Path.Combine("FrontedLayouts", relativePath).Replace('\\', '/')
            };
        }
    }

    private static IEnumerable<FrontedLayoutPackageResourceEntry> EnumerateResourceEntries(string packagePath)
    {
        var resourcesRoot = Directory.Exists(Path.Combine(packagePath, "Resources"))
            ? Path.Combine(packagePath, "Resources")
            : Path.Combine(packagePath, "resources");
        if (!Directory.Exists(resourcesRoot))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(resourcesRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(packagePath, file).Replace('\\', '/');
            yield return new FrontedLayoutPackageResourceEntry
            {
                Id = relativePath,
                Kind = "File",
                Path = relativePath,
                Uri = relativePath
            };
        }
    }

    private async Task CopyDirectoryContentsAsync(
        string sourceRoot,
        string targetRoot,
        CancellationToken cancellationToken,
        IReadOnlySet<string>? excludedRootFiles = null)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        var fullTargetRoot = EnsureTrailingSeparator(Path.GetFullPath(targetRoot));
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceRoot, file);
            if (excludedRootFiles is not null
                && !relativePath.Contains(Path.DirectorySeparatorChar)
                && !relativePath.Contains(Path.AltDirectorySeparatorChar)
                && excludedRootFiles.Contains(relativePath))
            {
                continue;
            }

            if (!IsSafeRelativePath(relativePath))
            {
                throw new InvalidOperationException("Package file path is not safe.");
            }

            var targetPath = Path.Combine(targetRoot, relativePath);
            var fullTargetPath = Path.GetFullPath(targetPath);
            if (!fullTargetPath.StartsWith(fullTargetRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Package copy target escaped package root.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullTargetPath)!);
            await using var sourceStream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            await using var targetStream = new FileStream(
                fullTargetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            await sourceStream.CopyToAsync(targetStream, cancellationToken);
        }
    }

    private string LocalizedOrFallback(string key, string fallback)
    {
        var localized = _localize?.Invoke(key);
        return string.IsNullOrWhiteSpace(localized) || string.Equals(localized, key, StringComparison.Ordinal)
            ? fallback
            : localized;
    }

}
