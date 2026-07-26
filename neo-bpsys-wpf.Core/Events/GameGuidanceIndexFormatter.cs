namespace neo_bpsys_wpf.Core.Events;

internal static class GameGuidanceIndexFormatter
{
    internal static string FormatIndexes(IReadOnlyCollection<int>? indexes) =>
        indexes is null || indexes.Count == 0
            ? "[]"
            : $"[{string.Join(", ", indexes)}]";
}
