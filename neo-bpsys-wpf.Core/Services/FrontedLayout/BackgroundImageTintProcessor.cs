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
    {
        var bitmap = ConvertToBitmapSource(source, logger);
        if (bitmap is null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return null;
        }

        strength = double.IsFinite(strength) ? Math.Clamp(strength, 0D, 1D) : 1D;
        textureStrength = double.IsFinite(textureStrength) ? Math.Clamp(textureStrength, 0D, 1D) : 0.45D;
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
        var meanLuminance = mode == BackgroundTintMode.BaseColorWithTexture
            ? CalculateMeanLuminance(pixels)
            : 0D;
        var tintHsl = mode == BackgroundTintMode.BaseColorWithTexture
            ? ToHsl(tint.R, tint.G, tint.B)
            : default;

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
                var detail = (luminance - meanLuminance) / 255D;
                var texturedLightness = Math.Clamp(tintHsl.Lightness + detail * textureStrength, 0D, 1D);
                (targetR, targetG, targetB) = FromHsl(tintHsl.Hue, tintHsl.Saturation, texturedLightness);
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

    private static double CalculateMeanLuminance(byte[] pixels)
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

    private static double CalculateLuminance(byte red, byte green, byte blue) =>
        red * 0.2126D + green * 0.7152D + blue * 0.0722D;

    private static HslColor ToHsl(byte red, byte green, byte blue)
    {
        var r = red / 255D;
        var g = green / 255D;
        var b = blue / 255D;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2D;
        var delta = max - min;
        if (delta <= double.Epsilon)
        {
            return new HslColor(0D, 0D, lightness);
        }

        var saturation = delta / (1D - Math.Abs(2D * lightness - 1D));
        var hue = max == r
            ? 60D * (((g - b) / delta) % 6D)
            : max == g
                ? 60D * ((b - r) / delta + 2D)
                : 60D * ((r - g) / delta + 4D);
        return new HslColor(hue < 0D ? hue + 360D : hue, saturation, lightness);
    }

    private static (byte Red, byte Green, byte Blue) FromHsl(double hue, double saturation, double lightness)
    {
        var chroma = (1D - Math.Abs(2D * lightness - 1D)) * saturation;
        var hueSection = hue / 60D;
        var x = chroma * (1D - Math.Abs(hueSection % 2D - 1D));
        var (r1, g1, b1) = hueSection switch
        {
            < 1D => (chroma, x, 0D),
            < 2D => (x, chroma, 0D),
            < 3D => (0D, chroma, x),
            < 4D => (0D, x, chroma),
            < 5D => (x, 0D, chroma),
            _ => (chroma, 0D, x)
        };
        var match = lightness - chroma / 2D;
        return (ToByte(r1 + match), ToByte(g1 + match), ToByte(b1 + match));
    }

    private static byte ToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value * 255D), 0D, 255D);

    private readonly record struct HslColor(double Hue, double Saturation, double Lightness);

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
        int SourceIdentity);
}
