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
    /// <summary>HTTP 与 HTTPS BitmapImage 必须被识别为远程资源，其他网络 scheme 必须拒绝。</summary>
    [Theory]
    [InlineData("http://images.example.test/team.png", WebImageSourceKind.RemoteHttp)]
    [InlineData("https://images.example.test/player.png", WebImageSourceKind.RemoteHttp)]
    [InlineData("file:///C:/team.png", WebImageSourceKind.LocalFile)]
    [InlineData("ftp://images.example.test/player.png", WebImageSourceKind.Invalid)]
    [InlineData("https://user:secret@images.example.test/player.png", WebImageSourceKind.Invalid)]
    public void BitmapImageUriClassificationIsSchemeBased(string value, WebImageSourceKind expected)
    {
        WpfTestThread.Run(() =>
        {
            var image = CreateDeferredBitmap(value);
            Assert.Equal(expected, WebRuntimeAssetRegistry.Classify(image));
        });
    }

    /// <summary>远程图片只发布描述符；sidecar 确认后才变为 resolved proxy asset。</summary>
    [Fact]
    public void RemoteBitmapTransitionsFromPendingToResolvedProxyAsset()
    {
        WpfTestThread.Run(() =>
        {
            using var registry = new WebRuntimeAssetRegistry();
            var image = CreateDeferredBitmap("https://images.example.test/player.png");

            Assert.False(registry.TryRegister(image, out _, out var pending));
            Assert.Equal("RuntimeAssetPending", pending);
            var request = Assert.Single(registry.DrainRemoteRequests());
            Assert.DoesNotContain("images.example.test", request.Token);

            registry.CompleteRemote(request.Token, request.Revision, "image/png", null);

            Assert.True(registry.TryRegister(image, out var asset, out var error));
            Assert.Null(error);
            Assert.Equal("remote", asset.SourceKind);
            Assert.Equal($"/remote-assets/{request.Token}", asset.Url);
        });
    }

    /// <summary>URL 更新后旧 token 的结果必须被丢弃，只有当前图片可以解析。</summary>
    [Fact]
    public void RemoteBitmapUrlUpdateIsLatestWins()
    {
        WpfTestThread.Run(() =>
        {
            using var registry = new WebRuntimeAssetRegistry();
            var previous = CreateDeferredBitmap("https://images.example.test/player-v1.png");
            var current = CreateDeferredBitmap("https://images.example.test/player-v2.png");
            Assert.False(registry.TryRegister(previous, out _, out _));
            var previousRequest = Assert.Single(registry.DrainRemoteRequests());
            Assert.False(registry.TryRegister(current, out _, out _));
            var currentRequest = Assert.Single(registry.DrainRemoteRequests());

            registry.ReplaceRemoteSources([current]);
            registry.CompleteRemote(previousRequest.Token, previousRequest.Revision, "image/png", null);
            Assert.False(registry.TryRegister(current, out _, out var pending));
            Assert.Equal("RuntimeAssetPending", pending);

            registry.CompleteRemote(currentRequest.Token, currentRequest.Revision, "image/png", null);
            Assert.True(registry.TryRegister(current, out var asset, out _));
            Assert.Equal(currentRequest.Token, asset.Token);
        });
    }

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

    /// <summary>本地 BitmapImage 的 UriSource 必须在线程切换前读取。</summary>
    [Fact]
    public async Task LocalBitmapReadsUriSourceOnRegisteringThread()
    {
        var path = Path.Combine(Path.GetTempPath(), $"neo-bpsys-local-{Guid.NewGuid():N}.png");
        try
        {
            using var registry = new WebRuntimeAssetRegistry();
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            registry.AssetStateChanged += (_, _) => changed.TrySetResult();
            WpfTestThread.Run(() =>
            {
                var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(path)) encoder.Save(stream);
                var image = new BitmapImage(new Uri(path));
                Assert.False(registry.TryRegister(image, out _, out var pending));
                Assert.Equal("RuntimeAssetPending", pending);
            });
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
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

    private static BitmapImage CreateDeferredBitmap(string uri)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CreateOptions = BitmapCreateOptions.DelayCreation;
        image.UriSource = new Uri(uri, UriKind.Absolute);
        image.EndInit();
        return image;
    }
}
