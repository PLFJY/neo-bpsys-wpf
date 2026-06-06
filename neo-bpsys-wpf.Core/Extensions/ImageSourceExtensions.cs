using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Core.Extensions;

/// <summary>
/// 提供 <see cref="ImageSource"/> 的扩展方法
/// </summary>
public static class ImageSourceExtensions
{
    /// <summary>
    /// 将 <see cref="ImageSource"/> 转换为灰度图像，同时保留 Alpha 通道
    /// </summary>
    /// <param name="source">源图像</param>
    /// <returns>转换后的灰度图像</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> 为 null</exception>
    public static ImageSource ToGrayKeepAlpha(this ImageSource source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        BitmapSource bitmapSource = source as BitmapSource
                                    ?? RenderToBitmapSource(source);

        return bitmapSource.ToGrayKeepAlphaBitmapSource();
    }

    /// <summary>
    /// 将 <see cref="BitmapSource"/> 转换为灰度图像，同时保留 Alpha 通道
    /// </summary>
    /// <param name="source">源图像</param>
    /// <returns>转换后的灰度图像</returns>
    private static BitmapSource ToGrayKeepAlphaBitmapSource(this BitmapSource source)
    {
        BitmapSource bgraSource = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int width = bgraSource.PixelWidth;
        int height = bgraSource.PixelHeight;
        int stride = width * 4;

        byte[] pixels = new byte[stride * height];
        bgraSource.CopyPixels(pixels, stride, 0);

        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte b = pixels[i];
            byte g = pixels[i + 1];
            byte r = pixels[i + 2];

            byte gray = (byte)((r * 299 + g * 587 + b * 114) / 1000);

            pixels[i] = gray;       // B
            pixels[i + 1] = gray;   // G
            pixels[i + 2] = gray;   // R
            // pixels[i + 3] Alpha 保留
        }

        BitmapSource result = BitmapSource.Create(
            width,
            height,
            bgraSource.DpiX,
            bgraSource.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);

        result.Freeze();
        return result;
    }

    /// <summary>
    /// 将 <see cref="ImageSource"/> 渲染为 <see cref="BitmapSource"/>
    /// </summary>
    private static BitmapSource RenderToBitmapSource(ImageSource source)
    {
        double width = source.Width;
        double height = source.Height;

        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("ImageSource 没有有效的宽高，无法渲染为 BitmapSource。");

        int pixelWidth = Math.Max(1, (int)Math.Ceiling(width));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(height));

        var visual = new DrawingVisual();

        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawImage(source, new Rect(0, 0, width, height));
        }

        var rtb = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            96,
            96,
            PixelFormats.Pbgra32);

        rtb.Render(visual);
        rtb.Freeze();

        return rtb;
    }

    /// <summary>
    /// 将 <see cref="ImageSource"/> 叠加到 <see cref="ImageSource"/> 上，并返回叠加后的结果
    /// </summary>
    /// <param name="baseSource">底图</param>
    /// <param name="overlaySource">覆盖图</param>
    /// <param name="opacity">透明度，默认为 1.0</param>
    /// <returns>叠加后的结果</returns>
    public static ImageSource Overlay(
        this ImageSource baseSource,
        ImageSource overlaySource,
        double opacity = 1.0)
    {
        if (baseSource == null)
            throw new ArgumentNullException(nameof(baseSource));

        if (overlaySource == null)
            throw new ArgumentNullException(nameof(overlaySource));

        var size = GetImageSourceSize(baseSource);

        if (size.Width <= 0 || size.Height <= 0)
            throw new InvalidOperationException("baseSource 没有有效宽高，无法叠加。");

        double dpiX = 96;
        double dpiY = 96;

        if (baseSource is BitmapSource bitmapSource)
        {
            dpiX = bitmapSource.DpiX;
            dpiY = bitmapSource.DpiY;
        }

        int pixelWidth = Math.Max(1, (int)Math.Ceiling(size.Width * dpiX / 96.0));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(size.Height * dpiY / 96.0));

        var visual = new DrawingVisual();

        using (DrawingContext dc = visual.RenderOpen())
        {
            var rect = new Rect(0, 0, size.Width, size.Height);

            // 底图
            dc.DrawImage(baseSource, rect);

            // 覆盖图，透明区域会自然保留
            dc.PushOpacity(opacity);
            dc.DrawImage(overlaySource, rect);
            dc.Pop();
        }

        var result = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            dpiX,
            dpiY,
            PixelFormats.Pbgra32);

        result.Render(visual);
        result.Freeze();

        return result;
    }

    /// <summary>
    /// 获取 <see cref="ImageSource"/> 的宽高
    /// </summary>
    /// <param name="source">源图像</param>
    /// <returns>宽高</returns>
    private static Size GetImageSourceSize(ImageSource source)
    {
        double width = source.Width;
        double height = source.Height;

        if (double.IsNaN(width) || double.IsInfinity(width))
            width = 0;

        if (double.IsNaN(height) || double.IsInfinity(height))
            height = 0;

        return new Size(width, height);
    }
}