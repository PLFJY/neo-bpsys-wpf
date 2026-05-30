using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using System.IO;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 默认 v3 前台资源解析器。
/// </summary>
public class FrontedResourceResolver(ILogger<FrontedResourceResolver> logger) : IFrontedResourceResolver
{
    /// <inheritdoc />
    public string? ResolveImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(path);
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
