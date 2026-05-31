using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 默认 v3 前台资源解析器。
/// </summary>
public class FrontedResourceResolver(ILogger<FrontedResourceResolver> logger) : IFrontedResourceResolver
{
    private static readonly Regex SafePackageIdRegex = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public string? ResolveImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(path);
        if (TryResolveBpuiPath(expandedPath, out var bpuiPath))
        {
            return bpuiPath;
        }

        if (Path.IsPathRooted(expandedPath))
        {
            return File.Exists(expandedPath) ? expandedPath : null;
        }

        var normalized = expandedPath.Replace('\\', '/');
        string resolvedPath;
        if (normalized.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
        {
            resolvedPath = Path.Combine(
                AppConstants.ResourcesPath,
                "bpui",
                normalized["Resources/".Length..].Replace('/', Path.DirectorySeparatorChar));
        }
        else
        {
            resolvedPath = Path.Combine(
                AppConstants.ResourcesPath,
                "bpui",
                normalized.Replace('/', Path.DirectorySeparatorChar));
        }

        return File.Exists(resolvedPath) ? resolvedPath : null;
    }

    private static bool TryResolveBpuiPath(string value, out string? resolvedPath)
    {
        resolvedPath = null;
        if (!value.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "bpui", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var packageId = Uri.UnescapeDataString(uri.Host);
        if (!IsSafePackageId(packageId))
        {
            return true;
        }

        var relativePath = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (!IsSafeRelativePath(relativePath))
        {
            return true;
        }

        var packageRoot = Path.GetFullPath(Path.Combine(AppConstants.FrontedLayoutPackagesPath, packageId));
        var packageRootWithSeparator = EnsureTrailingSeparator(packageRoot);
        var candidate = Path.GetFullPath(Path.Combine(
            packageRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!candidate.StartsWith(packageRootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, packageRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        resolvedPath = File.Exists(candidate) ? candidate : null;
        return true;
    }

    private static bool IsSafePackageId(string packageId)
    {
        return !string.IsNullOrWhiteSpace(packageId)
               && SafePackageIdRegex.IsMatch(packageId)
               && !packageId.Contains("..", StringComparison.Ordinal)
               && !packageId.Contains('%', StringComparison.Ordinal);
    }

    private static bool IsSafeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        return relativePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment != "." && segment != "..");
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    /// <inheritdoc />
    public ImageSource? ResolveImage(string? path)
    {
        var resolvedPath = ResolveImagePath(path);
        if (resolvedPath is null)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                logger.LogWarning("Fronted resource image path could not be resolved: {Path}", path);
            }

            return null;
        }

        return ImageHelper.GetImageFromUriStr(resolvedPath);
    }
}
