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
    private readonly LinkedList<ImageCacheKey> _imageCacheOrder = new();
    private readonly Dictionary<ImageCacheKey, LinkedListNode<ImageCacheKey>> _imageCacheNodes = new();
    private long _currentCachedBytes;
    private const int MaxCachedImages = 256;
    /// <summary>图片缓存解码后像素字节上限。与 <see cref="MaxCachedImages"/> 共同限制缓存规模，先触发的先驱逐。</summary>
    private const long MaxCachedBytes = 128L * 1024 * 1024;

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
                // LRU: 命中时把条目移动到链表末尾（最近使用），保证频繁使用的图片不会被中间动画帧驱逐。
                if (_imageCacheNodes.TryGetValue(cacheKey, out var node))
                {
                    _imageCacheOrder.Remove(node);
                    _imageCacheOrder.AddLast(node);
                }

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
            _imageCacheNodes.Clear();
            _currentCachedBytes = 0;
        }
    }

    /// <summary>获取当前图片缓存条目数。仅供诊断使用，不改变生产生命周期。</summary>
    /// <returns>当前缓存中的条目数量。</returns>
    /// <remarks>该属性在已有锁下读取，不会反向持有任何缓存对象。</remarks>
    internal int CachedEntryCount
    {
        get
        {
            lock (_imageCacheLock)
            {
                return _imageCache.Count;
            }
        }
    }

    /// <summary>估算当前图片缓存占用的解码字节数。仅供诊断使用。</summary>
    /// <returns>缓存中所有图片解码后的像素字节总和；无法识别格式的条目按 0 计算。</returns>
    /// <remarks>
    /// 估算公式为 <c>PixelWidth * PixelHeight * ceil(BitsPerPixel / 8)</c>。
    /// 该属性在已有锁下读取，使用 <see cref="long"/> 避免溢出，不改变生产生命周期。
    /// </remarks>
    internal long EstimatedCachedBytes
    {
        get
        {
            lock (_imageCacheLock)
            {
                long total = 0;
                foreach (var image in _imageCache.Values)
                {
                    total += EstimateImageBytes(image);
                }
                return total;
            }
        }
    }

    private static long EstimateImageBytes(ImageSource? image)
    {
        if (image is not BitmapSource bitmap || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return 0;
        }

        var bitsPerPixel = bitmap.Format.BitsPerPixel;
        if (bitsPerPixel <= 0)
        {
            bitsPerPixel = 32;
        }

        return (long)bitmap.PixelWidth * bitmap.PixelHeight * ((bitsPerPixel + 7) / 8);
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
            if (_imageCache.TryGetValue(key, out var existing))
            {
                // 已存在的条目被覆盖：先扣除旧值字节数，再更新 LRU 位置。
                _currentCachedBytes -= EstimateImageBytes(existing);
                _imageCache[key] = image;
                _currentCachedBytes += EstimateImageBytes(image);
                if (_imageCacheNodes.TryGetValue(key, out var node))
                {
                    _imageCacheOrder.Remove(node);
                    _imageCacheOrder.AddLast(node);
                }
            }
            else
            {
                _imageCache[key] = image;
                _currentCachedBytes += EstimateImageBytes(image);
                var node = _imageCacheOrder.AddLast(key);
                _imageCacheNodes[key] = node;
            }

            // 双限制驱逐：条目数或字节预算任一超限，从链表头部（最久未使用）开始驱逐。
            while ((_imageCache.Count > MaxCachedImages || _currentCachedBytes > MaxCachedBytes)
                   && _imageCacheOrder.First is { } firstNode)
            {
                var oldestKey = firstNode.Value;
                var oldestImage = _imageCache[oldestKey];
                _currentCachedBytes -= EstimateImageBytes(oldestImage);
                _imageCache.Remove(oldestKey);
                _imageCacheNodes.Remove(oldestKey);
                _imageCacheOrder.RemoveFirst();
            }

            // 字节估算可能因 PixelFormat 差异产生小量偏差，防止长期漂移导致负数。
            if (_currentCachedBytes < 0)
            {
                _currentCachedBytes = 0;
            }
        }
    }

    private readonly record struct ImageCacheKey(
        string Path,
        FrontedImagePurpose Purpose,
        long FileBytes,
        long LastWriteTicks);
}
