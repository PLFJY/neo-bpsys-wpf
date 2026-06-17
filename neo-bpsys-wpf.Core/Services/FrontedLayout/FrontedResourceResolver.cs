using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 默认 v3 前台资源解析器。
/// </summary>
public class FrontedResourceResolver : IFrontedResourceResolver
{
    private static readonly Regex SafePackageIdRegex = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger<FrontedResourceResolver> _logger;
    private readonly IFrontedImageSafetyService _imageSafetyService;
    private readonly object _imageCacheLock = new();
    private readonly Dictionary<ImageCacheKey, ImageSource?> _imageCache = new();
    private readonly Queue<ImageCacheKey> _imageCacheOrder = new();
    private const int MaxCachedImages = 256;

    public FrontedResourceResolver(ILogger<FrontedResourceResolver> logger)
        : this(logger, new FrontedImageSafetyService())
    {
    }

    public FrontedResourceResolver(
        ILogger<FrontedResourceResolver> logger,
        IFrontedImageSafetyService imageSafetyService)
    {
        _logger = logger;
        _imageSafetyService = imageSafetyService;
    }

    /// <inheritdoc />
    public string? ResolveImagePath(string? path)
    {
        return ResolveFilePath(path);
    }

    /// <inheritdoc />
    public string? ResolveFilePath(string? path)
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
    public ImageSource? ResolveImage(
        string? path,
        FrontedImagePurpose purpose = FrontedImagePurpose.PackageResource)
    {
        var resolvedPath = ResolveImagePath(path);
        if (resolvedPath is null)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _logger.LogWarning("Fronted resource image path could not be resolved: {Path}", path);
            }

            return null;
        }

        if (!TryCreateImageCacheKey(resolvedPath, purpose, out var cacheKey))
        {
            return null;
        }

        lock (_imageCacheLock)
        {
            if (_imageCache.TryGetValue(cacheKey, out var cachedImage))
            {
                return cachedImage;
            }
        }

        var validation = _imageSafetyService.ValidateFile(resolvedPath, purpose);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Fronted resource image was rejected. Path: {Path}, Code: {Code}",
                resolvedPath,
                validation.ErrorCode);
            CacheImage(cacheKey, null);
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(resolvedPath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            var longSide = Math.Max(validation.PixelWidth, validation.PixelHeight);
            if (longSide > 1024)
            {
                bitmap.DecodePixelWidth = validation.PixelWidth >= validation.PixelHeight ? 1024 : 0;
                bitmap.DecodePixelHeight = validation.PixelHeight > validation.PixelWidth ? 1024 : 0;
            }

            bitmap.EndInit();
            bitmap.Freeze();
            CacheImage(cacheKey, bitmap);
            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fronted resource image could not be decoded safely: {Path}", resolvedPath);
            CacheImage(cacheKey, null);
            return null;
        }
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        lock (_imageCacheLock)
        {
            _imageCache.Clear();
            _imageCacheOrder.Clear();
        }
    }

    private static bool TryCreateImageCacheKey(
        string resolvedPath,
        FrontedImagePurpose purpose,
        out ImageCacheKey key)
    {
        key = default;
        try
        {
            var info = new FileInfo(resolvedPath);
            if (!info.Exists)
            {
                return false;
            }

            key = new ImageCacheKey(
                Path.GetFullPath(resolvedPath),
                purpose,
                info.Length,
                info.LastWriteTimeUtc.Ticks);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void CacheImage(ImageCacheKey key, ImageSource? image)
    {
        lock (_imageCacheLock)
        {
            if (!_imageCache.ContainsKey(key))
            {
                _imageCacheOrder.Enqueue(key);
            }

            _imageCache[key] = image;
            while (_imageCacheOrder.Count > MaxCachedImages)
            {
                _imageCache.Remove(_imageCacheOrder.Dequeue());
            }
        }
    }

    private readonly record struct ImageCacheKey(
        string Path,
        FrontedImagePurpose Purpose,
        long FileBytes,
        long LastWriteTicks);
}
