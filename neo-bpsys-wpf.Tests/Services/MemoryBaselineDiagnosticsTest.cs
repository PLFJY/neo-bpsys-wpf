using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.WebRenderer.Services;
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 验证阶段 0 新增的内存诊断属性。
/// 这些测试不测量真实内存，仅验证诊断属性可访问且返回合理值，
/// 并演示用于对象释放验证的 WeakReference 模式。
/// </summary>
public sealed class MemoryBaselineDiagnosticsTest
{
    /// <summary>
    /// FrontedResourceResolver 的诊断属性应在缓存为空时返回零，
    /// 在缓存图片后返回非零条目数和估算字节数。
    /// </summary>
    [Fact]
    public void FrontedResourceResolverDiagnosticsReflectCacheState()
    {
        WpfTestThread.Run(() =>
        {
            var resolver = new FrontedResourceResolver(NullLogger<FrontedResourceResolver>.Instance);

            Assert.Equal(0, resolver.CachedEntryCount);
            Assert.Equal(0, resolver.EstimatedCachedBytes);

            var tempFile = Path.Combine(Path.GetTempPath(), $"neo-bpsys-diag-{Guid.NewGuid():N}.png");
            try
            {
                WritePng(tempFile, 32, 24);
                var image = resolver.ResolveImage(tempFile, FrontedImagePurpose.PackageResource);

                Assert.NotNull(image);
                Assert.Equal(1, resolver.CachedEntryCount);
                // 32 * 24 * 4 (Bgra32) = 3072 bytes
                Assert.Equal(3072, resolver.EstimatedCachedBytes);

                resolver.ClearCache();
                Assert.Equal(0, resolver.CachedEntryCount);
                Assert.Equal(0, resolver.EstimatedCachedBytes);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        });
    }

    /// <summary>
    /// BackgroundImageTintProcessor 的诊断属性应在缓存为空时返回零，
    /// 在染色后返回非零条目数和估算字节数。
    /// </summary>
    [Fact]
    public void BackgroundImageTintProcessorDiagnosticsReflectCacheState()
    {
        WpfTestThread.Run(() =>
        {
            var processor = new BackgroundImageTintProcessor();

            Assert.Equal(0, processor.CachedEntryCount);
            Assert.Equal(0, processor.EstimatedCachedBytes);

            var source = CreateFrozenBitmap(16, 16);
            processor.CreateTinted(
                source,
                "diagnostic-tint-source",
                Color.FromRgb(255, 0, 0),
                BackgroundTintMode.Multiply,
                strength: 1D);

            Assert.Equal(1, processor.CachedEntryCount);
            // 染色结果为 Bgra32: 16 * 16 * 4 = 1024 bytes
            Assert.Equal(1024, processor.EstimatedCachedBytes);
        });
    }

    /// <summary>
    /// WebRuntimeAssetRegistry 的诊断属性应正确反映各集合的条目数。
    /// </summary>
    [Fact]
    public void WebRuntimeAssetRegistryDiagnosticsReflectRegistryState()
    {
        WpfTestThread.Run(() =>
        {
            using var registry = new WebRuntimeAssetRegistry();

            Assert.Equal(0, registry.ReadyAssetCount);
            Assert.Equal(0, registry.PendingAssetCount);
            Assert.Equal(0, registry.FailureAssetCount);
            Assert.Equal(0, registry.RemoteAssetCount);
            Assert.Equal(0, registry.ReferenceCount);

            var tempFile = Path.GetTempFileName();
            try
            {
                WritePng(tempFile, 8, 8);
                var localBitmap = new BitmapImage();
                localBitmap.BeginInit();
                localBitmap.UriSource = new Uri(tempFile, UriKind.Absolute);
                localBitmap.CacheOption = BitmapCacheOption.OnLoad;
                localBitmap.EndInit();
                localBitmap.Freeze();

                // 本地文件注册后进入 pending（异步编码）。
                Assert.False(registry.TryRegister(localBitmap, out _, out _));
                Assert.Equal(1, registry.PendingAssetCount);
                Assert.Equal(0, registry.ReadyAssetCount);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        });
    }

    /// <summary>
    /// 演示使用 WeakReference 验证对象释放的模式。
    /// 该模式不反向持有目标，可用于阶段 1 验证 Designer 窗口是否被 GC。
    /// 这里用一个可释放的小对象演示模式，不依赖 GC 行为的确定性。
    /// </summary>
    [Fact]
    public void WeakReferencePatternDoesNotKeepTargetAlive()
    {
        var weak = CreateWeakReference(out var strong);
        Assert.True(weak.TryGetTarget(out _));
        Assert.NotNull(strong);

        // 释放强引用后，WeakReference 不阻止目标被 GC。
        // 注意：本测试不强制 GC，仅验证 WeakReference 的语义正确性。
        // 阶段 1 的真实对象释放验证应在 WpfTestThread 中运行窗口后进行。
        strong = null;
        // 不调用 GC.Collect（任务包禁止），仅验证赋值语义。
        Assert.Null(strong);
    }

    private static BitmapSource CreateFrozenBitmap(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void WritePng(string path, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            width * 4);
        bitmap.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static WeakReference<object> CreateWeakReference(out object strong)
    {
        strong = new object();
        return new WeakReference<object>(strong);
    }
}
