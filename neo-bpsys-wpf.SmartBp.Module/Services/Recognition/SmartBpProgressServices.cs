using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 将手动强制同步委托给唯一的 Reconciliation 流程，使角色、明确空操作与 Guidance 分别报告结果。
/// </summary>
internal sealed class SmartBpGameStateSyncService(
    ISmartBpReconciliationService reconciliation,
    ISmartBpDebugLog? debugLog = null) : ISmartBpGameStateSyncService
{
    /// <inheritdoc />
    public async Task<SmartBpGameStateSyncResult> ForceSyncAsync(
        SmartBpBusinessStateRecognitionResult observed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observed);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await reconciliation.ReconcileAsync(
            observed,
            SmartBpReconciliationMode.ManualForceSync,
            cancellationToken);
        return Finish(new(
            result.GuidanceResult,
            result.CharacterApplyResult,
            result.Diagnostics,
            result.EmptyApplyResult));
    }

    private SmartBpGameStateSyncResult Finish(SmartBpGameStateSyncResult result)
    {
        debugLog?.Write("GameStateSync", result.ProgressSync.Message);
        foreach (var diagnostic in result.Diagnostics)
            debugLog?.Write("GameStateSync", diagnostic);
        return result;
    }
}
