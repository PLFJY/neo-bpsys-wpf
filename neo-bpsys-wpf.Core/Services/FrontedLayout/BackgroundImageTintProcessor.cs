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
    /// <summary>启用行级并行处理的像素数阈值；普通 1080p 区域保持串行，仅超大区域才并行以避免调度开销。</summary>
    private const long ParallelPixelThreshold = 2_500_000L;

    private readonly FrontedRenderCache<TintCacheKey, BitmapSource> _cache =
        new(MaxCacheEntries, MaxCacheBytes, EstimateBitmapBytes);

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
        // 只有 BaseColorWithTexture 模式的输出依赖可见区域的局部平均亮度；其余模式逐像素计算，
        // 与区域、Canvas 尺寸和归一化模式无关。缓存键忽略这些输入后，同源同色的不同染色区域
        // （以及不同窗口中的同类控件）可以复用同一张整图染色结果。
        var regionSensitive = mode == BackgroundTintMode.BaseColorWithTexture;
        var restrictToRegion = regionSensitive && options.RestrictProcessingToRegion;
        var key = regionSensitive
            ? new TintCacheKey(
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
                normalization.PolygonHash,
                restrictToRegion)
            : new TintCacheKey(
                sourceKey ?? string.Empty,
                tint.R,
                tint.G,
                tint.B,
                mode,
                strength,
                0D,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                RuntimeHelpers.GetHashCode(bitmap),
                BackgroundTintNormalizationMode.WholeImage,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                0D,
                0D,
                0D,
                0D,
                0,
                false);

        if (_cache.TryGet(key, out var cached))
        {
            return cached;
        }

        var converted = bitmap.Format == PixelFormats.Bgra32
            ? bitmap
            : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        var bounds = restrictToRegion
            ? GetPixelBounds(normalization, converted.PixelWidth, converted.PixelHeight)
            : new PixelBounds(0, 0, converted.PixelWidth, converted.PixelHeight);
        var meanLuminance = mode == BackgroundTintMode.BaseColorWithTexture
            ? CalculateMeanLuminance(pixels, converted.PixelWidth, converted.PixelHeight, normalization, bounds, restrictToRegion)
            : 0D;

        // 区域限制模式下只遍历可见区域；区域外保留源图像素，由宿主裁剪，视觉结果与整图处理后裁剪一致。
        void ProcessRow(int y)
        {
            var rowOffset = y * stride;
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                var offset = rowOffset + (x * 4);
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
                // Preserve source alpha exactly inside the processed region.
            }
        }

        var pixelCount = (long)(bounds.Right - bounds.Left) * (bounds.Bottom - bounds.Top);
        if (pixelCount >= ParallelPixelThreshold)
        {
            // 大面积整图染色在后台线程执行，按行并行可显著缩短缓存填充时间；小图保持串行避免调度开销。
            System.Threading.Tasks.Parallel.For(bounds.Top, bounds.Bottom, ProcessRow);
        }
        else
        {
            for (var y = bounds.Top; y < bounds.Bottom; y++)
            {
                ProcessRow(y);
            }
        }

        if (restrictToRegion)
        {
            // 区域外像素不参与可见渲染；清零 alpha 避免裁剪边缘出现未染色源图残影。
            ClearOutsideBounds(pixels, stride, converted.PixelWidth, converted.PixelHeight, bounds);
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
        _cache.Set(key, result);
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

    /// <summary>获取当前染色缓存条目数。仅供诊断使用，不改变生产生命周期。</summary>
    /// <returns>当前缓存中的条目数量。</returns>
    /// <remarks>该属性线程安全，不会反向持有任何缓存对象。</remarks>
    internal int CachedEntryCount => _cache.Count;

    /// <summary>估算当前染色缓存占用的解码字节数。仅供诊断使用。</summary>
    /// <returns>缓存中所有染色位图的像素字节总和；无法识别格式的条目按 0 计算。</returns>
    /// <remarks>
    /// 估算公式为 <c>PixelWidth * PixelHeight * ceil(BitsPerPixel / 8)</c>。
    /// 该属性线程安全，使用 <see cref="long"/> 避免溢出，不改变生产生命周期。
    /// </remarks>
    internal long EstimatedCachedBytes => _cache.EstimatedBytes;

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
        NormalizedProcessingOptions options,
        PixelBounds bounds,
        bool restrictToRegion)
    {
        var total = 0D;
        var count = 0;
        // WholeImage 归一化仍需要全图均值；其余模式下区域外像素不可见，只需统计可见区域。
        var restrictMeanToBounds = restrictToRegion
            && options.Mode != BackgroundTintNormalizationMode.WholeImage;
        var top = restrictMeanToBounds ? bounds.Top : 0;
        var bottom = restrictMeanToBounds ? bounds.Bottom : pixelHeight;
        var left = restrictMeanToBounds ? bounds.Left : 0;
        var right = restrictMeanToBounds ? bounds.Right : pixelWidth;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var offset = ((y * pixelWidth) + x) * 4;
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

    private static void ClearOutsideBounds(
        byte[] pixels,
        int stride,
        int pixelWidth,
        int pixelHeight,
        PixelBounds bounds)
    {
        var rowBytes = pixelWidth * 4;
        for (var y = 0; y < pixelHeight; y++)
        {
            var rowOffset = y * stride;
            if (y < bounds.Top || y >= bounds.Bottom)
            {
                Array.Clear(pixels, rowOffset, rowBytes);
                continue;
            }

            if (bounds.Left > 0)
            {
                Array.Clear(pixels, rowOffset, bounds.Left * 4);
            }

            if (bounds.Right < pixelWidth)
            {
                Array.Clear(pixels, rowOffset + (bounds.Right * 4), (pixelWidth - bounds.Right) * 4);
            }
        }
    }

    private static PixelBounds GetPixelBounds(
        NormalizedProcessingOptions options,
        int pixelWidth,
        int pixelHeight)
    {
        var canvasWidth = options.CanvasWidth > 0D ? options.CanvasWidth : pixelWidth;
        var canvasHeight = options.CanvasHeight > 0D ? options.CanvasHeight : pixelHeight;
        var region = options.Region;
        var left = (int)Math.Floor(region.X / canvasWidth * pixelWidth);
        var top = (int)Math.Floor(region.Y / canvasHeight * pixelHeight);
        var right = (int)Math.Ceiling((region.X + region.Width) / canvasWidth * pixelWidth);
        var bottom = (int)Math.Ceiling((region.Y + region.Height) / canvasHeight * pixelHeight);

        left = Math.Clamp(left, 0, pixelWidth - 1);
        top = Math.Clamp(top, 0, pixelHeight - 1);
        right = Math.Clamp(right, left + 1, pixelWidth);
        bottom = Math.Clamp(bottom, top + 1, pixelHeight);
        return new PixelBounds(left, top, right, bottom);
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
        int PolygonHash,
        bool RestrictToRegion);

    private readonly record struct PixelBounds(int Left, int Top, int Right, int Bottom);
}
