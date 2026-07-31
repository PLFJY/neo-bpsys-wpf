using SkiaSharp;
using System.IO;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 使用 SkiaSharp 对导入暂存区中的前台图片执行有上限的等比压缩。
/// </summary>
public sealed class FrontedImageCompressionService
{
    private const int MaxCompressionAttempts = 12;
    private const int MaxDecodeLongSide = 16384;
    private const long MaxDecodePixels = 64L * 1024 * 1024;

    private static readonly int[] JpegQualitySteps = [88, 80, 72, 64, 56];

    /// <summary>
    /// 当图片超过指定用途的安全阈值时，就地压缩该图片。
    /// 支持 PNG、JPEG、BMP、GIF、WebP 和 ICO；不能被 SkiaSharp 安全重编码的格式保持原文件不变。
    /// </summary>
    /// <param name="path">导入暂存区中的图片路径。</param>
    /// <param name="purpose">图片用途，用于选择文件体积和像素阈值。</param>
    /// <returns>压缩结果；未超过阈值或格式不受支持时，<see cref="FrontedImageCompressionResult.WasCompressed"/> 为 <see langword="false"/>。</returns>
    public FrontedImageCompressionResult CompressIfNeeded(string path, FrontedImagePurpose purpose)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return FrontedImageCompressionResult.Failed("ImageNotFound");
        }

        var extension = Path.GetExtension(path);
        var format = extension.ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".bmp" => SKEncodedImageFormat.Bmp,
            ".gif" => SKEncodedImageFormat.Gif,
            ".webp" => SKEncodedImageFormat.Webp,
            ".ico" => SKEncodedImageFormat.Ico,
            _ => (SKEncodedImageFormat?)null
        };
        if (format is null)
        {
            return FrontedImageCompressionResult.NotCompressed(new FileInfo(path).Length);
        }

        var originalBytes = new FileInfo(path).Length;
        try
        {
            using var encodedData = SKData.Create(path);
            using var codec = SKCodec.Create(encodedData);
            if (codec is null)
            {
                return FrontedImageCompressionResult.Failed("UnsupportedImageFormat", originalBytes);
            }

            var sourceInfo = codec.Info;
            if (sourceInfo.Width <= 0 || sourceInfo.Height <= 0)
            {
                return FrontedImageCompressionResult.Failed("UnsupportedImageDimensions", originalBytes);
            }

            if (sourceInfo.Width > MaxDecodeLongSide
                || sourceInfo.Height > MaxDecodeLongSide
                || (long)sourceInfo.Width * sourceInfo.Height > MaxDecodePixels)
            {
                return FrontedImageCompressionResult.Failed("ImageDecodeLimitExceeded", originalBytes);
            }

            var (maxBytes, maxLongSide, maxPixels) = GetLimits(purpose);
            var dimensionScale = CalculateDimensionScale(
                sourceInfo.Width,
                sourceInfo.Height,
                maxLongSide,
                maxPixels);
            if (originalBytes <= maxBytes && dimensionScale >= 1D)
            {
                return FrontedImageCompressionResult.NotCompressed(
                    originalBytes,
                    sourceInfo.Width,
                    sourceInfo.Height);
            }

            using var source = SKBitmap.Decode(path);
            if (source is null)
            {
                return FrontedImageCompressionResult.Failed("UnsupportedImageFormat", originalBytes);
            }

            var targetBytes = Math.Max(1L, (long)Math.Floor(maxBytes * 0.95D));
            var scale = Math.Min(1D, dimensionScale);
            for (var attempt = 0; attempt < MaxCompressionAttempts; attempt++)
            {
                var width = Math.Max(1, (int)Math.Floor(source.Width * scale));
                var height = Math.Max(1, (int)Math.Floor(source.Height * scale));
                using var resized = Resize(source, width, height);
                if (resized is null)
                {
                    return FrontedImageCompressionResult.Failed("ImageResizeFailed", originalBytes);
                }

                byte[]? smallestCandidate = null;
                foreach (var quality in GetQualitySteps(format.Value))
                {
                    var candidate = Encode(resized, format.Value, quality);
                    if (candidate is null)
                    {
                        continue;
                    }

                    if (smallestCandidate is null || candidate.Length < smallestCandidate.Length)
                    {
                        smallestCandidate = candidate;
                    }

                    if (candidate.LongLength <= targetBytes)
                    {
                        ReplaceFile(path, candidate);
                        return FrontedImageCompressionResult.Compressed(
                            originalBytes,
                            candidate.LongLength,
                            sourceInfo.Width,
                            sourceInfo.Height,
                            width,
                            height);
                    }
                }

                if (smallestCandidate is null)
                {
                    return FrontedImageCompressionResult.Failed("ImageEncodeFailed", originalBytes);
                }

                var sizeScale = Math.Sqrt(targetBytes / (double)smallestCandidate.LongLength) * 0.94D;
                scale *= Math.Clamp(sizeScale, 0.5D, 0.9D);
            }

            return FrontedImageCompressionResult.Failed("ImageCompressionLimitNotReached", originalBytes);
        }
        catch (Exception ex)
        {
            return FrontedImageCompressionResult.Failed(ex.GetType().Name, originalBytes);
        }
    }

    private static (long MaxBytes, int MaxLongSide, long MaxPixels) GetLimits(FrontedImagePurpose purpose) =>
        purpose switch
        {
            FrontedImagePurpose.Background => (
                FrontedLayoutLimits.MaxBackgroundImageBytes,
                FrontedLayoutLimits.MaxBackgroundImageLongSide,
                FrontedLayoutLimits.MaxBackgroundImagePixels),
            FrontedImagePurpose.PackageResource => (
                FrontedLayoutLimits.MaxPackageSingleEntryBytes,
                FrontedLayoutLimits.MaxBackgroundImageLongSide,
                FrontedLayoutLimits.MaxBackgroundImagePixels),
            _ => (
                FrontedLayoutLimits.MaxUiImageBytes,
                FrontedLayoutLimits.MaxUiImageLongSide,
                FrontedLayoutLimits.MaxUiImagePixels)
        };

    private static double CalculateDimensionScale(int width, int height, int maxLongSide, long maxPixels)
    {
        var scale = Math.Min(1D, maxLongSide / (double)Math.Max(width, height));
        var pixels = (long)width * height;
        if (pixels > maxPixels)
        {
            scale = Math.Min(scale, Math.Sqrt(maxPixels / (double)pixels));
        }

        return scale;
    }

    private static IEnumerable<int> GetQualitySteps(SKEncodedImageFormat format) =>
        format is SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp ? JpegQualitySteps : [100];

    private static SKBitmap? Resize(SKBitmap source, int width, int height)
    {
        if (source.Width == width && source.Height == height)
        {
            return source.Copy();
        }

        var target = new SKBitmap(new SKImageInfo(
            width,
            height,
            source.ColorType,
            source.AlphaType,
            source.ColorSpace));
        if (source.ScalePixels(
                target,
                new SKSamplingOptions(SKCubicResampler.Mitchell)))
        {
            return target;
        }

        target.Dispose();
        return null;
    }

    private static byte[]? Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        return data?.ToArray();
    }

    private static void ReplaceFile(string path, byte[] content)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.compressing";
        try
        {
            File.WriteAllBytes(tempPath, content);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}

/// <summary>
/// 描述一次导入图片压缩尝试的结果。
/// </summary>
public sealed class FrontedImageCompressionResult
{
    private FrontedImageCompressionResult()
    {
    }

    /// <summary>
    /// 指示图片是否已被重新编码并替换。
    /// </summary>
    public bool WasCompressed { get; private init; }

    /// <summary>
    /// 压缩前的文件字节数。
    /// </summary>
    public long OriginalBytes { get; private init; }

    /// <summary>
    /// 压缩后的文件字节数；未压缩时与 <see cref="OriginalBytes"/> 相同。
    /// </summary>
    public long CompressedBytes { get; private init; }

    /// <summary>
    /// 压缩前的像素宽度。
    /// </summary>
    public int OriginalWidth { get; private init; }

    /// <summary>
    /// 压缩前的像素高度。
    /// </summary>
    public int OriginalHeight { get; private init; }

    /// <summary>
    /// 压缩后的像素宽度。
    /// </summary>
    public int CompressedWidth { get; private init; }

    /// <summary>
    /// 压缩后的像素高度。
    /// </summary>
    public int CompressedHeight { get; private init; }

    /// <summary>
    /// 压缩失败时的诊断代码；未尝试或成功时为空。
    /// </summary>
    public string? ErrorCode { get; private init; }

    internal static FrontedImageCompressionResult NotCompressed(
        long bytes,
        int width = 0,
        int height = 0) => new()
        {
            OriginalBytes = bytes,
            CompressedBytes = bytes,
            OriginalWidth = width,
            OriginalHeight = height,
            CompressedWidth = width,
            CompressedHeight = height
        };

    internal static FrontedImageCompressionResult Compressed(
        long originalBytes,
        long compressedBytes,
        int originalWidth,
        int originalHeight,
        int compressedWidth,
        int compressedHeight) => new()
        {
            WasCompressed = true,
            OriginalBytes = originalBytes,
            CompressedBytes = compressedBytes,
            OriginalWidth = originalWidth,
            OriginalHeight = originalHeight,
            CompressedWidth = compressedWidth,
            CompressedHeight = compressedHeight
        };

    internal static FrontedImageCompressionResult Failed(string errorCode, long originalBytes = 0) => new()
    {
        OriginalBytes = originalBytes,
        CompressedBytes = originalBytes,
        ErrorCode = errorCode
    };
}
