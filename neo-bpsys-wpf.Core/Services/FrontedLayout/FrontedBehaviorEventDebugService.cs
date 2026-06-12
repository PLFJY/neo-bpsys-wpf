using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Thread-safe global recorder for all events published to <see cref="IFrontedEventBus" />.
/// </summary>
public sealed class FrontedBehaviorEventDebugService : IFrontedBehaviorEventDebugService
{
    private readonly IDisposable _subscription;
    private readonly object _gate = new();
    private readonly List<FrontedBehaviorEventDebugRecord> _records = [];
    private long _sequence;
    private bool _isEnabled = true;
    private bool _isPaused;
    private int _maxRecords = 300;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FrontedBehaviorEventDebugService" />.
    /// </summary>
    /// <param name="eventBus">Global fronted behavior event bus.</param>
    public FrontedBehaviorEventDebugService(IFrontedEventBus eventBus)
    {
        _subscription = eventBus.Subscribe(null, OnEventAsync);
    }

    /// <inheritdoc />
    public bool IsEnabled
    {
        get
        {
            lock (_gate)
            {
                return _isEnabled;
            }
        }
        set
        {
            lock (_gate)
            {
                _isEnabled = value;
            }
        }
    }

    /// <inheritdoc />
    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _isPaused;
            }
        }
        set
        {
            lock (_gate)
            {
                _isPaused = value;
            }
        }
    }

    /// <inheritdoc />
    public int MaxRecords
    {
        get
        {
            lock (_gate)
            {
                return _maxRecords;
            }
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            lock (_gate)
            {
                _maxRecords = value;
                TrimRecords();
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<FrontedBehaviorEventDebugRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return _records.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<FrontedBehaviorEventDebugRecord>? RecordAdded;

    /// <inheritdoc />
    public event EventHandler? RecordsCleared;

    /// <inheritdoc />
    public void Clear()
    {
        lock (_gate)
        {
            _records.Clear();
        }

        RecordsCleared?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscription.Dispose();
    }

    private Task OnEventAsync(FrontedBehaviorEvent behaviorEvent)
    {
        FrontedBehaviorEventDebugRecord? record;
        lock (_gate)
        {
            if (_disposed || !_isEnabled || _isPaused)
            {
                return Task.CompletedTask;
            }

            record = CreateRecord(behaviorEvent, ++_sequence);
            _records.Add(record);
            TrimRecords();
        }

        RecordAdded?.Invoke(this, record);
        return Task.CompletedTask;
    }

    private void TrimRecords()
    {
        var overflow = _records.Count - _maxRecords;
        if (overflow > 0)
        {
            _records.RemoveRange(0, overflow);
        }
    }

    private static FrontedBehaviorEventDebugRecord CreateRecord(FrontedBehaviorEvent behaviorEvent, long sequence)
    {
        return new FrontedBehaviorEventDebugRecord
        {
            Sequence = sequence,
            Timestamp = behaviorEvent.Timestamp,
            EventType = behaviorEvent.EventType,
            WindowId = behaviorEvent.WindowId,
            WindowType = behaviorEvent.WindowType,
            CanvasName = behaviorEvent.CanvasName,
            Source = behaviorEvent.Source,
            IsPreview = behaviorEvent.IsPreview,
            Payload = behaviorEvent.Payload
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair =>
                {
                    var formatted = FrontedBehaviorPayloadValueFormatter.Format(pair.Value);
                    return new FrontedBehaviorPayloadDebugEntry
                    {
                        Key = pair.Key,
                        TypeName = pair.Value?.GetType().Name ?? "null",
                        RawValue = pair.Value,
                        DisplayValue = formatted,
                        FilterText = formatted
                    };
                })
                .ToArray()
        };
    }
}
