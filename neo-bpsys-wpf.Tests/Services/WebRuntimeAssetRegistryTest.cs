using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.WebRenderer.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Verifies lifecycle cleanup for image assets published to the web renderer.
/// </summary>
public sealed class WebRuntimeAssetRegistryTest
{
    /// <summary>
    /// Verifies inactive ready assets are released while active assets remain available.
    /// </summary>
    [Fact]
    public async Task ReplacingActiveSourcesReleasesOnlyInactiveReadyAssets()
    {
        var active = FrozenBitmap();
        var inactive = FrozenBitmap();
        using var registry = new WebRuntimeAssetRegistry();
        await RegisterReadyAsync(registry, active);
        await RegisterReadyAsync(registry, inactive);
        Assert.Equal(2, registry.ReadyAssetCount);

        registry.ReplaceActiveSources([active]);

        Assert.Equal(1, registry.ReadyAssetCount);
        Assert.True(registry.TryRegister(active, out _, out _));
        Assert.False(registry.TryRegister(inactive, out _, out var state));
        Assert.Equal("RuntimeAssetPending", state);
    }

    /// <summary>
    /// Verifies an asset can be encoded again after it has been removed from the active snapshot.
    /// </summary>
    [Fact]
    public async Task ReleasedAssetCanBeRegisteredAgain()
    {
        var bitmap = FrozenBitmap();
        using var registry = new WebRuntimeAssetRegistry();
        await RegisterReadyAsync(registry, bitmap);

        registry.ReplaceActiveSources([]);
        Assert.Equal(0, registry.ReadyAssetCount);

        await RegisterReadyAsync(registry, bitmap);
        Assert.Equal(1, registry.ReadyAssetCount);
    }

    /// <summary>
    /// Verifies a pending encoding result cannot reappear after its source is released.
    /// </summary>
    [Fact]
    public async Task ReleasingPendingAssetDiscardsLateEncodingResult()
    {
        var bitmap = FrozenBitmap(32, 32);
        using var registry = new WebRuntimeAssetRegistry();

        Assert.False(registry.TryRegister(bitmap, out _, out _));
        registry.ReplaceActiveSources([]);
        Assert.Equal(0, registry.PendingAssetCount);

        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(0, registry.ReadyAssetCount);
        Assert.Equal(0, registry.PendingAssetCount);
        Assert.Equal(0, registry.FailureAssetCount);
    }

    private static async Task RegisterReadyAsync(WebRuntimeAssetRegistry registry, BitmapSource bitmap)
    {
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (_, _) => changed.TrySetResult();
        registry.AssetStateChanged += handler;
        try
        {
            if (!registry.TryRegister(bitmap, out _, out _))
            {
                await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            }

            Assert.True(registry.TryRegister(bitmap, out _, out _));
        }
        finally
        {
            registry.AssetStateChanged -= handler;
        }
    }

    private static BitmapSource FrozenBitmap(int width = 16, int height = 16)
    {
        BitmapSource? bitmap = null;
        WpfTestThread.Run(() =>
        {
            bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                new byte[width * height * 4],
                width * 4);
            bitmap.Freeze();
        });
        return bitmap!;
    }
}
