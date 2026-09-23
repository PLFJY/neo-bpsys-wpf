using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;
using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner;

public sealed partial class TriggerDescriptorEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;
    private readonly Action _captureUndoSnapshot;
    private readonly Func<string, string, string> _localize;

    public TriggerDescriptorEditorViewModel(
        TriggerDescriptor? model,
        IReadOnlyList<BehaviorEventOptionViewModel> eventOptions,
        IReadOnlyList<BehaviorOptionViewModel> operatorOptions,
        Action markDirty,
        Func<string, string, string> localize,
        Action? captureUndoSnapshot = null)
    {
        Model = model ?? new TriggerDescriptor { EventType = eventOptions.FirstOrDefault()?.EventType ?? string.Empty };
        var resolvedEventOptions = eventOptions.ToList();
        if (!string.IsNullOrWhiteSpace(Model.EventType)
            && resolvedEventOptions.All(option => !string.Equals(option.EventType, Model.EventType, StringComparison.Ordinal)))
        {
            resolvedEventOptions.Add(CreateMissingEventOption(Model.EventType, localize));
        }

        EventOptions = resolvedEventOptions;
        OperatorOptions = operatorOptions;
        _markDirty = markDirty;
        _captureUndoSnapshot = captureUndoSnapshot ?? (() => { });
        _localize = localize;
        foreach (var filter in Model.Filters)
        {
            Filters.Add(new TriggerFilterEditorViewModel(filter, operatorOptions, markDirty, localize, captureUndoSnapshot));
        }
        UpdateSelectedEvent(localize);
    }

    private static BehaviorEventOptionViewModel CreateMissingEventOption(
        string eventType,
        Func<string, string, string> localize)
    {
        var identity = eventType;
        if (FrontedBehaviorEventIdValidator.TryParseCanonicalEventType(eventType, out var packageId, out var localEventId))
        {
            identity = $"{packageId} / {localEventId}";
        }

        return new BehaviorEventOptionViewModel(
            eventType,
            string.Empty,
            string.Empty,
            string.Empty,
            identity,
            localize("Designer.Behaviors.MissingPluginEvent", "⚠ Missing plugin event"),
            eventType,
            FrontedBehaviorEventUsage.All,
            [],
            localize,
            isMissing: true);
    }

    public TriggerDescriptor Model { get; }

    public IReadOnlyList<BehaviorEventOptionViewModel> EventOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> OperatorOptions { get; }

    public ObservableCollection<TriggerFilterEditorViewModel> Filters { get; } = [];

    public BehaviorEventOptionViewModel? SelectedEventDescriptor { get; private set; }

    public IReadOnlyList<BehaviorPayloadFieldOptionViewModel> PayloadFieldOptions { get; private set; } = [];

    public bool HasPayloadFields => PayloadFieldOptions.Count > 0;

    public string EventType
    {
        get => Model.EventType;
        set
        {
            if (string.Equals(Model.EventType, value, StringComparison.Ordinal))
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.EventType, value, Model, static (model, next) => model.EventType = next))
            {
                ClearFilters();
                _markDirty();
                UpdateSelectedEvent(_localize);
            }
        }
    }

    [RelayCommand]
    public void AddFilter()
    {
        _captureUndoSnapshot();
        var filter = new TriggerFilter
        {
            Left = PayloadFieldOptions.FirstOrDefault(field => field.IsCommonFilterTarget)?.Path
                   ?? PayloadFieldOptions.FirstOrDefault()?.Path
                   ?? string.Empty,
            Operator = TriggerFilterOperator.Equals
        };
        Model.Filters.Add(filter);
        var filterVm = new TriggerFilterEditorViewModel(filter, OperatorOptions, _markDirty, _localize, _captureUndoSnapshot);
        filterVm.SetPayloadFieldOptions(PayloadFieldOptions);
        Filters.Add(filterVm);
        _markDirty();
    }

    [RelayCommand]
    public void RemoveFilter(TriggerFilterEditorViewModel? filter)
    {
        if (filter is null)
        {
            return;
        }

        if (!Model.Filters.Contains(filter.Model))
        {
            return;
        }

        _captureUndoSnapshot();
        if (!Model.Filters.Remove(filter.Model))
        {
            return;
        }

        Filters.Remove(filter);
        _markDirty();
    }

    private void ClearFilters()
    {
        Model.Filters.Clear();
        Filters.Clear();
    }

    private void UpdateSelectedEvent(Func<string, string, string> localize)
    {
        SelectedEventDescriptor = EventOptions.FirstOrDefault(option =>
            string.Equals(option.EventType, Model.EventType, StringComparison.Ordinal));
        var options = SelectedEventDescriptor?.PayloadFields.ToList() ?? [];
        foreach (var path in Model.Filters.Select(filter => filter.Left).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct())
        {
            if (options.All(option => !string.Equals(option.Path, path, StringComparison.Ordinal)))
            {
                options.Add(new BehaviorPayloadFieldOptionViewModel(
                    path,
                    "Designer.Behaviors.UnknownParameterFormat",
                    string.Empty,
                    path,
                    path,
                    path,
                    [],
                    true,
                    false,
                    localize));
            }
        }

        PayloadFieldOptions = options;
        foreach (var filter in Filters)
        {
            filter.SetPayloadFieldOptions(options);
        }
        OnPropertyChanged(nameof(SelectedEventDescriptor));
        OnPropertyChanged(nameof(PayloadFieldOptions));
        OnPropertyChanged(nameof(HasPayloadFields));
    }

    /// <summary>
    /// 刷新所有本地化显示字符串以支持热切换语言。
    /// </summary>
    public void RefreshLocalization()
    {
        foreach (var option in PayloadFieldOptions)
        {
            option.Refresh();
        }

        foreach (var filter in Filters)
        {
            filter.RefreshLocalization();
        }

        OnPropertyChanged(nameof(HasPayloadFields));
    }
}
