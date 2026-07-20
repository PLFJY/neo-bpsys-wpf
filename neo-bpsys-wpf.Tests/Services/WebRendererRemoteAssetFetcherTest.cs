extern alias host;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using AddressPolicy = host::neo_bpsys_wpf.WebRenderer.Host.IRemoteAssetAddressPolicy;
using ProductionAddressPolicy = host::neo_bpsys_wpf.WebRenderer.Host.RemoteAssetAddressPolicy;
using RemoteAssetException = host::neo_bpsys_wpf.WebRenderer.Host.RemoteAssetException;
using RemoteAssetFetcher = host::neo_bpsys_wpf.WebRenderer.Host.RemoteAssetFetcher;
using RemoteAssetFetch = host::neo_bpsys_wpf.WebRenderer.Protocol.WebRemoteAssetFetch;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>Web Renderer sidecar 远程图片代理测试。</summary>
public sealed class WebRendererRemoteAssetFetcherTest
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3];

    /// <summary>允许的图片格式应按原始字节缓存并授权代理 token。</summary>
    [Theory]
    [InlineData("image/png", "png")]
    [InlineData("image/jpeg", "jpeg")]
    [InlineData("image/webp", "webp")]
    [InlineData("image/gif", "gif")]
    public async Task DownloadsSupportedImageFormats(string contentType, string kind)
    {
        var bytes = ImageBytes(kind);
        await WithFetcherAsync((_, _) => Task.FromResult(Response(HttpStatusCode.OK, contentType, bytes)), async (fetcher, _) =>
        {
            fetcher.SetGeneration(1);
            var request = Request(1, 'a', 'b', $"https://images.example.test/photo.{kind}");

            var result = await fetcher.FetchAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(contentType, result.ContentType);
            Assert.Equal(bytes, result.Bytes);
            Assert.True(fetcher.TryGet(request.Token, 1, out var authorized));
            Assert.Same(result, authorized);
        });
    }

    /// <summary>相同 revision 的并发下载必须合并为一个 HTTP 请求。</summary>
    [Fact]
    public async Task CoalescesConcurrentRequestsForSameUrl()
    {
        var calls = 0;
        await WithFetcherAsync(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(50, cancellationToken);
            return Response(HttpStatusCode.OK, "image/png", Png);
        }, async (fetcher, _) =>
        {
            fetcher.SetGeneration(1);
            var first = fetcher.FetchAsync(Request(1, 'a', 'b', "https://images.example.test/shared.png"), TestContext.Current.CancellationToken);
            var second = fetcher.FetchAsync(Request(1, 'c', 'b', "https://images.example.test/shared.png"), TestContext.Current.CancellationToken);

            await Task.WhenAll(first, second);

            Assert.Equal(1, calls);
        });
    }

    /// <summary>取消一个等待者不得取消同 revision 的共享下载。</summary>
    [Fact]
    public async Task CallerCancellationDoesNotCancelSharedDownload()
    {
        var calls = 0;
        await WithFetcherAsync(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(75, cancellationToken);
            return Response(HttpStatusCode.OK, "image/png", Png);
        }, async (fetcher, _) =>
        {
            fetcher.SetGeneration(1);
            using var firstCancellation = new CancellationTokenSource();
            var first = fetcher.FetchAsync(Request(1, 'a', 'b', "https://images.example.test/shared.png"),
                firstCancellation.Token);
            var second = fetcher.FetchAsync(Request(1, 'c', 'b', "https://images.example.test/shared.png"),
                TestContext.Current.CancellationToken);

            firstCancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            Assert.Equal("image/png", (await second).ContentType);
            Assert.Equal(1, calls);
        });
    }

    /// <summary>重定向必须受限，超时和响应体大小必须取消或拒绝下载。</summary>
    [Fact]
    public async Task EnforcesRedirectTimeoutAndSizeLimits()
    {
        await WithFetcherAsync((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.StartsWith("/redirect")
            ? Redirect("/redirect-next")
            : Response(HttpStatusCode.OK, "image/png", Png)), async (_, root) =>
        {
            using var client = new HttpClient(new DelegateHandler((request, _) => Task.FromResult(Redirect(request.RequestUri!.AbsolutePath + "-next"))));
            var fetcher = new RemoteAssetFetcher(client, new AllowAllAddressPolicy(), root, maxRedirects: 1);
            fetcher.SetGeneration(1);
            var error = await Assert.ThrowsAsync<RemoteAssetException>(() => fetcher.FetchAsync(
                Request(1, 'a', 'b', "https://images.example.test/redirect"), TestContext.Current.CancellationToken));
            Assert.Equal("RemoteAssetRedirectLimitExceeded", error.Diagnostic);
        });

        await WithFetcherAsync(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            return Response(HttpStatusCode.OK, "image/png", Png);
        }, async (_, root) =>
        {
            using var client = new HttpClient(new DelegateHandler(async (_, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                return Response(HttpStatusCode.OK, "image/png", Png);
            }));
            var fetcher = new RemoteAssetFetcher(client, new AllowAllAddressPolicy(), root,
                totalTimeout: TimeSpan.FromMilliseconds(30));
            fetcher.SetGeneration(1);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetcher.FetchAsync(
                Request(1, 'c', 'd', "https://images.example.test/slow"), TestContext.Current.CancellationToken));
        });

        await WithFetcherAsync((_, _) => Task.FromResult(Response(HttpStatusCode.OK, "image/png", Png)), async (_, root) =>
        {
            using var client = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(Response(HttpStatusCode.OK, "image/png", Png))));
            var fetcher = new RemoteAssetFetcher(client, new AllowAllAddressPolicy(), root, maxResponseBytes: 8);
            fetcher.SetGeneration(1);
            var error = await Assert.ThrowsAsync<RemoteAssetException>(() => fetcher.FetchAsync(
                Request(1, 'e', 'f', "https://images.example.test/large"), TestContext.Current.CancellationToken));
            Assert.Equal("RemoteAssetTooLarge", error.Diagnostic);
        });
    }

    /// <summary>一个 URL 的失败不得阻止新 URL/revision 立即下载。</summary>
    [Fact]
    public async Task UrlChangeBypassesPreviousFailure()
    {
        await WithFetcherAsync((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/failed"
            ? new HttpResponseMessage(HttpStatusCode.BadGateway)
            : Response(HttpStatusCode.OK, "image/png", Png)), async (fetcher, _) =>
        {
            fetcher.SetGeneration(1);
            await Assert.ThrowsAsync<RemoteAssetException>(() => fetcher.FetchAsync(
                Request(1, 'a', 'b', "https://images.example.test/failed"), TestContext.Current.CancellationToken));

            var result = await fetcher.FetchAsync(
                Request(1, 'c', 'd', "https://images.example.test/updated.png"), TestContext.Current.CancellationToken);

            Assert.Equal("image/png", result.ContentType);
        });
    }

    /// <summary>下载失败不得写入失败缓存，同一资源随后可以成功重试。</summary>
    [Fact]
    public async Task FailedDownloadCanBeRetried()
    {
        var calls = 0;
        await WithFetcherAsync((_, _) => Task.FromResult(Interlocked.Increment(ref calls) == 1
            ? new HttpResponseMessage(HttpStatusCode.BadGateway)
            : Response(HttpStatusCode.OK, "image/png", Png)), async (fetcher, _) =>
        {
            fetcher.SetGeneration(1);
            var request = Request(1, 'a', 'b', "https://images.example.test/retry.png");

            await Assert.ThrowsAsync<RemoteAssetException>(() =>
                fetcher.FetchAsync(request, TestContext.Current.CancellationToken));
            var result = await fetcher.FetchAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal("image/png", result.ContentType);
            Assert.Equal(2, calls);
        });
    }

    /// <summary>完整落盘的成功项应可供新的 sidecar fetcher 实例复用。</summary>
    [Fact]
    public async Task DiskCacheSurvivesFetcherRestart()
    {
        await WithFetcherAsync((_, _) => Task.FromResult(Response(HttpStatusCode.OK, "image/png", Png)),
            async (fetcher, root) =>
            {
                fetcher.SetGeneration(1);
                var request = Request(1, 'a', 'b', "https://images.example.test/cached.png");
                await fetcher.FetchAsync(request, TestContext.Current.CancellationToken);

                var networkCalls = 0;
                using var client = new HttpClient(new DelegateHandler((_, _) =>
                {
                    Interlocked.Increment(ref networkCalls);
                    throw new HttpRequestException("Network should not be used for a disk hit.");
                }));
                var restarted = new RemoteAssetFetcher(client, new AllowAllAddressPolicy(), root);
                restarted.SetGeneration(1);

                var cached = await restarted.FetchAsync(request, TestContext.Current.CancellationToken);

                Assert.Equal(Png, cached.Bytes);
                Assert.Equal(0, networkCalls);
            });
    }

    /// <summary>生产 SSRF 策略必须拒绝本机、链路本地和普通 LAN 地址。</summary>
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    public void ProductionAddressPolicyRejectsNonPublicAddresses(string value)
    {
        Assert.False(ProductionAddressPolicy.IsPublicAddress(IPAddress.Parse(value)));
    }

    /// <summary>生产 URI 策略必须拒绝 userinfo 和非 HTTP/HTTPS scheme。</summary>
    [Theory]
    [InlineData("https://user:secret@images.example.test/photo.png")]
    [InlineData("file:///C:/photo.png")]
    [InlineData("ftp://images.example.test/photo.png")]
    [InlineData("http://localhost/photo.png")]
    public void ProductionAddressPolicyRejectsUnsafeUris(string value)
    {
        var error = Assert.Throws<RemoteAssetException>(() =>
            ProductionAddressPolicy.ValidateUri(new Uri(value, UriKind.Absolute)));
        Assert.Contains("Rejected", error.Diagnostic, StringComparison.Ordinal);
    }

    private static async Task WithFetcherAsync(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        Func<RemoteAssetFetcher, string, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), $"neo-bpsys-remote-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using var client = new HttpClient(new DelegateHandler(handler));
            var fetcher = new RemoteAssetFetcher(client, new AllowAllAddressPolicy(), root);
            await test(fetcher, root);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static RemoteAssetFetch Request(long generation, char token, char revision, string uri) =>
        new(generation, new string(token, 64), new string(revision, 64), uri);

    private static HttpResponseMessage Response(HttpStatusCode status, string contentType, byte[] bytes)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return response;
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static byte[] ImageBytes(string kind) => kind switch
    {
        "png" => Png,
        "jpeg" => [0xff, 0xd8, 0xff, 1],
        "gif" => "GIF89a-data"u8.ToArray(),
        "webp" => "RIFF0000WEBPdata"u8.ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private sealed class AllowAllAddressPolicy : AddressPolicy
    {
        public Task ValidateAsync(Uri uri, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}
