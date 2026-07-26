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
/// 独立全局行为事件调试器窗口的视图模型。
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
    /// 初始化 <see cref="FrontedBehaviorEventDebuggerViewModel" /> 的新实例。
    /// </summary>
    /// <param name="debugService">全局行为事件调试服务。</param>
    public FrontedBehaviorEventDebuggerViewModel(IFrontedBehaviorEventDebugService debugService)
    {
        _debugService = debugService;
        RecordsView = CollectionViewSource.GetDefaultView(Records);
        RecordsView.Filter = FilterRecord;

        IsEnabled = debugService.IsEnabled;
        IsPaused = debugService.IsPaused;
        MaxRecords = debugService.MaxRecords;

        _debugService.RecordAdded += DebugService_OnRecordAdded;
        _debugService.RecordsCleared += DebugService_OnRecordsCleared;
    }

    /// <summary>
    /// 调试器显示的已捕获记录。
    /// </summary>
    public ObservableCollection<FrontedBehaviorEventDebugRecordViewModel> Records { get; } = [];

    /// <summary>
    /// 事件列表的已筛选记录视图。
    /// </summary>
    public ICollectionView RecordsView { get; }

    /// <summary>
    /// 事件列表中选中的事件记录。
    /// </summary>
    [ObservableProperty]
    public partial FrontedBehaviorEventDebugRecordViewModel? SelectedRecord { get; set; }

    /// <summary>
    /// 当前选中的、用于复制辅助命令的负载条目。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyPathCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyConditionPathCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyEqualsFilterCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyIfConditionCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyContainsFilterCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyValueCommand))]
    public partial FrontedBehaviorPayloadDebugEntry? SelectedPayloadEntry { get; set; }

    /// <summary>
    /// 获取一个值，指示当前是否选中了可复制的负载条目。
    /// </summary>
    public bool HasSelectedPayloadEntry => SelectedPayloadEntry is not null;

    /// <summary>
    /// 获取或设置调试器是否记录传入的行为事件。
    /// </summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    /// <summary>
    /// 获取或设置调试器是否保留已有记录但忽略新事件。
    /// </summary>
    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    /// <summary>
    /// 获取或设置调试器保留的最大记录数。
    /// </summary>
    [ObservableProperty]
    public partial int MaxRecords { get; set; }

    /// <summary>
    /// 获取或设置事件列表使用的自由文本筛选条件。
    /// </summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置事件类型筛选条件。
    /// </summary>
    [ObservableProperty]
    public partial string EventTypeFilter { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置窗口类型筛选条件。
    /// </summary>
    [ObservableProperty]
    public partial string WindowTypeFilter { get; set; } = string.Empty;

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
    /// 将负载路径复制到剪贴板。
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedPayloadEntry))]
    public void CopyPath()
    {
        CopyText(SelectedPayloadEntry?.Path);
    }

    /// <summary>
    /// 复制与图条件节点兼容的负载路径。
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedPayloadEntry))]
    public void CopyConditionPath()
    {
        CopyText(SelectedPayloadEntry?.Path);
    }

    /// <summary>
    /// 复制负载条目的 Equals 筛选表达式。
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedPayloadEntry))]
    public void CopyEqualsFilter()
    {
        CopyText(CreateEqualsFilter(SelectedPayloadEntry));
    }

    /// <summary>
    /// 复制负载条目的 IF 条件表达式。
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedPayloadEntry))]
    public void CopyIfCondition()
    {
        CopyText(CreateIfCondition(SelectedPayloadEntry));
    }

    /// <summary>
    /// 复制负载条目的 Contains 筛选表达式。
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedPayloadEntry))]
    public void CopyContainsFilter()
    {
        CopyText(CreateContainsFilter(SelectedPayloadEntry));
    }

    /// <summary>
    /// 将负载筛选值复制到剪贴板。
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedPayloadEntry))]
    public void CopyValue()
    {
        CopyText(SelectedPayloadEntry?.FilterText);
    }

    /// <summary>
    /// 清除所有已捕获的事件记录。
    /// </summary>
    [RelayCommand]
    public void Clear()
    {
        _debugService.Clear();
    }

    /// <summary>
    /// 将选中的事件记录以 JSON 格式复制。
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
    /// 将当前所有事件记录导出为 JSON。
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
    /// 为负载条目创建 Equals 筛选表达式。
    /// </summary>
    /// <param name="entry">负载条目。</param>
    /// <returns>筛选表达式；未提供条目时返回空字符串。</returns>
    public static string CreateEqualsFilter(FrontedBehaviorPayloadDebugEntry? entry) =>
        entry is null ? string.Empty : $"{entry.Path} Equals {entry.FilterText}";

    /// <summary>
    /// 为负载条目创建与条件节点兼容的 IF 表达式。
    /// </summary>
    /// <param name="entry">负载条目。</param>
    /// <returns>IF 条件表达式；未提供条目时返回空字符串。</returns>
    public static string CreateIfCondition(FrontedBehaviorPayloadDebugEntry? entry) =>
        CreateEqualsFilter(entry);

    /// <summary>
    /// 为负载条目创建 Contains 筛选表达式。
    /// </summary>
    /// <param name="entry">负载条目。</param>
    /// <returns>筛选表达式；未提供条目时返回空字符串。</returns>
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
/// 已捕获行为事件记录的 UI 包装器。
/// </summary>
public sealed class FrontedBehaviorEventDebugRecordViewModel
{
    /// <summary>
    /// 初始化 <see cref="FrontedBehaviorEventDebugRecordViewModel" /> 的新实例。
    /// </summary>
    /// <param name="record">已捕获的调试记录。</param>
    public FrontedBehaviorEventDebugRecordViewModel(FrontedBehaviorEventDebugRecord record)
    {
        Record = record;
        PayloadSummary = BuildPayloadSummary(record.Payload);
    }

    /// <summary>
    /// 源调试记录。
    /// </summary>
    public FrontedBehaviorEventDebugRecord Record { get; }

    /// <summary>
    /// 单调递增的序列号。
    /// </summary>
    public long Sequence => Record.Sequence;

    /// <summary>
    /// 本地显示时间。
    /// </summary>
    public string TimeText => Record.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

    /// <summary>
    /// 行为事件类型。
    /// </summary>
    public string EventType => Record.EventType;

    /// <summary>
    /// 可选的运行时窗口标识符。
    /// </summary>
    public string? WindowId => Record.WindowId;

    /// <summary>
    /// 可选的窗口类型名称。
    /// </summary>
    public string? WindowType => Record.WindowType;

    /// <summary>
    /// 可选的 Canvas 名称。
    /// </summary>
    public string? CanvasName => Record.CanvasName;

    /// <summary>
    /// 可选的事件源名称。
    /// </summary>
    public string? Source => Record.Source;

    /// <summary>
    /// 此事件是否来自设计器预览。
    /// </summary>
    public bool IsPreview => Record.IsPreview;

    /// <summary>
    /// 负载条目。
    /// </summary>
    public IReadOnlyList<FrontedBehaviorPayloadDebugEntry> Payload => Record.Payload;

    /// <summary>
    /// 紧凑的事件负载摘要。
    /// </summary>
    public string PayloadSummary { get; }

    private static string BuildPayloadSummary(IReadOnlyList<FrontedBehaviorPayloadDebugEntry> payload)
    {
        const int maxLength = 160;
        var text = string.Join(", ", payload.Select(entry => $"{entry.Key}={entry.FilterText}"));
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
