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
    private readonly object _cacheLock = new();
    private readonly Dictionary<TintCacheKey, BitmapSource> _cache = [];
    private readonly Queue<TintCacheKey> _cacheOrder = [];

    public BitmapSource? CreateTinted(
        ImageSource source,
        string? sourceKey,
        Color tint,
        BackgroundTintMode mode,
        double strength,
        ILogger? logger = null)
    {
        var bitmap = ConvertToBitmapSource(source, logger);
        if (bitmap is null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return null;
        }

        strength = double.IsFinite(strength) ? Math.Clamp(strength, 0D, 1D) : 1D;
        var key = new TintCacheKey(
            sourceKey ?? string.Empty,
            tint.R,
            tint.G,
            tint.B,
            mode,
            strength,
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            RuntimeHelpers.GetHashCode(bitmap));
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var converted = bitmap.Format == PixelFormats.Bgra32
            ? bitmap
            : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

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
            else
            {
                var luminance = (byte)Math.Clamp(
                    Math.Round(sourceR * 0.2126D + sourceG * 0.7152D + sourceB * 0.0722D),
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
            if (_cache.ContainsKey(key))
            {
                return;
            }

            _cache[key] = value;
            _cacheOrder.Enqueue(key);
            while (_cache.Count > MaxCacheEntries && _cacheOrder.TryDequeue(out var oldest))
            {
                _cache.Remove(oldest);
            }
        }
    }

    private static byte Multiply(byte left, byte right) =>
        (byte)Math.Clamp(Math.Round(left * right / 255D), 0D, 255D);

    private static byte Interpolate(byte source, byte target, double strength) =>
        (byte)Math.Clamp(Math.Round(source + (target - source) * strength), 0D, 255D);

    private readonly record struct TintCacheKey(
        string SourceKey,
        byte R,
        byte G,
        byte B,
        BackgroundTintMode Mode,
        double Strength,
        int PixelWidth,
        int PixelHeight,
        int SourceIdentity);
}
