using System.Text.Json;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Defines stable branch-count and port-name behavior for <c>flow.parallel</c> nodes.
/// </summary>
public static class FrontedParallelNodePorts
{
    /// <summary>Gets the default branch count used by legacy and newly created parallel nodes.</summary>
    public const int DefaultBranchCount = 3;

    /// <summary>Gets the minimum supported parallel branch count.</summary>
    public const int MinBranchCount = 1;

    /// <summary>Gets the maximum supported parallel branch count.</summary>
    public const int MaxBranchCount = 20;

    /// <summary>
    /// Gets the normalized configured branch count for a parallel node.
    /// </summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns>A branch count between <see cref="MinBranchCount"/> and <see cref="MaxBranchCount"/>.</returns>
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
    /// Enumerates stable branch port names for the configured branch count.
    /// </summary>
    /// <param name="branchCount">The requested branch count.</param>
    /// <returns>Stable port names from <c>Branch1</c> through the normalized count.</returns>
    public static IEnumerable<string> GetBranchPortNames(int branchCount) =>
        Enumerable.Range(1, Math.Clamp(branchCount, MinBranchCount, MaxBranchCount))
            .Select(index => $"Branch{index}");

    /// <summary>
    /// Determines whether a port name is a stable parallel branch port.
    /// </summary>
    /// <param name="portName">The port name to inspect.</param>
    /// <param name="branchIndex">The parsed one-based branch index when successful.</param>
    /// <returns><see langword="true"/> when the name is between <c>Branch1</c> and <c>Branch20</c>.</returns>
    public static bool TryGetBranchIndex(string? portName, out int branchIndex)
    {
        branchIndex = 0;
        return portName?.StartsWith("Branch", StringComparison.Ordinal) == true
               && int.TryParse(portName["Branch".Length..], out branchIndex)
               && branchIndex is >= MinBranchCount and <= MaxBranchCount;
    }
}
