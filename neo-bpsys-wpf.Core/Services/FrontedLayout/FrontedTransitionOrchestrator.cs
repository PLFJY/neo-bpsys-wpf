using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Default transition orchestrator that wraps business commits with fronted behavior graphs.
/// </summary>
public sealed class FrontedTransitionOrchestrator : IFrontedTransitionOrchestrator
{
    private readonly FrontedBehaviorRuntimeHostManager _hostManager;
    private readonly ILogger<FrontedTransitionOrchestrator> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FrontedTransitionOrchestrator"/>.
    /// </summary>
    /// <param name="hostManager">Runtime host manager containing attached fronted behavior hosts.</param>
    /// <param name="logger">Logger for transition diagnostics.</param>
    public FrontedTransitionOrchestrator(
        FrontedBehaviorRuntimeHostManager hostManager,
        ILogger<FrontedTransitionOrchestrator> logger)
    {
        _hostManager = hostManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task RunTransitionAsync(
        FrontedTransitionRequest request,
        Func<Task> commitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(commitAsync);

        return RunMultiTargetTransitionAsync([request], commitAsync, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RunMultiTargetTransitionAsync(
        IReadOnlyList<FrontedTransitionRequest> requests,
        Func<Task> commitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(commitAsync);

        var executions = requests
            .SelectMany(request => _hostManager.CreateTransitionExecutions(request, cancellationToken))
            .ToArray();

        try
        {
            if (executions.Length > 0)
            {
                await Task.WhenAll(executions.Select(execution => execution.RunExitGraphAsync(cancellationToken)));

                if (executions.Any(execution => execution.IsCancellationRequested))
                {
                    _logger.LogInformation(
                        "Transition commit skipped because one or more matching transition executions were interrupted.");
                    return;
                }
            }

            await commitAsync();

            if (executions.Length > 0)
            {
                await Task.WhenAll(executions.Select(execution => execution.RunEnterGraphAsync(cancellationToken)));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Transition commit failed; EnterGraph execution will be skipped.");
            throw;
        }
        finally
        {
            foreach (var execution in executions)
            {
                execution.Complete();
            }
        }
    }
}
