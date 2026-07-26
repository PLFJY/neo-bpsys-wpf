using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>在不改变 WPF 编排器所有权的前提下协调 Web Exit/Enter 图。</summary>
public sealed class WebTransitionOrchestratorDecorator(
    IFrontedTransitionOrchestrator inner,
    IWebTransitionGateway gateway,
    WebRendererRuntimeStatePublisher runtimePublisher,
    WebRendererLaunchOptions options,
    ILogger<WebTransitionOrchestratorDecorator> logger) : IFrontedTransitionOrchestrator
{
    /// <inheritdoc />
    public Task RunTransitionAsync(FrontedTransitionRequest request, Func<Task> commitAsync, CancellationToken cancellationToken = default) =>
        RunMultiTargetTransitionAsync([request], commitAsync, cancellationToken);

    /// <inheritdoc />
    public async Task RunMultiTargetTransitionAsync(IReadOnlyList<FrontedTransitionRequest> requests, Func<Task> commitAsync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(commitAsync);
        var session = gateway.Prepare(requests, gateway.CurrentGeneration, cancellationToken);
        var committed = false;
        try
        {
            try { await gateway.WaitForExitAsync(session, cancellationToken).WaitAsync(options.ExitTimeout, CancellationToken.None); }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { logger.LogDebug(ex, "Web transition exit failed open."); }
            await inner.RunMultiTargetTransitionAsync(requests, async () =>
            {
                var barrier = runtimePublisher.BeginCommitBarrier(requests, session.Generation);
                try
                {
                    await commitAsync();
                    committed = true;
                    var commitPoint = await runtimePublisher.WaitForCommitBarrierAsync(
                        barrier,
                        options.EnterTimeout,
                        CancellationToken.None);
                    logger.LogInformation(
                        "Web transition commit barrier completed. CorrelationId={CorrelationId}, RequiredGeneration={RequiredGeneration}, RequiredSequence={RequiredSequence}, IsStable={IsStable}.",
                        session.CorrelationId,
                        commitPoint.Generation,
                        commitPoint.Sequence,
                        commitPoint.IsStable);
                    gateway.Commit(session, commitPoint.Generation, commitPoint.Sequence);
                }
                catch
                {
                    runtimePublisher.CancelCommitBarrier(barrier);
                    throw;
                }
            }, cancellationToken);
            if (committed)
            {
                try { await gateway.WaitForEnterAsync(session, cancellationToken).WaitAsync(options.EnterTimeout, CancellationToken.None); }
                catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { logger.LogDebug(ex, "Web transition enter failed open."); }
            }
        }
        finally { gateway.Cancel(session, committed ? "completed" : "not-committed"); }
    }
}
