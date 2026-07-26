using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 前台节点图运行时接口，负责执行行为节点图并返回执行结果。
/// </summary>
public interface IFrontedNodeGraphRuntime
{
    /// <summary>
    /// 执行指定的节点图。
    /// </summary>
    /// <param name="graph">要执行的节点图。</param>
    /// <param name="context">图执行上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>图执行结果。</returns>
    Task<FrontedGraphExecutionResult> ExecuteAsync(
        FrontedNodeGraph graph,
        FrontedGraphExecutionContext context,
        CancellationToken cancellationToken = default);
}
