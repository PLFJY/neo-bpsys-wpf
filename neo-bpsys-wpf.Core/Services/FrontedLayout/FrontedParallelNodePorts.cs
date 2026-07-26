using System.Text.Json;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 定义 <c>flow.parallel</c> 节点的稳定分支数和端口名称行为。
/// </summary>
public static class FrontedParallelNodePorts
{
    /// <summary>获取旧版和新建并行节点使用的默认分支数。</summary>
    public const int DefaultBranchCount = 3;

    /// <summary>获取支持的最小并行分支数。</summary>
    public const int MinBranchCount = 1;

    /// <summary>获取支持的最大并行分支数。</summary>
    public const int MaxBranchCount = 20;

    /// <summary>
    /// 获取并行节点的规范化配置分支数。
    /// </summary>
    /// <param name="node">要检查的节点。</param>
    /// <returns>介于 <see cref="MinBranchCount"/> 和 <see cref="MaxBranchCount"/> 之间的分支数。</returns>
    public static int GetBranchCount(FrontedNode node)
    {
        if (!node.Properties.TryGetValue("BranchCount", out var value))
        {
            return DefaultBranchCount;
        }

        var parsed = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
                ? number
                : DefaultBranchCount;
        return Math.Clamp(parsed, MinBranchCount, MaxBranchCount);
    }

    /// <summary>
    /// 枚举配置分支数的稳定分支端口名称。
    /// </summary>
    /// <param name="branchCount">请求的分支数。</param>
    /// <returns>从 <c>Branch1</c> 到规范化计数的稳定端口名称。</returns>
    public static IEnumerable<string> GetBranchPortNames(int branchCount) =>
        Enumerable.Range(1, Math.Clamp(branchCount, MinBranchCount, MaxBranchCount))
            .Select(index => $"Branch{index}");

    /// <summary>
    /// 确定端口名称是否为稳定的并行分支端口。
    /// </summary>
    /// <param name="portName">要检查的端口名称。</param>
    /// <param name="branchIndex">成功时解析得到的从 1 开始的分支索引。</param>
    /// <returns>当名称介于 <c>Branch1</c> 和 <c>Branch20</c> 之间时为 <see langword="true"/>。</returns>
    public static bool TryGetBranchIndex(string? portName, out int branchIndex)
    {
        branchIndex = 0;
        return portName?.StartsWith("Branch", StringComparison.Ordinal) == true
               && int.TryParse(portName["Branch".Length..], out branchIndex)
               && branchIndex is >= MinBranchCount and <= MaxBranchCount;
    }
}
