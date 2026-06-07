using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IFrontedNodeGraphRuntime
{
    Task<FrontedGraphExecutionResult> ExecuteAsync(
        FrontedNodeGraph graph,
        FrontedGraphExecutionContext context,
        CancellationToken cancellationToken = default);
}
