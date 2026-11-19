using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.Helpers;

/// <summary>保留未变化行的对象和容器，以稳定身份协调呈现集合。</summary>
internal static class PresentationCollection
{
    internal static void Reconcile<T>(ObservableCollection<T> target, IEnumerable<T> source,
        Func<T, string> identity, Func<T, T, bool> equivalent, IEqualityComparer<string>? comparer = null)
        where T : class
    {
        comparer ??= StringComparer.Ordinal;
        var incoming = source.ToArray();
        // 在修改可见集合前核验身份，失败时保留上一次完整呈现。
        var desired = incoming.ToDictionary(identity, comparer);
        var existing = target.ToDictionary(identity, comparer);
        for (var i = target.Count - 1; i >= 0; i--)
            if (!desired.ContainsKey(identity(target[i]))) target.RemoveAt(i);

        for (var i = 0; i < incoming.Length; i++)
        {
            var next = incoming[i];
            if (existing.TryGetValue(identity(next), out var previous))
            {
                var position = i < target.Count && ReferenceEquals(target[i], previous) ? i : target.IndexOf(previous);
                if (position != i) target.Move(position, i);
                if (!ReferenceEquals(previous, next) && !equivalent(previous, next)) target[i] = next;
            }
            else target.Insert(i, next);
        }
    }
}
