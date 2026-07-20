using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.WebRenderer.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>Web Renderer 动态图片协议测试。</summary>
public sealed class WebRendererImageRuntimeTest
{
    /// <summary>资源协议应同时保存 WPF DIP、像素尺寸和源 DPI。</summary>
    [Fact]
    public async Task FrozenBitmapPublishesNaturalDipPixelAndDpiDimensions()
    {
        BitmapSource? source = null;
        WpfTestThread.Run(() =>
        {
            const int pixelWidth = 200;
            const int pixelHeight = 100;
            const int stride = pixelWidth * 4;
            source = BitmapSource.Create(
                pixelWidth,
                pixelHeight,
                192,
                192,
                PixelFormats.Bgra32,
                null,
                new byte[stride * pixelHeight],
                stride);
            source.Freeze();
        });

        using var registry = new WebRuntimeAssetRegistry();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registry.AssetStateChanged += (_, _) => changed.TrySetResult();

        Assert.False(registry.TryRegister(source!, out _, out var pending));
        Assert.Equal("RuntimeAssetPending", pending);
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(registry.TryRegister(source!, out var asset, out var error));

        Assert.Null(error);
        Assert.NotNull(asset.NaturalWidthDip);
        Assert.NotNull(asset.NaturalHeightDip);
        Assert.Equal(100D, asset.NaturalWidthDip.Value, 6);
        Assert.Equal(50D, asset.NaturalHeightDip.Value, 6);
        Assert.Equal(200, asset.PixelWidth);
        Assert.Equal(100, asset.PixelHeight);
        Assert.NotNull(asset.DpiX);
        Assert.NotNull(asset.DpiY);
        Assert.Equal(192D, asset.DpiX.Value, 6);
        Assert.Equal(192D, asset.DpiY.Value, 6);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, asset.Url);
    }

    /// <summary>业务 null、pending 和 failed 必须是互不混淆的显式状态。</summary>
    [Fact]
    public void RuntimeValueStatesDistinguishBusinessNullAndAssetPreparation()
    {
        using var registry = new WebRuntimeAssetRegistry();
        var factory = new WebRuntimeValueFactory(registry);

        var nullValue = factory.Create(null, "CurrentGame.Player.Character", out var diagnostic);

        Assert.Equal(WebRuntimeValueStates.Null, nullValue.State);
        Assert.Null(diagnostic);
        Assert.Equal("pending", WebRuntimeValueStates.Pending);
        Assert.Equal("failed", WebRuntimeValueStates.Failed);
    }
}
