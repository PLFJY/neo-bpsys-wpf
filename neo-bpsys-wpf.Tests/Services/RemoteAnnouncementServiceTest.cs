using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WPFLocalizeExtension.Engine;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>公告镜像源请求顺序和取消行为测试。</summary>
public sealed class RemoteAnnouncementServiceTest
{
    /// <summary>应用语言决定首选源，请求或内容失败后请求另一镜像的清单。</summary>
    /// <param name="cultureName">应用界面文化。</param>
    /// <param name="firstHost">预期首选源主机。</param>
    /// <param name="failure">首选源失败类型。</param>
    /// <returns>测试完成任务。</returns>
    [Theory]
    [InlineData("zh-Hans", "gitee.com", "http")]
    [InlineData("zh-CN", "gitee.com", "json")]
    [InlineData("zh-SG", "gitee.com", "timeout")]
    [InlineData("en-US", "raw.githubusercontent.com", "http")]
    [InlineData("ja-JP", "raw.githubusercontent.com", "json")]
    [InlineData("zh-TW", "raw.githubusercontent.com", "timeout")]
    public async Task Refresh_SourceFailure_TriesMirrorInLanguageOrder(string cultureName, string firstHost, string failure)
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var previous = LocalizeDictionary.Instance.Culture;
            try
            {
                LocalizeDictionary.Instance.Culture = CultureInfo.GetCultureInfo(cultureName);
                var requests = new List<Uri>();
                var handler = new Mock<HttpMessageHandler>();
                handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                    .Returns((HttpRequestMessage request, CancellationToken _) =>
                    {
                        requests.Add(request.RequestUri!);
                        if (requests.Count == 1 && failure == "timeout")
                            throw new TaskCanceledException("Request timed out.");
                        return Task.FromResult(new HttpResponseMessage(
                            requests.Count == 1 && failure == "json" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable)
                        {
                            Content = new StringContent("{\"schemaVersion\":999}")
                        });
                    });
                using var client = new HttpClient(handler.Object);
                var factory = new Mock<IHttpClientFactory>();
                factory.Setup(value => value.CreateClient("RemoteAnnouncements")).Returns(client);
                var service = new RemoteAnnouncementService(factory.Object, NullLogger<RemoteAnnouncementService>.Instance);

                await service.RefreshOnDemandAsync();

                Assert.Equal(2, requests.Count);
                Assert.Equal(firstHost, requests[0].Host);
                Assert.Equal(firstHost == "gitee.com" ? "raw.githubusercontent.com" : "gitee.com", requests[1].Host);
                Assert.All(requests, uri => Assert.EndsWith("/manifest.json", uri.AbsolutePath));
                Assert.True(service.IsStartupCheckCompleted);
                Assert.Empty(service.Announcements);
            }
            finally
            {
                LocalizeDictionary.Instance.Culture = previous;
            }
        });
    }

    /// <summary>调用方取消请求时不再访问备用源。</summary>
    /// <returns>测试完成任务。</returns>
    [Fact]
    public async Task Refresh_CallerCancellation_DoesNotRequestMirror()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            using var cancellation = new CancellationTokenSource();
            var count = 0;
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns((HttpRequestMessage _, CancellationToken token) =>
                {
                    count++;
                    cancellation.Cancel();
                    return Task.FromCanceled<HttpResponseMessage>(token);
                });
            using var client = new HttpClient(handler.Object);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(value => value.CreateClient("RemoteAnnouncements")).Returns(client);
            var service = new RemoteAnnouncementService(factory.Object, NullLogger<RemoteAnnouncementService>.Instance);

            try
            {
                await service.RefreshOnDemandAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // 取消可由 HttpClient 直接传播；不允许继续请求镜像。
            }
            Assert.Equal(1, count);
        });
    }
}