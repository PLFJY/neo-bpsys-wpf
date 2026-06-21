using Microsoft.Extensions.Logging;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services;
using Wpf.Ui;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class GameGuidanceCompletionTest
{
    [Fact]
    public void CompleteGuidance_FiresStoppedWithNonCancelReason()
    {
        var infoBar = new Mock<IInfoBarService>();
        var service = new GameGuidanceService(
            Mock.Of<ISharedDataService>(),
            Mock.Of<INavigationService>(),
            infoBar.Object,
            Mock.Of<ILogger<GameGuidanceService>>());
        var stopped = 0;
        var cancelled = 0;
        string? reason = null;
        service.GuidanceStopped += (_, args) => { stopped++; reason = args.Reason; };
        service.GuidanceCancelled += (_, _) => cancelled++;
        service.IsGuidanceStarted = true;

        service.CompleteGuidance("SmartBpCharacterBpEnded");

        Assert.False(service.IsGuidanceStarted);
        Assert.Equal(1, stopped);
        Assert.Equal(0, cancelled);
        Assert.Equal("SmartBpCharacterBpEnded", reason);
        infoBar.Verify(x => x.CloseInfoBar(), Times.Once);
    }

    [Fact]
    public void StopGuidance_FiresCancelled()
    {
        var service = new GameGuidanceService(
            Mock.Of<ISharedDataService>(),
            Mock.Of<INavigationService>(),
            Mock.Of<IInfoBarService>(),
            Mock.Of<ILogger<GameGuidanceService>>());
        var stopped = 0;
        var cancelled = 0;
        string? reason = null;
        service.GuidanceStopped += (_, _) => stopped++;
        service.GuidanceCancelled += (_, args) => { cancelled++; reason = args.Reason; };
        service.IsGuidanceStarted = true;

        service.StopGuidance();

        Assert.False(service.IsGuidanceStarted);
        Assert.Equal(0, stopped);
        Assert.Equal(1, cancelled);
        Assert.Equal("Cancelled", reason);
    }
}
