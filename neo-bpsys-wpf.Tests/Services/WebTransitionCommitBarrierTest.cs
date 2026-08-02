using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.WebRenderer.Services;
using neo_bpsys_wpf.WebRenderer.Protocol;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>Web Transition runtime commit 屏障测试。</summary>
public sealed class WebTransitionCommitBarrierTest
{
    /// <summary>generation 不匹配或无客户端时屏障必须立即 fail-open。</summary>
    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public async Task BarrierFailsOpenForUnavailableGeneration(long requestedGeneration)
    {
        using var publisher = new WebRendererRuntimeStatePublisher(
            Mock.Of<ISharedDataService>(),
            Mock.Of<IFrontedEventBus>());
        publisher.ReplaceLayout(new WebRendererBootstrapSnapshot(
            WebRendererIpcProtocol.Version,
            7,
            "builtin",
            [],
            new Dictionary<string, WebRendererAsset>()));

        var barrier = publisher.BeginCommitBarrier([], requestedGeneration);
        var point = await publisher.WaitForCommitBarrierAsync(
            barrier,
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken);

        Assert.False(point.IsStable);
        Assert.Equal(7, point.Generation);
    }

    /// <summary>committed 信号必须携带 EnterGraph 要等待的 runtime generation/sequence。</summary>
    [Fact]
    public void GatewayPublishesRequiredRuntimeCommitPoint()
    {
        var gateway = new WebTransitionGateway();
        gateway.UpdateGeneration(9);
        gateway.SetClientCount(1);
        WebTransitionSignal? committed = null;
        gateway.SignalPublished += (_, signal) =>
        {
            if (signal.Type.EndsWith("committed", StringComparison.Ordinal)) committed = signal;
        };
        var session = gateway.Prepare(
            [new FrontedTransitionRequest { TargetBehaviorGuid = Guid.NewGuid() }],
            9,
            TestContext.Current.CancellationToken);

        gateway.Commit(session, 9, 42);

        Assert.NotNull(committed);
        Assert.Equal(9, committed.Session.RequiredGeneration);
        Assert.Equal(42, committed.Session.RequiredSequence);
        gateway.Cancel(session, "test-complete");
    }
}
