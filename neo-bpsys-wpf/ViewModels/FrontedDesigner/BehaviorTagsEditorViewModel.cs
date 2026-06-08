using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner;

/// <summary>
/// Edits behavior trigger tags stored on a fronted control config.
/// </summary>
public sealed partial class BehaviorTagsEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;
    private FrontedControlConfigBase? _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="BehaviorTagsEditorViewModel"/> class.
    /// </summary>
    /// <param name="markDirty">Callback used to mark the layout dirty.</param>
    public BehaviorTagsEditorViewModel(Action markDirty)
    {
        _markDirty = markDirty;
    }

    /// <summary>
    /// Gets editable behavior tag rows.
    /// </summary>
    public ObservableCollection<BehaviorTagRowViewModel> Tags { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the selected control has any behavior tags.
    /// </summary>
    public bool HasTags => Tags.Count > 0;

    /// <summary>
    /// Sets the selected control config whose tags should be edited.
    /// </summary>
    /// <param name="config">The selected control config, or null when no control is selected.</param>
    public void SetConfig(FrontedControlConfigBase? config)
    {
        _config = config;
        Tags.Clear();
        if (_config is not null)
        {
            foreach (var pair in _config.BehaviorTags.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                Tags.Add(new BehaviorTagRowViewModel(pair.Key, pair.Value, OnRowChanged));
            }
        }

        OnPropertyChanged(nameof(HasTags));
    }

    /// <summary>
    /// Adds a new behavior tag row.
    /// </summary>
    [RelayCommand]
    public void AddTag()
    {
        if (_config is null)
        {
            return;
        }

        var key = CreateDefaultKey();
        _config.BehaviorTags[key] = string.Empty;
        Tags.Add(new BehaviorTagRowViewModel(key, string.Empty, OnRowChanged));
        MarkDirty();
    }

    /// <summary>
    /// Removes a behavior tag row.
    /// </summary>
    /// <param name="row">The row to remove.</param>
    [RelayCommand]
    public void RemoveTag(BehaviorTagRowViewModel? row)
    {
        if (_config is null || row is null)
        {
            return;
        }

        Tags.Remove(row);
        RebuildConfigTags();
        MarkDirty();
    }

    private void OnRowChanged()
    {
        RebuildConfigTags();
        MarkDirty();
    }

    private void RebuildConfigTags()
    {
        if (_config is null)
        {
            return;
        }

        _config.BehaviorTags.Clear();
        foreach (var row in Tags)
        {
            if (string.IsNullOrWhiteSpace(row.Key))
            {
                continue;
            }

            _config.BehaviorTags[row.Key.Trim()] = row.Value ?? string.Empty;
        }
    }

    private string CreateDefaultKey()
    {
        var index = Tags.Count + 1;
        while (_config?.BehaviorTags.ContainsKey($"Tag{index}") == true)
        {
            index++;
        }

        return $"Tag{index}";
    }

    private void MarkDirty()
    {
        _markDirty();
        OnPropertyChanged(nameof(HasTags));
    }
}

/// <summary>
/// Editable behavior tag row.
/// </summary>
public sealed partial class BehaviorTagRowViewModel : ObservableObject
{
    private readonly Action _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BehaviorTagRowViewModel"/> class.
    /// </summary>
    /// <param name="key">Initial tag key.</param>
    /// <param name="value">Initial tag value.</param>
    /// <param name="changed">Callback invoked after edits.</param>
    public BehaviorTagRowViewModel(string key, string? value, Action changed)
    {
        _key = key;
        _value = value ?? string.Empty;
        _changed = changed;
    }

    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string? _value;

    partial void OnKeyChanged(string value) => _changed();

    partial void OnValueChanged(string? value) => _changed();
}
