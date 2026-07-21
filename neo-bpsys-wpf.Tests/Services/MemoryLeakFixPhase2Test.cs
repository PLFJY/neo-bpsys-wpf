#nullable enable

using System;
using System.IO;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tests.Infrastructure;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 阶段 2 内存治理验证测试。
/// 验证 FrontedResourceResolver 和 BackgroundImageTintProcessor 的缓存
/// 从 FIFO（Queue）升级为真 LRU（LinkedList + Dictionary），
/// 并加入字节预算实现双限制确定性驱逐。
/// </summary>
public sealed class MemoryLeakFixPhase2Test
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. FrontedResourceResolver 源码契约
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 FrontedResourceResolver 的缓存顺序容器为 LinkedList（真 LRU），
    /// 而非旧的 Queue（FIFO）。
    /// </summary>
    [Fact]
    public void ResourceResolver_CacheOrder_IsLinkedListNotQueue()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf.Core",
            "Services",
            "FrontedLayout",
            "FrontedResourceResolver.cs");

        Assert.Contains("private readonly LinkedList<ImageCacheKey> _imageCacheOrder", source);
        Assert.Contains("private readonly Dictionary<ImageCacheKey, LinkedListNode<ImageCacheKey>> _imageCacheNodes", source);
        Assert.DoesNotContain("Queue<ImageCacheKey> _imageCacheOrder", source);
    }

    /// <summary>
    /// 验证 FrontedResourceResolver 声明了字节预算常量 MaxCachedBytes。
    /// </summary>
    [Fact]
    public void ResourceResolver_HasMaxCachedBytesConstant()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf.Core",
            "Services",
            "FrontedLayout",
            "FrontedResourceResolver.cs");

        Assert.Contains("MaxCachedBytes", source);
        Assert.Contains("const long MaxCachedBytes", source);
    }

    /// <summary>
    /// 验证 FrontedResourceResolver 命中时执行 LRU move-to-end
    /// （Remove + AddLast），保证频繁使用的图片不会被中间动画帧驱逐。
    /// </summary>
    [Fact]
    public void ResourceResolver_HitPath_PerformsLruMoveToEnd()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf.Core",
            "Services",
            "FrontedLayout",
            "FrontedResourceResolver.cs");

        Assert.Contains("_imageCacheOrder.Remove(node)", source);
        Assert.Contains("_imageCacheOrder.AddLast(node)", source);
    }

    /// <summary>
    /// 验证 FrontedResourceResolver 的驱逐条件同时检查条目数和字节预算
    /// （使用 || 连接），先触发的先驱逐。
    /// </summary>
    [Fact]
    public void ResourceResolver_Eviction_ChecksCountAndBytes()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf.Core",
            "Services",
            "FrontedLayout",
            "FrontedResourceResolver.cs");

        Assert.Contains("_imageCache.Count > MaxCachedImages || _currentCachedBytes > MaxCachedBytes", source);
    }

    /// <summary>
    /// 验证 FrontedResourceResolver 的 ClearCache 重置字节计数器。
    /// </summary>
    [Fact]
    public void ResourceResolver_ClearCache_ResetsByteCounter()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf.Core",
            "Services",
            "FrontedLayout",
            "FrontedResourceResolver.cs");

        Assert.Contains("_currentCachedBytes = 0", source);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. BackgroundImageTintProcessor 源码契约
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 BackgroundImageTintProcessor 的缓存顺序容器为 LinkedList（真 LRU），
    /// 而非旧的 Queue（FIFO）。
    /// </summary>
    [Fact]
    public void TintProcessor_CacheOrder_IsLinkedListNotQueue()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf.Core",
            "Services",
            "FrontedLayout",
            "BackgroundImageTintProcessor.cs");

        Assert.Contains("private readonly LinkedList<TintCacheKey> _cacheOrder", source);
        Assert.Contains("private readonly Dictionary<TintCacheKey, LinkedListNode<TintCacheKey>> _cacheNodes", source);
        Assert.DoesNotContain("Queue<TintCacheKey> _cacheOrder", source);
    }

    /// <summary>
    /// 验证 BackgroundImageTintProcessor 声明了字节预算常量 MaxCacheBytes。
    /// </summary>
    [Fact]
    public void TintProcessor_HasMaxCacheBytesConstant()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf.Core",
            "Services",
            "FrontedLayout",
            "BackgroundImageTintProcessor.cs");

        Assert.Contains("MaxCacheBytes", source);
        Assert.Contains("const long MaxCacheBytes", source);
    }

    /// <summary>
    /// 验证 BackgroundImageTintProcessor 命中时执行 LRU move-to-end。
    /// </summary>
    [Fact]
    public void TintProcessor_HitPath_PerformsLruMoveToEnd()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf.Core",
            "Services",
            "FrontedLayout",
            "BackgroundImageTintProcessor.cs");

        Assert.Contains("_cacheOrder.Remove(node)", source);
        Assert.Contains("_cacheOrder.AddLast(node)", source);
    }

    /// <summary>
    /// 验证 BackgroundImageTintProcessor 的驱逐条件同时检查条目数和字节预算。
    /// </summary>
    [Fact]
    public void TintProcessor_Eviction_ChecksCountAndBytes()
    {
        var source = ReadRepoFile(
            "neo-bpsys-wpf.Core",
            "Services",
            "FrontedLayout",
            "BackgroundImageTintProcessor.cs");

        Assert.Contains("_cache.Count > MaxCacheEntries || _currentCacheBytes > MaxCacheBytes", source);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. FrontedResourceResolver 行为测试
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 FrontedResourceResolver 命中缓存时返回同一个已冻结实例（引用相等）。
    /// </summary>
    [Fact]
    public void ResourceResolver_CacheHit_ReturnsSameInstance()
    {
        WpfTestThread.Run(() =>
        {
            var resolver = new FrontedResourceResolver(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FrontedResourceResolver>.Instance);

            var tempFile = Path.Combine(Path.GetTempPath(), $"neo-bpsys-phase2-hit-{Guid.NewGuid():N}.png");
            try
            {
                WritePng(tempFile, 16, 16);
                var first = resolver.ResolveImage(tempFile, FrontedImagePurpose.PackageResource);
                var second = resolver.ResolveImage(tempFile, FrontedImagePurpose.PackageResource);

                Assert.NotNull(first);
                Assert.NotNull(second);
                Assert.Same(first, second);
                Assert.Equal(1, resolver.CachedEntryCount);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        });
    }

    /// <summary>
    /// 验证 FrontedResourceResolver 缓存多张图片后 EstimatedCachedBytes 正确累加，
    /// ClearCache 后条目数和字节数均归零。
    /// </summary>
    [Fact]
    public void ResourceResolver_MultipleImages_TrackedCorrectlyAndClearResets()
    {
        WpfTestThread.Run(() =>
        {
            var resolver = new FrontedResourceResolver(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FrontedResourceResolver>.Instance);

            var tempFile1 = Path.Combine(Path.GetTempPath(), $"neo-bpsys-phase2-a-{Guid.NewGuid():N}.png");
            var tempFile2 = Path.Combine(Path.GetTempPath(), $"neo-bpsys-phase2-b-{Guid.NewGuid():N}.png");
            try
            {
                WritePng(tempFile1, 32, 24);
                WritePng(tempFile2, 16, 16);

                resolver.ResolveImage(tempFile1, FrontedImagePurpose.PackageResource);
                resolver.ResolveImage(tempFile2, FrontedImagePurpose.PackageResource);

                Assert.Equal(2, resolver.CachedEntryCount);
                // 32*24*4 + 16*16*4 = 3072 + 1024 = 4096
                Assert.Equal(4096, resolver.EstimatedCachedBytes);

                resolver.ClearCache();
                Assert.Equal(0, resolver.CachedEntryCount);
                Assert.Equal(0, resolver.EstimatedCachedBytes);
            }
            finally
            {
                try { File.Delete(tempFile1); } catch { }
                try { File.Delete(tempFile2); } catch { }
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. BackgroundImageTintProcessor 行为测试
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 BackgroundImageTintProcessor 命中缓存时返回同一个已冻结实例。
    /// </summary>
    [Fact]
    public void TintProcessor_CacheHit_ReturnsSameInstance()
    {
        WpfTestThread.Run(() =>
        {
            var processor = new BackgroundImageTintProcessor();
            var source = CreateFrozenBitmap(16, 16);

            var first = processor.CreateTinted(
                source, "lru-hit-test", Color.FromRgb(255, 0, 0),
                BackgroundTintMode.Multiply, strength: 0.5D);

            var second = processor.CreateTinted(
                source, "lru-hit-test", Color.FromRgb(255, 0, 0),
                BackgroundTintMode.Multiply, strength: 0.5D);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Same(first, second);
            Assert.Equal(1, processor.CachedEntryCount);
        });
    }

    /// <summary>
    /// 验证 BackgroundImageTintProcessor 在条目数超过 MaxCacheEntries (32) 时
    /// 执行确定性驱逐，缓存条目数始终不超过 32。
    /// </summary>
    [Fact]
    public void TintProcessor_CountEviction_KeepsEntryCountAtLimit()
    {
        WpfTestThread.Run(() =>
        {
            var processor = new BackgroundImageTintProcessor();
            var source = CreateFrozenBitmap(8, 8);

            // 插入 40 个不同染色（不同 tint 颜色），触发 count-based 驱逐。
            for (byte i = 0; i < 40; i++)
            {
                processor.CreateTinted(
                    source, $"count-eviction-{i}", Color.FromRgb(i, 0, 0),
                    BackgroundTintMode.Multiply, strength: 1D);
            }

            // MaxCacheEntries = 32，所以缓存条目数不应超过 32。
            Assert.Equal(32, processor.CachedEntryCount);
        });
    }

    /// <summary>
    /// 验证 BackgroundImageTintProcessor 的 LRU 语义：
    /// 命中条目后移动到最近使用端，新条目驱逐最久未使用的条目（而非被命中的条目）。
    /// </summary>
    [Fact]
    public void TintProcessor_LruEviction_HitEntrySurvivesEviction()
    {
        WpfTestThread.Run(() =>
        {
            var processor = new BackgroundImageTintProcessor();
            var source = CreateFrozenBitmap(8, 8);

            // 步骤 1：用 32 个不同颜色填满缓存（达到 MaxCacheEntries 上限）。
            // entry[0] 是最老的（链表头部），entry[31] 是最新的（链表尾部）。
            var entries = new BitmapSource?[32];
            for (byte i = 0; i < 32; i++)
            {
                entries[i] = processor.CreateTinted(
                    source, $"lru-{i}", Color.FromRgb(i, 100, 100),
                    BackgroundTintMode.Multiply, strength: 0.5D);
                Assert.NotNull(entries[i]);
            }

            Assert.Equal(32, processor.CachedEntryCount);

            // 步骤 2：命中 entry[0]（最老的），LRU 将其移动到链表尾部（最近使用）。
            var hitResult = processor.CreateTinted(
                source, "lru-0", Color.FromRgb(0, 100, 100),
                BackgroundTintMode.Multiply, strength: 0.5D);

            // 命中应返回同一个实例。
            Assert.Same(entries[0], hitResult);
            Assert.Equal(32, processor.CachedEntryCount);

            // 步骤 3：插入第 33 个条目，触发驱逐。
            // LRU 应驱逐 entry[1]（当前最久未使用），而非 entry[0]（刚刚被命中）。
            processor.CreateTinted(
                source, "lru-new", Color.FromRgb(200, 200, 200),
                BackgroundTintMode.Multiply, strength: 0.5D);

            Assert.Equal(32, processor.CachedEntryCount);

            // 步骤 4：再次请求 entry[0] —— 应命中缓存（同一个实例）。
            var reHit0 = processor.CreateTinted(
                source, "lru-0", Color.FromRgb(0, 100, 100),
                BackgroundTintMode.Multiply, strength: 0.5D);
            Assert.Same(entries[0], reHit0);

            // 步骤 5：请求 entry[1] —— 应未命中缓存（被驱逐），返回新实例。
            var miss1 = processor.CreateTinted(
                source, "lru-1", Color.FromRgb(1, 100, 100),
                BackgroundTintMode.Multiply, strength: 0.5D);
            Assert.NotSame(entries[1], miss1);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string ReadRepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var path = Path.Combine([directory.FullName, .. parts]);
        Assert.True(File.Exists(path), $"File not found: {path}");
        return File.ReadAllText(path);
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
}
