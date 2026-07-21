using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.CompilerServices;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public class BackgroundImageTintProcessor
{
    private const int MaxCacheEntries = 32;
    /// <summary>染色缓存解码后像素字节上限。与 <see cref="MaxCacheEntries"/> 共同限制缓存规模，先触发的先驱逐。</summary>
    private const long MaxCacheBytes = 64L * 1024 * 1024;
    private readonly object _cacheLock = new();
    private readonly Dictionary<TintCacheKey, BitmapSource> _cache = [];
    private readonly LinkedList<TintCacheKey> _cacheOrder = new();
    private readonly Dictionary<TintCacheKey, LinkedListNode<TintCacheKey>> _cacheNodes = new();
    private long _currentCacheBytes;

    public BitmapSource? CreateTinted(
        ImageSource source,
        string? sourceKey,
        Color tint,
        BackgroundTintMode mode,
        double strength,
        ILogger? logger = null) =>
        CreateTinted(source, sourceKey, tint, mode, strength, 0.45D, logger);

    public BitmapSource? CreateTinted(
        ImageSource source,
        string? sourceKey,
        Color tint,
        BackgroundTintMode mode,
        double strength,
        double textureStrength,
        ILogger? logger = null)
        => CreateTinted(
            source,
            sourceKey,
            tint,
            new BackgroundTintProcessingOptions
            {
                Mode = mode,
                TintStrength = strength,
                TextureStrength = textureStrength,
                NormalizationMode = BackgroundTintNormalizationMode.WholeImage
            },
            logger);

    public BitmapSource? CreateTinted(
        ImageSource source,
        string? sourceKey,
        Color tint,
        BackgroundTintProcessingOptions options,
        ILogger? logger = null)
    {
        var bitmap = ConvertToBitmapSource(source, logger);
        if (bitmap is null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return null;
        }

        var mode = options.Mode;
        var strength = double.IsFinite(options.TintStrength) ? Math.Clamp(options.TintStrength, 0D, 1D) : 1D;
        var textureStrength = double.IsFinite(options.TextureStrength)
            ? Math.Clamp(options.TextureStrength, 0D, 1D)
            : 0.45D;
        var normalization = NormalizeOptions(options, bitmap.PixelWidth, bitmap.PixelHeight);
        var key = new TintCacheKey(
            sourceKey ?? string.Empty,
            tint.R,
            tint.G,
            tint.B,
            mode,
            strength,
            textureStrength,
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            RuntimeHelpers.GetHashCode(bitmap),
            normalization.Mode,
            normalization.CanvasWidth,
            normalization.CanvasHeight,
            normalization.Region.X,
            normalization.Region.Y,
            normalization.Region.Width,
            normalization.Region.Height,
            normalization.PolygonHash);
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                // LRU: 命中时把条目移动到链表末尾（最近使用），保证稳定帧不会被中间动画帧驱逐。
                if (_cacheNodes.TryGetValue(key, out var node))
                {
                    _cacheOrder.Remove(node);
                    _cacheOrder.AddLast(node);
                }

                return cached;
            }
        }

        var converted = bitmap.Format == PixelFormats.Bgra32
            ? bitmap
            : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        var meanLuminance = mode == BackgroundTintMode.BaseColorWithTexture
            ? CalculateMeanLuminance(pixels, converted.PixelWidth, converted.PixelHeight, normalization)
            : 0D;

        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var sourceB = pixels[offset];
            var sourceG = pixels[offset + 1];
            var sourceR = pixels[offset + 2];
            byte targetR;
            byte targetG;
            byte targetB;

            if (mode == BackgroundTintMode.Multiply)
            {
                targetR = Multiply(sourceR, tint.R);
                targetG = Multiply(sourceG, tint.G);
                targetB = Multiply(sourceB, tint.B);
            }
            else if (mode == BackgroundTintMode.BaseColorWithTexture)
            {
                var luminance = CalculateLuminance(sourceR, sourceG, sourceB);
                var detail = (luminance - meanLuminance) * textureStrength;
                targetR = ClampToByte(tint.R + detail);
                targetG = ClampToByte(tint.G + detail);
                targetB = ClampToByte(tint.B + detail);
            }
            else
            {
                var luminance = (byte)Math.Clamp(
                    Math.Round(CalculateLuminance(sourceR, sourceG, sourceB)),
                    0D,
                    255D);
                targetR = Multiply(luminance, tint.R);
                targetG = Multiply(luminance, tint.G);
                targetB = Multiply(luminance, tint.B);
            }

            pixels[offset] = Interpolate(sourceB, targetB, strength);
            pixels[offset + 1] = Interpolate(sourceG, targetG, strength);
            pixels[offset + 2] = Interpolate(sourceR, targetR, strength);
            // Preserve source alpha exactly.
        }

        var result = BitmapSource.Create(
            converted.PixelWidth,
            converted.PixelHeight,
            converted.DpiX,
            converted.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        result.Freeze();
        Cache(key, result);
        return result;
    }

    private static BitmapSource? ConvertToBitmapSource(ImageSource source, ILogger? logger)
    {
        if (source is BitmapSource bitmap)
        {
            return bitmap;
        }

        try
        {
            var width = Math.Max(1, (int)Math.Ceiling(source.Width));
            var height = Math.Max(1, (int)Math.Ceiling(source.Height));
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen())
            {
                context.DrawImage(source, new Rect(0, 0, width, height));
            }

            var rendered = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rendered.Render(drawing);
            rendered.Freeze();
            return rendered;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Canvas background image could not be converted for tinting.");
            return null;
        }
    }

    private void Cache(TintCacheKey key, BitmapSource value)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                // 已存在的条目被覆盖：先扣除旧值字节数，再更新 LRU 位置。
                _currentCacheBytes -= EstimateBitmapBytes(existing);
                _cache[key] = value;
                _currentCacheBytes += EstimateBitmapBytes(value);
                if (_cacheNodes.TryGetValue(key, out var node))
                {
                    _cacheOrder.Remove(node);
                    _cacheOrder.AddLast(node);
                }
            }
            else
            {
                _cache[key] = value;
                _currentCacheBytes += EstimateBitmapBytes(value);
                var node = _cacheOrder.AddLast(key);
                _cacheNodes[key] = node;
            }

            // 双限制驱逐：条目数或字节预算任一超限，从链表头部（最久未使用）开始驱逐。
            while ((_cache.Count > MaxCacheEntries || _currentCacheBytes > MaxCacheBytes)
                   && _cacheOrder.First is { } firstNode)
            {
                var oldestKey = firstNode.Value;
                var oldestBitmap = _cache[oldestKey];
                _currentCacheBytes -= EstimateBitmapBytes(oldestBitmap);
                _cache.Remove(oldestKey);
                _cacheNodes.Remove(oldestKey);
                _cacheOrder.RemoveFirst();
            }

            // 字节估算可能因 PixelFormat 差异产生小量偏差，防止长期漂移导致负数。
            if (_currentCacheBytes < 0)
            {
                _currentCacheBytes = 0;
            }
        }
    }

    /// <summary>获取当前染色缓存条目数。仅供诊断使用，不改变生产生命周期。</summary>
    /// <returns>当前缓存中的条目数量。</returns>
    /// <remarks>该属性在已有锁下读取，不会反向持有任何缓存对象。</remarks>
    internal int CachedEntryCount
    {
        get
        {
            lock (_cacheLock)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>估算当前染色缓存占用的解码字节数。仅供诊断使用。</summary>
    /// <returns>缓存中所有染色位图的像素字节总和；无法识别格式的条目按 0 计算。</returns>
    /// <remarks>
    /// 估算公式为 <c>PixelWidth * PixelHeight * ceil(BitsPerPixel / 8)</c>。
    /// 该属性在已有锁下读取，使用 <see cref="long"/> 避免溢出，不改变生产生命周期。
    /// </remarks>
    internal long EstimatedCachedBytes
    {
        get
        {
            lock (_cacheLock)
            {
                long total = 0;
                foreach (var bitmap in _cache.Values)
                {
                    total += EstimateBitmapBytes(bitmap);
                }
                return total;
            }
        }
    }

    private static long EstimateBitmapBytes(BitmapSource? bitmap)
    {
        if (bitmap is null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
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

    private static byte Multiply(byte left, byte right) =>
        (byte)Math.Clamp(Math.Round(left * right / 255D), 0D, 255D);

    private static byte Interpolate(byte source, byte target, double strength) =>
        (byte)Math.Clamp(Math.Round(source + (target - source) * strength), 0D, 255D);

    private static double CalculateMeanLuminance(
        byte[] pixels,
        int pixelWidth,
        int pixelHeight,
        NormalizedProcessingOptions options)
    {
        var total = 0D;
        var count = 0;
        for (var y = 0; y < pixelHeight; y++)
        {
            for (var x = 0; x < pixelWidth; x++)
            {
                var offset = (y * pixelWidth + x) * 4;
                if (pixels[offset + 3] == 0 || !IncludesPixel(x, y, pixelWidth, pixelHeight, options))
                {
                    continue;
                }

                total += CalculateLuminance(pixels[offset + 2], pixels[offset + 1], pixels[offset]);
                count++;
            }
        }

        return count > 0
            ? total / count
            : CalculateWholeImageMeanLuminance(pixels);
    }

    private static double CalculateWholeImageMeanLuminance(byte[] pixels)
    {
        var total = 0D;
        var count = 0;
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            if (pixels[offset + 3] == 0)
            {
                continue;
            }

            total += CalculateLuminance(pixels[offset + 2], pixels[offset + 1], pixels[offset]);
            count++;
        }

        return count > 0 ? total / count : 0D;
    }

    private static bool IncludesPixel(
        int pixelX,
        int pixelY,
        int pixelWidth,
        int pixelHeight,
        NormalizedProcessingOptions options)
    {
        if (options.Mode == BackgroundTintNormalizationMode.WholeImage)
        {
            return true;
        }

        var canvasPoint = new Point(
            (pixelX + 0.5D) / pixelWidth * options.CanvasWidth,
            (pixelY + 0.5D) / pixelHeight * options.CanvasHeight);
        if (options.Mode == BackgroundTintNormalizationMode.VisiblePolygon
            && options.PolygonPoints is { Count: >= 3 })
        {
            return IsPointInsidePolygon(canvasPoint, options.PolygonPoints);
        }

        return options.Region.Contains(canvasPoint);
    }

    private static bool IsPointInsidePolygon(Point point, IReadOnlyList<Point> polygon)
    {
        var inside = false;
        for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
        {
            var currentPoint = polygon[current];
            var previousPoint = polygon[previous];
            if ((currentPoint.Y > point.Y) != (previousPoint.Y > point.Y)
                && point.X < (previousPoint.X - currentPoint.X) * (point.Y - currentPoint.Y)
                / (previousPoint.Y - currentPoint.Y) + currentPoint.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static NormalizedProcessingOptions NormalizeOptions(
        BackgroundTintProcessingOptions options,
        int pixelWidth,
        int pixelHeight)
    {
        var canvasWidth = double.IsFinite(options.CanvasWidth) && options.CanvasWidth > 0
            ? options.CanvasWidth
            : pixelWidth;
        var canvasHeight = double.IsFinite(options.CanvasHeight) && options.CanvasHeight > 0
            ? options.CanvasHeight
            : pixelHeight;
        var region = IsValidRegion(options.CanvasRegion)
            ? options.CanvasRegion
            : new Rect(0D, 0D, canvasWidth, canvasHeight);
        var polygon = CreateAbsolutePolygon(options.PolygonPoints, region);
        var mode = options.NormalizationMode switch
        {
            BackgroundTintNormalizationMode.VisibleMask when polygon.Count >= 3 =>
                BackgroundTintNormalizationMode.VisiblePolygon,
            BackgroundTintNormalizationMode.VisibleMask =>
                BackgroundTintNormalizationMode.VisibleRectangle,
            BackgroundTintNormalizationMode.VisiblePolygon when polygon.Count < 3 =>
                BackgroundTintNormalizationMode.VisibleRectangle,
            _ => options.NormalizationMode
        };
        return new NormalizedProcessingOptions(
            mode,
            canvasWidth,
            canvasHeight,
            region,
            polygon,
            CalculatePolygonHash(polygon));
    }

    private static bool IsValidRegion(Rect region) =>
        !region.IsEmpty
        && double.IsFinite(region.X)
        && double.IsFinite(region.Y)
        && double.IsFinite(region.Width)
        && double.IsFinite(region.Height)
        && region.Width > 0D
        && region.Height > 0D;

    private static IReadOnlyList<Point> CreateAbsolutePolygon(
        IReadOnlyList<PolygonVertexConfig>? points,
        Rect region) =>
        points?
            .Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y))
            .Select(point => new Point(
                region.X + PolygonVertexGeometryHelper.ClampCoordinate(point.X) * region.Width,
                region.Y + PolygonVertexGeometryHelper.ClampCoordinate(point.Y) * region.Height))
            .ToArray()
        ?? [];

    private static int CalculatePolygonHash(IReadOnlyList<Point> points)
    {
        var hash = new HashCode();
        foreach (var point in points)
        {
            hash.Add(point.X);
            hash.Add(point.Y);
        }

        return hash.ToHashCode();
    }

    private static byte ClampToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value), 0D, 255D);

    private static double CalculateLuminance(byte red, byte green, byte blue) =>
        red * 0.2126D + green * 0.7152D + blue * 0.0722D;

    private readonly record struct NormalizedProcessingOptions(
        BackgroundTintNormalizationMode Mode,
        double CanvasWidth,
        double CanvasHeight,
        Rect Region,
        IReadOnlyList<Point> PolygonPoints,
        int PolygonHash);

    private readonly record struct TintCacheKey(
        string SourceKey,
        byte R,
        byte G,
        byte B,
        BackgroundTintMode Mode,
        double Strength,
        double TextureStrength,
        int PixelWidth,
        int PixelHeight,
        int SourceIdentity,
        BackgroundTintNormalizationMode NormalizationMode,
        double CanvasWidth,
        double CanvasHeight,
        double RegionX,
        double RegionY,
        double RegionWidth,
        double RegionHeight,
        int PolygonHash);
}
