extern alias smartbp;

using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using ISmartBpReconciliationService = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpReconciliationService;
using SmartBpBusinessStateRecognitionResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpBusinessStateRecognitionResult;
using SmartBpGameStateSyncService = smartbp::neo_bpsys_wpf.SmartBp.Module.Services.Recognition.SmartBpGameStateSyncService;
using SmartBpOperationApplyResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpOperationApplyResult;
using SmartBpProgressSyncResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpProgressSyncResult;
using SmartBpReconciliationMode = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpReconciliationMode;
using SmartBpReconciliationResult = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpReconciliationResult;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SmartBpProgressSyncServiceTest
{
    [Fact]
    public async Task GameStateSyncReportsCharacterEmptyAndGuidanceResultsSeparately()
    {
        var observed = new SmartBpBusinessStateRecognitionResult { Phase = "选择求生者" };
        var reconciliation = new Mock<ISmartBpReconciliationService>();
        reconciliation.Setup(service => service.ReconcileAsync(
                observed,
                SmartBpReconciliationMode.ManualForceSync,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmartBpReconciliationResult(
                new SmartBpOperationApplyResult(2, 1, ["characters"]),
                new SmartBpOperationApplyResult(1, 0, ["empty"]),
                new SmartBpProgressSyncResult(false, false, 0, null, null, [], "guidance held", ["guidance"]),
                ["diagnostic"]));
        var service = new SmartBpGameStateSyncService(reconciliation.Object);

        var result = await service.ForceSyncAsync(observed);

        Assert.Equal(2, result.ApplyResult?.AppliedCount);
        Assert.Equal(1, result.EmptyApplyResult?.AppliedCount);
        Assert.False(result.ProgressSync.Succeeded);
        Assert.Equal("guidance held", result.ProgressSync.Message);
    }

    [Fact]
    public async Task GameStateSyncKeepsCharacterSuccessWhenGuidanceCannotAlign()
    {
        var observed = new SmartBpBusinessStateRecognitionResult { Phase = "未知" };
        var reconciliation = new Mock<ISmartBpReconciliationService>();
        reconciliation.Setup(service => service.ReconcileAsync(
                observed,
                SmartBpReconciliationMode.ManualForceSync,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmartBpReconciliationResult(
                new SmartBpOperationApplyResult(1, 0, ["character applied"]),
                new SmartBpOperationApplyResult(0, 0, []),
                new SmartBpProgressSyncResult(false, false, 0, null, null, [], "phase unknown", []),
                []));
        var service = new SmartBpGameStateSyncService(reconciliation.Object);

        var result = await service.ForceSyncAsync(observed);

        Assert.Equal(1, result.ApplyResult?.AppliedCount);
        Assert.False(result.ProgressSync.Succeeded);
        reconciliation.Verify(service => service.ReconcileAsync(
            observed,
            SmartBpReconciliationMode.ManualForceSync,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
