namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台派生渲染结果的有界缓存。
/// </summary>
/// <typeparam name="TKey">派生结果键。</typeparam>
/// <typeparam name="TValue">派生结果值。</typeparam>
/// <remarks>
/// <para>
/// 用语义输入作为键缓存可复用的渲染产物（如染色位图、冻结画刷），并同时按条目数和字节数限制，
/// 避免连续交互产生的大量中间结果无界增长。
/// </para>
/// <para>
/// 缓存本身不负责线程切换；调用方应保证键与值在插入后不可变。值建议为已冻结的 WPF Freezable。
/// </para>
/// </remarks>
internal sealed class FrontedRenderCache<TKey, TValue>
    where TKey : notnull
{
    private readonly object _gate = new();
    private readonly Dictionary<TKey, Entry> _entries = [];
    private readonly LinkedList<TKey> _order = [];
    private readonly int _maxEntries;
    private readonly long _maxBytes;
    private readonly Func<TValue, long>? _sizeSelector;
    private long _estimatedBytes;

    /// <summary>
    /// 初始化有界缓存。
    /// </summary>
    /// <param name="maxEntries">最多保留的条目数，必须大于 0。</param>
    /// <param name="maxBytes">最多保留的估算字节数；小于等于 0 时不按字节限制。</param>
    /// <param name="sizeSelector">估算单个值字节数的委托；为 <see langword="null"/> 时按 0 估算。</param>
    /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="maxEntries"/> 小于等于 0 时抛出。</exception>
    public FrontedRenderCache(int maxEntries, long maxBytes, Func<TValue, long>? sizeSelector = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        _maxEntries = maxEntries;
        _maxBytes = maxBytes;
        _sizeSelector = sizeSelector;
    }

    /// <summary>
    /// 获取当前条目数。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// 获取当前条目估算字节数。
    /// </summary>
    public long EstimatedBytes
    {
        get
        {
            lock (_gate)
            {
                return _estimatedBytes;
            }
        }
    }

    /// <summary>
    /// 尝试读取缓存，命中时更新 LRU 顺序。
    /// </summary>
    /// <param name="key">派生结果键。</param>
    /// <param name="value">命中时返回缓存值。</param>
    /// <returns>命中时为 <see langword="true"/>。</returns>
    public bool TryGet(TKey key, out TValue value)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                value = default!;
                return false;
            }

            _order.Remove(entry.OrderNode);
            _order.AddLast(entry.OrderNode);
            value = entry.Value;
            return true;
        }
    }

    /// <summary>
    /// 写入缓存；键已存在时覆盖旧值并刷新 LRU 顺序。
    /// </summary>
    /// <param name="key">派生结果键。</param>
    /// <param name="value">要缓存的派生结果。</param>
    public void Set(TKey key, TValue value)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                _estimatedBytes -= existing.Size;
                existing.Value = value;
                existing.Size = Estimate(value);
                _estimatedBytes += existing.Size;
                _order.Remove(existing.OrderNode);
                _order.AddLast(existing.OrderNode);
            }
            else
            {
                var node = _order.AddLast(key);
                var entry = new Entry(value, Estimate(value), node);
                _entries[key] = entry;
                _estimatedBytes += entry.Size;
            }

            while ((_entries.Count > _maxEntries || (_maxBytes > 0 && _estimatedBytes > _maxBytes))
                   && _order.First is { } oldest)
            {
                var oldestKey = oldest.Value;
                var removed = _entries[oldestKey];
                _entries.Remove(oldestKey);
                _order.RemoveFirst();
                _estimatedBytes -= removed.Size;
            }

            if (_estimatedBytes < 0)
            {
                _estimatedBytes = 0;
            }
        }
    }

    private long Estimate(TValue value) => _sizeSelector?.Invoke(value) ?? 0L;

    private sealed class Entry(TValue value, long size, LinkedListNode<TKey> orderNode)
    {
        public TValue Value { get; set; } = value;

        public long Size { get; set; } = size;

        public LinkedListNode<TKey> OrderNode { get; } = orderNode;
    }
}
