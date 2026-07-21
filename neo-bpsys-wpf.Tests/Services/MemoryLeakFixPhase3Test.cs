#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.WebRenderer.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 阶段 3 内存治理验证测试。
/// 验证 WebRuntimeAssetRegistry 的异步编码竞态修复和全类型 active snapshot 清理。
/// </summary>
public sealed class MemoryLeakFixPhase3Test
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. CompleteEncoding 源码契约
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 CompleteEncoding 在写入 _ready/_failures 前检查 source 是否仍在 _pending 中。
    /// 这是"异步任务回写前验证生命周期"约束的源码契约。
    /// </summary>
    [Fact]
    public void CompleteEncoding_ValidatesPendingBeforeWriteback()
    {
        var source = ReadRepoFile(
            "Built-inPlugins",
            "neo-bpsys-wpf.WebRenderer",
            "Services",
            "WebRuntimeValue.cs");

        // 必须存在 _pending.Remove(source) 的返回值检查。
        Assert.Contains("if (!_pending.Remove(source))", source);
        // 检查失败时必须 return（丢弃过期编码结果）。
        Assert.Contains("return;", source);
    }

    /// <summary>
    /// 验证 CompleteEncoding 不再无条件调用 _pending.Remove(source) 后写入。
    /// 旧代码是 _pending.Remove(source); 然后直接写入，没有检查返回值。
    /// </summary>
    [Fact]
    public void CompleteEncoding_DoesNotUnconditionallyRemovePending()
    {
        var source = ReadRepoFile(
            "Built-inPlugins",
            "neo-bpsys-wpf.WebRenderer",
            "Services",
            "WebRuntimeValue.cs");

        // 提取 CompleteEncoding 方法体，验证没有无条件 Remove 后直接写入的模式。
        var startIdx = source.IndexOf("private void CompleteEncoding", StringComparison.Ordinal);
        Assert.True(startIdx >= 0, "CompleteEncoding method not found");
        var endIdx = source.IndexOf("private void ScheduleRetry", StringComparison.Ordinal);
        Assert.True(endIdx > startIdx, "ScheduleRetry method not found after CompleteEncoding");
        var methodBody = source.Substring(startIdx, endIdx - startIdx);

        // 新代码必须在 if 条件中调用 _pending.Remove(source)（验证返回值），
        // 而非无条件调用后直接写入。
        Assert.Contains("if (!_pending.Remove(source))", methodBody);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. ReplaceActiveSources 源码契约
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 ReplaceActiveSources 方法存在且清理所有四种集合。
    /// </summary>
    [Fact]
    public void ReplaceActiveSources_ExistsAndClearsAllCollections()
    {
        var source = ReadRepoFile(
            "Built-inPlugins",
            "neo-bpsys-wpf.WebRenderer",
            "Services",
            "WebRuntimeValue.cs");

        Assert.Contains("public void ReplaceActiveSources(IEnumerable<ImageSource> sources)", source);

        // 提取 ReplaceActiveSources 方法体。
        var startIdx = source.IndexOf("public void ReplaceActiveSources", StringComparison.Ordinal);
        Assert.True(startIdx >= 0);
        var endIdx = source.IndexOf("internal int ReadyAssetCount", StringComparison.Ordinal);
        Assert.True(endIdx > startIdx);
        var methodBody = source.Substring(startIdx, endIdx - startIdx);

        // 必须清理所有四种集合。
        Assert.Contains("_remote.Remove(source)", methodBody);
        Assert.Contains("_ready.Remove(source)", methodBody);
        Assert.Contains("_pending.Remove(source)", methodBody);
        Assert.Contains("_failures.Remove(source)", methodBody);

        // 必须从所有集合收集 source（不只是 _remote.Keys）。
        Assert.Contains("_ready.Keys", methodBody);
        Assert.Contains("_pending", methodBody);
        Assert.Contains("_failures.Keys", methodBody);
        Assert.Contains("_remote.Keys", methodBody);
    }

    /// <summary>
    /// 验证 ReplaceRemoteSources 仍然保留（向后兼容），且只清理 _remote 中的 source。
    /// </summary>
    [Fact]
    public void ReplaceRemoteSources_StillExistsForBackwardCompat()
    {
        var source = ReadRepoFile(
            "Built-inPlugins",
            "neo-bpsys-wpf.WebRenderer",
            "Services",
            "WebRuntimeValue.cs");

        Assert.Contains("public void ReplaceRemoteSources(IEnumerable<ImageSource> sources)", source);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. publisher 源码契约
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 publisher 调用 ReplaceActiveSources 而非旧的 ReplaceRemoteSources。
    /// </summary>
    [Fact]
    public void Publisher_UsesReplaceActiveSources()
    {
        var source = ReadRepoFile(
            "Built-inPlugins",
            "neo-bpsys-wpf.WebRenderer",
            "Services",
            "WebRendererRuntimeStatePublisher.cs");

        Assert.Contains("_assets.ReplaceActiveSources(activeImages)", source);
        // 不应再调用 ReplaceRemoteSources（已被 ReplaceActiveSources 替代）。
        Assert.DoesNotContain("_assets.ReplaceRemoteSources(activeImages)", source);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. ReplaceActiveSources 行为测试
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 验证 ReplaceActiveSources 清理已就绪的冻结位图资源。
    /// 布局切换后旧的冻结位图应从 _ready 中移除，不再被强引用。
    /// </summary>
    [Fact]
    public async Task ReplaceActiveSources_ClearsReadyFrozenBitmap()
    {
        BitmapSource? frozenBitmap = null;
        WpfTestThread.Run(() =>
        {
            frozenBitmap = CreateFrozenBitmap(16, 16);
        });

        using var registry = new WebRuntimeAssetRegistry();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registry.AssetStateChanged += (_, _) => changed.TrySetResult();

        // 注册冻结位图，等待编码完成进入 _ready。
        Assert.False(registry.TryRegister(frozenBitmap!, out _, out var pending));
        Assert.Equal("RuntimeAssetPending", pending);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(registry.TryRegister(frozenBitmap!, out _, out _));
        Assert.Equal(1, registry.ReadyAssetCount);

        // 模拟布局切换：active snapshot 不再包含此图片。
        registry.ReplaceActiveSources([]);
        Assert.Equal(0, registry.ReadyAssetCount);
        Assert.Equal(0, registry.PendingAssetCount);
        Assert.Equal(0, registry.FailureAssetCount);
    }

    /// <summary>
    /// 验证 ReplaceActiveSources 保留仍在快照中的资源。
    /// </summary>
    [Fact]
    public async Task ReplaceActiveSources_PreservesActiveSources()
    {
        BitmapSource? frozenBitmap = null;
        WpfTestThread.Run(() =>
        {
            frozenBitmap = CreateFrozenBitmap(16, 16);
        });

        using var registry = new WebRuntimeAssetRegistry();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registry.AssetStateChanged += (_, _) => changed.TrySetResult();

        Assert.False(registry.TryRegister(frozenBitmap!, out _, out _));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(registry.TryRegister(frozenBitmap!, out _, out _));
        Assert.Equal(1, registry.ReadyAssetCount);

        // active snapshot 仍包含此图片，应保留。
        registry.ReplaceActiveSources([frozenBitmap!]);
        Assert.Equal(1, registry.ReadyAssetCount);
    }

    /// <summary>
    /// 验证 ReplaceActiveSources 清理本地文件资源。
    /// </summary>
    [Fact]
    public async Task ReplaceActiveSources_ClearsLocalFileAsset()
    {
        var path = Path.Combine(Path.GetTempPath(), $"neo-bpsys-phase3-local-{Guid.NewGuid():N}.png");
        try
        {
            WpfTestThread.Run(() =>
            {
                var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(path)) encoder.Save(stream);
            });

            using var registry = new WebRuntimeAssetRegistry();
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            registry.AssetStateChanged += (_, _) => changed.TrySetResult();

            BitmapImage? localImage = null;
            WpfTestThread.Run(() =>
            {
                localImage = new BitmapImage(new Uri(path));
                Assert.False(registry.TryRegister(localImage, out _, out var pending));
                Assert.Equal("RuntimeAssetPending", pending);
            });
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.True(registry.TryRegister(localImage!, out _, out _));
            Assert.Equal(1, registry.ReadyAssetCount);

            // 布局切换：本地文件资源应被清理。
            registry.ReplaceActiveSources([]);
            Assert.Equal(0, registry.ReadyAssetCount);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    /// <summary>
    /// 验证编码完成后 ReplaceActiveSources 清理，重新注册会重新编码。
    /// 这是端到端验证：清理后资源完全移除，重新注册触发新一轮编码。
    /// </summary>
    [Fact]
    public async Task ReplaceActiveSources_AfterClear_ReRegisterTriggersReEncoding()
    {
        BitmapSource? frozenBitmap = null;
        WpfTestThread.Run(() =>
        {
            frozenBitmap = CreateFrozenBitmap(16, 16);
        });

        using var registry = new WebRuntimeAssetRegistry();

        // 第一轮：注册 → 编码完成 → 进入 _ready。
        var firstChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registry.AssetStateChanged += (_, _) => firstChanged.TrySetResult();
        Assert.False(registry.TryRegister(frozenBitmap!, out _, out _));
        await firstChanged.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(registry.TryRegister(frozenBitmap!, out _, out _));
        Assert.Equal(1, registry.ReadyAssetCount);

        // 布局切换清理。
        registry.ReplaceActiveSources([]);
        Assert.Equal(0, registry.ReadyAssetCount);

        // 第二轮：重新注册应触发重新编码（pending）。
        var secondChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (_, _) => secondChanged.TrySetResult();
        registry.AssetStateChanged += handler;
        try
        {
            Assert.False(registry.TryRegister(frozenBitmap!, out _, out var pending));
            Assert.Equal("RuntimeAssetPending", pending);
            await secondChanged.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.True(registry.TryRegister(frozenBitmap!, out _, out _));
            Assert.Equal(1, registry.ReadyAssetCount);
        }
        finally
        {
            registry.AssetStateChanged -= handler;
        }
    }

    /// <summary>
    /// 验证注册后立即清理（编码可能仍在进行），最终 ReadyAssetCount 为 0。
    /// 无论编码在清理前还是清理后完成，source 都不应残留在 _ready 中。
    /// </summary>
    [Fact]
    public async Task ReplaceActiveSources_ImmediatelyAfterRegister_KeepsReadyEmpty()
    {
        BitmapSource? frozenBitmap = null;
        WpfTestThread.Run(() =>
        {
            frozenBitmap = CreateFrozenBitmap(32, 32);
        });

        using var registry = new WebRuntimeAssetRegistry();

        // 注册后立即清理（编码可能仍在进行）。
        Assert.False(registry.TryRegister(frozenBitmap!, out _, out _));
        registry.ReplaceActiveSources([]);
        Assert.Equal(0, registry.PendingAssetCount);

        // 等待足够长时间确保编码任务完成。
        // 如果 CompleteEncoding 的 lifecycle 验证生效，过期结果被丢弃，_ready 保持为 0。
        // 如果编码在清理前完成，source 短暂进入 _ready 但已被 ReplaceActiveSources 清理。
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(0, registry.ReadyAssetCount);
        Assert.Equal(0, registry.PendingAssetCount);
        Assert.Equal(0, registry.FailureAssetCount);
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
}
