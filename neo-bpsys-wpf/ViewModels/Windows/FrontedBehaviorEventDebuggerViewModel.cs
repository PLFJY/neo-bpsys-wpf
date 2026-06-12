using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// View model for the independent global behavior event debugger window.
/// </summary>
public sealed partial class FrontedBehaviorEventDebuggerViewModel : ViewModelBase, IDisposable
{
    private readonly IFrontedBehaviorEventDebugService _debugService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FrontedBehaviorEventDebuggerViewModel" />.
    /// </summary>
    /// <param name="debugService">Global behavior event debug service.</param>
    public FrontedBehaviorEventDebuggerViewModel(IFrontedBehaviorEventDebugService debugService)
    {
        _debugService = debugService;
        _isEnabled = debugService.IsEnabled;
        _isPaused = debugService.IsPaused;
        _maxRecords = debugService.MaxRecords;

        foreach (var record in debugService.Records)
        {
            Records.Add(new FrontedBehaviorEventDebugRecordViewModel(record));
        }

        RecordsView = CollectionViewSource.GetDefaultView(Records);
        RecordsView.Filter = FilterRecord;
        _debugService.RecordAdded += DebugService_OnRecordAdded;
        _debugService.RecordsCleared += DebugService_OnRecordsCleared;
    }

    /// <summary>
    /// Captured records shown by the debugger.
    /// </summary>
    public ObservableCollection<FrontedBehaviorEventDebugRecordViewModel> Records { get; } = [];

    /// <summary>
    /// Filtered record view for the event list.
    /// </summary>
    public ICollectionView RecordsView { get; }

    /// <summary>
    /// Selected event record in the event list.
    /// </summary>
    [ObservableProperty]
    private FrontedBehaviorEventDebugRecordViewModel? _selectedRecord;

    /// <summary>
    /// Selected payload entry for copy helper commands.
    /// </summary>
    [ObservableProperty]
    private FrontedBehaviorPayloadDebugEntry? _selectedPayloadEntry;

    /// <summary>
    /// Gets or sets whether the debugger records incoming behavior events.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>
    /// Gets or sets whether the debugger keeps existing records but ignores new events.
    /// </summary>
    [ObservableProperty]
    private bool _isPaused;

    /// <summary>
    /// Gets or sets the maximum number of records retained by the debugger.
    /// </summary>
    [ObservableProperty]
    private int _maxRecords;

    /// <summary>
    /// Gets or sets the free-text filter used by the event list.
    /// </summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>
    /// Gets or sets the event type filter.
    /// </summary>
    [ObservableProperty]
    private string _eventTypeFilter = string.Empty;

    /// <summary>
    /// Gets or sets the window type filter.
    /// </summary>
    [ObservableProperty]
    private string _windowTypeFilter = string.Empty;

    partial void OnIsEnabledChanged(bool value) => _debugService.IsEnabled = value;

    partial void OnIsPausedChanged(bool value) => _debugService.IsPaused = value;

    partial void OnMaxRecordsChanged(int value)
    {
        if (value < 1)
        {
            MaxRecords = 1;
            return;
        }

        _debugService.MaxRecords = value;
        SyncRecordsFromService();
    }

    partial void OnFilterTextChanged(string value) => RecordsView.Refresh();

    partial void OnEventTypeFilterChanged(string value) => RecordsView.Refresh();

    partial void OnWindowTypeFilterChanged(string value) => RecordsView.Refresh();

    /// <summary>
    /// Copies a payload path to the clipboard.
    /// </summary>
    /// <param name="entry">Payload entry to copy.</param>
    [RelayCommand]
    public void CopyPath(FrontedBehaviorPayloadDebugEntry? entry)
    {
        CopyText(entry?.Path);
    }

    /// <summary>
    /// Copies an Equals filter expression for a payload entry.
    /// </summary>
    /// <param name="entry">Payload entry to copy.</param>
    [RelayCommand]
    public void CopyEqualsFilter(FrontedBehaviorPayloadDebugEntry? entry)
    {
        CopyText(CreateEqualsFilter(entry));
    }

    /// <summary>
    /// Copies a Contains filter expression for a payload entry.
    /// </summary>
    /// <param name="entry">Payload entry to copy.</param>
    [RelayCommand]
    public void CopyContainsFilter(FrontedBehaviorPayloadDebugEntry? entry)
    {
        CopyText(CreateContainsFilter(entry));
    }

    /// <summary>
    /// Copies a payload filter value to the clipboard.
    /// </summary>
    /// <param name="entry">Payload entry to copy.</param>
    [RelayCommand]
    public void CopyValue(FrontedBehaviorPayloadDebugEntry? entry)
    {
        CopyText(entry?.FilterText);
    }

    /// <summary>
    /// Clears all captured event records.
    /// </summary>
    [RelayCommand]
    public void Clear()
    {
        _debugService.Clear();
    }

    /// <summary>
    /// Copies the selected event record as JSON.
    /// </summary>
    [RelayCommand]
    public void CopyEventJson()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        CopyText(JsonSerializer.Serialize(SelectedRecord.Record, _jsonOptions));
    }

    /// <summary>
    /// Exports all current event records as JSON.
    /// </summary>
    [RelayCommand]
    public void ExportJson()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON files|*.json|All files|*.*",
            FileName = $"behavior-events-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_debugService.Records, _jsonOptions));
    }

    /// <summary>
    /// Creates an Equals filter expression for a payload entry.
    /// </summary>
    /// <param name="entry">Payload entry.</param>
    /// <returns>Filter expression, or an empty string when no entry is supplied.</returns>
    public static string CreateEqualsFilter(FrontedBehaviorPayloadDebugEntry? entry) =>
        entry is null ? string.Empty : $"{entry.Path} Equals {entry.FilterText}";

    /// <summary>
    /// Creates a Contains filter expression for a payload entry.
    /// </summary>
    /// <param name="entry">Payload entry.</param>
    /// <returns>Filter expression, or an empty string when no entry is supplied.</returns>
    public static string CreateContainsFilter(FrontedBehaviorPayloadDebugEntry? entry)
    {
        if (entry is null)
        {
            return string.Empty;
        }

        var value = entry.FilterText;
        if (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal))
        {
            value = value[1..^1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? value;
        }

        return $"{entry.Path} Contains {value}";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debugService.RecordAdded -= DebugService_OnRecordAdded;
        _debugService.RecordsCleared -= DebugService_OnRecordsCleared;
    }

    private bool FilterRecord(object item)
    {
        if (item is not FrontedBehaviorEventDebugRecordViewModel record)
        {
            return false;
        }

        return Contains(record.EventType, EventTypeFilter)
               && Contains(record.WindowType ?? string.Empty, WindowTypeFilter)
               && (string.IsNullOrWhiteSpace(FilterText)
                   || Contains(record.EventType, FilterText)
                   || Contains(record.WindowType ?? string.Empty, FilterText)
                   || Contains(record.WindowId ?? string.Empty, FilterText)
                   || Contains(record.CanvasName ?? string.Empty, FilterText)
                   || Contains(record.Source ?? string.Empty, FilterText)
                   || Contains(record.PayloadSummary, FilterText));
    }

    private static bool Contains(string text, string filter) =>
        string.IsNullOrWhiteSpace(filter) || text.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private void DebugService_OnRecordAdded(object? sender, FrontedBehaviorEventDebugRecord record)
    {
        RunOnUiThread(() =>
        {
            Records.Add(new FrontedBehaviorEventDebugRecordViewModel(record));
            while (Records.Count > _debugService.MaxRecords)
            {
                Records.RemoveAt(0);
            }

            RecordsView.Refresh();
        });
    }

    private void DebugService_OnRecordsCleared(object? sender, EventArgs e)
    {
        RunOnUiThread(() =>
        {
            Records.Clear();
            SelectedRecord = null;
        });
    }

    private void SyncRecordsFromService()
    {
        RunOnUiThread(() =>
        {
            Records.Clear();
            foreach (var record in _debugService.Records)
            {
                Records.Add(new FrontedBehaviorEventDebugRecordViewModel(record));
            }

            RecordsView.Refresh();
        });
    }

    private static void CopyText(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
        }
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}

/// <summary>
/// UI wrapper for a captured behavior event record.
/// </summary>
public sealed class FrontedBehaviorEventDebugRecordViewModel
{
    /// <summary>
    /// Initializes a new instance of <see cref="FrontedBehaviorEventDebugRecordViewModel" />.
    /// </summary>
    /// <param name="record">Captured debug record.</param>
    public FrontedBehaviorEventDebugRecordViewModel(FrontedBehaviorEventDebugRecord record)
    {
        Record = record;
        PayloadSummary = BuildPayloadSummary(record.Payload);
    }

    /// <summary>
    /// Source debug record.
    /// </summary>
    public FrontedBehaviorEventDebugRecord Record { get; }

    /// <summary>
    /// Monotonic sequence number.
    /// </summary>
    public long Sequence => Record.Sequence;

    /// <summary>
    /// Local display time.
    /// </summary>
    public string TimeText => Record.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

    /// <summary>
    /// Behavior event type.
    /// </summary>
    public string EventType => Record.EventType;

    /// <summary>
    /// Optional runtime window identifier.
    /// </summary>
    public string? WindowId => Record.WindowId;

    /// <summary>
    /// Optional window type name.
    /// </summary>
    public string? WindowType => Record.WindowType;

    /// <summary>
    /// Optional canvas name.
    /// </summary>
    public string? CanvasName => Record.CanvasName;

    /// <summary>
    /// Optional event source name.
    /// </summary>
    public string? Source => Record.Source;

    /// <summary>
    /// Whether this event came from Designer preview.
    /// </summary>
    public bool IsPreview => Record.IsPreview;

    /// <summary>
    /// Payload entries.
    /// </summary>
    public IReadOnlyList<FrontedBehaviorPayloadDebugEntry> Payload => Record.Payload;

    /// <summary>
    /// Compact event payload summary.
    /// </summary>
    public string PayloadSummary { get; }

    private static string BuildPayloadSummary(IReadOnlyList<FrontedBehaviorPayloadDebugEntry> payload)
    {
        const int maxLength = 160;
        var text = string.Join(", ", payload.Select(entry => $"{entry.Key}={entry.FilterText}"));
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
