using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Verifies observable cache behavior for fronted images and generated tint assets.
/// </summary>
public sealed class FrontedResourceCacheTest
{
    /// <summary>
    /// Verifies resolver cache hits reuse an image and clearing the cache releases tracked state.
    /// </summary>
    [Fact]
    public void ResolverCacheHitAndClearAreObservable()
    {
        WpfTestThread.Run(() =>
        {
            var resolver = new FrontedResourceResolver(NullLogger<FrontedResourceResolver>.Instance);
            var firstPath = TempPng(32, 24);
            var secondPath = TempPng(16, 16);
            try
            {
                var first = resolver.ResolveImage(firstPath, FrontedImagePurpose.PackageResource);
                Assert.Same(first, resolver.ResolveImage(firstPath, FrontedImagePurpose.PackageResource));
                resolver.ResolveImage(secondPath, FrontedImagePurpose.PackageResource);
                Assert.Equal(2, resolver.CachedEntryCount);
                Assert.Equal(4096, resolver.EstimatedCachedBytes);

                resolver.ClearCache();
                Assert.Equal(0, resolver.CachedEntryCount);
                Assert.Equal(0, resolver.EstimatedCachedBytes);
            }
            finally
            {
                File.Delete(firstPath);
                File.Delete(secondPath);
            }
        });
    }

    /// <summary>
    /// Verifies runtime image purposes retain the original pixel dimensions.
    /// </summary>
    /// <param name="purpose">The image usage.</param>
    [Theory]
    [InlineData(FrontedImagePurpose.Background)]
    [InlineData(FrontedImagePurpose.PackageResource)]
    [InlineData(FrontedImagePurpose.UiElement)]
    public void RuntimeImagesPreservePixelDimensions(FrontedImagePurpose purpose)
    {
        WpfTestThread.Run(() =>
        {
            var resolver = new FrontedResourceResolver(NullLogger<FrontedResourceResolver>.Instance);
            var path = TempPng(160, 90);
            try
            {
                var bitmap = Assert.IsAssignableFrom<BitmapSource>(resolver.ResolveImage(path, purpose));
                Assert.Equal(160, bitmap.PixelWidth);
                Assert.Equal(90, bitmap.PixelHeight);
            }
            finally
            {
                File.Delete(path);
            }
        });
    }

    /// <summary>
    /// Verifies equivalent tint requests reuse the generated bitmap.
    /// </summary>
    [Fact]
    public void TintCacheHitReturnsSameBitmap()
    {
        WpfTestThread.Run(() =>
        {
            var processor = new BackgroundImageTintProcessor();
            var source = FrozenBitmap(16, 16);
            var first = processor.CreateTinted(source, "same", Colors.Red, BackgroundTintMode.Multiply, 0.5D);
            var second = processor.CreateTinted(source, "same", Colors.Red, BackgroundTintMode.Multiply, 0.5D);

            Assert.Same(first, second);
            Assert.Equal(1, processor.CachedEntryCount);
        });
    }

    /// <summary>
    /// Verifies a recently used tint survives capacity eviction while the oldest untouched tint does not.
    /// </summary>
    [Fact]
    public void TintCacheEvictsLeastRecentlyUsedBitmap()
    {
        WpfTestThread.Run(() =>
        {
            var processor = new BackgroundImageTintProcessor();
            var source = FrozenBitmap(8, 8);
            var entries = new BitmapSource?[32];
            for (byte index = 0; index < entries.Length; index++)
            {
                entries[index] = processor.CreateTinted(
                    source,
                    $"lru-{index}",
                    Color.FromRgb(index, 100, 100),
                    BackgroundTintMode.Multiply,
                    0.5D);
            }

            Assert.Same(entries[0], processor.CreateTinted(source, "lru-0", Color.FromRgb(0, 100, 100), BackgroundTintMode.Multiply, 0.5D));
            processor.CreateTinted(source, "new", Colors.White, BackgroundTintMode.Multiply, 0.5D);

            Assert.Equal(32, processor.CachedEntryCount);
            Assert.Same(entries[0], processor.CreateTinted(source, "lru-0", Color.FromRgb(0, 100, 100), BackgroundTintMode.Multiply, 0.5D));
            Assert.NotSame(entries[1], processor.CreateTinted(source, "lru-1", Color.FromRgb(1, 100, 100), BackgroundTintMode.Multiply, 0.5D));
        });
    }

    private static BitmapSource FrozenBitmap(int width, int height)
    {
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[width * height * 4],
            width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static string TempPng(int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), $"neo-bpsys-resource-{Guid.NewGuid():N}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(FrozenBitmap(width, height)));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }
}
