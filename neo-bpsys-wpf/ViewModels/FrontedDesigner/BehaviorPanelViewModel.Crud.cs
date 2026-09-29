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

/// <summary>
/// 行为面板的Crud逻辑。
/// </summary>
public sealed partial class BehaviorPanelViewModel
{
    public bool RemoveBehaviors(Guid behaviorGuid)
    {
        var existing = CurrentDocument.FindSet(behaviorGuid);
        if (existing is null)
        {
            return false;
        }

        CaptureUndoSnapshot();
        var removed = CurrentDocument.RemoveSet(behaviorGuid);
        if (!removed)
        {
            return false;
        }

        if (SelectedControl?.Config.BehaviorGuid == behaviorGuid)
        {
            RefreshForSelectedControl();
        }

        MarkBehaviorsDirty();
        return true;
    }

    /// <summary>
    /// 向选中控件添加一次性行为。
    /// </summary>
    [RelayCommand]
    public void AddOneShotBehavior()
    {
        if (SelectedControl is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var set = GetOrCreateSelectedSet();
        if (set is null)
        {
            return;
        }

        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.OneShot,
            Name = Localize("Designer.Behaviors.NewOneShot", "New OneShot Behavior"),
            Enabled = true,
            Trigger = new TriggerDescriptor { EventType = EventBusEventOptions.FirstOrDefault()?.EventType ?? string.Empty },
            Graph = new FrontedNodeGraph()
        };
        set.Behaviors.Add(behavior);
        RefreshFromSet(set, behavior);
        MarkBehaviorsDirty();
    }

    /// <summary>
    /// 向选中控件添加包含启动、循环和停止图的循环行为。
    /// </summary>
    [RelayCommand]
    public void AddLoopBehavior()
    {
        if (SelectedControl is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var set = GetOrCreateSelectedSet();
        if (set is null)
        {
            return;
        }

        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Loop,
            Name = Localize("Designer.Behaviors.NewLoop", "New Loop Behavior"),
            Enabled = true,
            StartTrigger = new TriggerDescriptor { EventType = EventBusEventOptions.FirstOrDefault()?.EventType ?? string.Empty },
            StopTriggers = [new TriggerDescriptor { EventType = EventBusEventOptions.FirstOrDefault()?.EventType ?? string.Empty }],
            StartGraph = new FrontedNodeGraph(),
            LoopGraph = new FrontedNodeGraph(),
            StopGraph = new FrontedNodeGraph(),
            LoopPolicy = new FrontedLoopPolicy()
        };
        set.Behaviors.Add(behavior);
        RefreshFromSet(set, behavior);
        MarkBehaviorsDirty();
    }

    /// <summary>
    /// 向选中控件添加转场行为。
    /// </summary>
    [RelayCommand]
    public void AddTransitionBehavior()
    {
        if (SelectedControl is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var set = GetOrCreateSelectedSet();
        if (set is null)
        {
            return;
        }

        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Transition,
            Name = Localize("Designer.Behaviors.NewTransition", "New Transition Behavior"),
            Enabled = true,
            TransitionTrigger = new TriggerDescriptor
            {
                EventType = TransitionEventOptions.FirstOrDefault(option => option.EventType == "Selection.CharacterPick")?.EventType
                            ?? TransitionEventOptions.FirstOrDefault()?.EventType
                            ?? string.Empty
            },
            ExitGraph = new FrontedNodeGraph(),
            EnterGraph = new FrontedNodeGraph(),
            ReentryPolicy = FrontedReentryPolicy.InterruptPrevious
        };
        set.Behaviors.Add(behavior);
        RefreshFromSet(set, behavior);
        MarkBehaviorsDirty();
    }

    /// <summary>
    /// 在面板中选择一个行为行。
    /// </summary>
    /// <param name="behavior">要选中的行为行。</param>
    [RelayCommand]
    public void SelectBehavior(BehaviorEditorViewModel? behavior)
    {
        SelectedBehavior = behavior;
    }

    /// <summary>
    /// 从当前控件行为集合中删除行为。
    /// </summary>
    /// <param name="behavior">要删除的行为行。</param>
    [RelayCommand]
    public void DeleteBehavior(BehaviorEditorViewModel? behavior)
    {
        if (behavior is null || _currentSet is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        if (!_currentSet.Behaviors.Remove(behavior.Model))
        {
            return;
        }

        if (_currentSet.Behaviors.Count == 0)
        {
            CurrentDocument.RemoveSet(_currentSet.BehaviorGuid);
            _currentSet = null;
            Behaviors.Clear();
            SelectedBehavior = null;
        }
        else
        {
            RefreshFromSet(_currentSet, _currentSet.Behaviors.FirstOrDefault());
        }

        MarkBehaviorsDirty();
    }

    /// <summary>
    /// 复制行为，并重新生成所有行为和图节点 ID。
    /// </summary>
    /// <param name="behavior">要复制的行为行。</param>
    [RelayCommand]
    public void DuplicateBehavior(BehaviorEditorViewModel? behavior)
    {
        if (behavior is null || _currentSet is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var json = JsonSerializer.Serialize(behavior.Model, _cloneJsonOptions);
        var clone = JsonSerializer.Deserialize<FrontedBehavior>(json, _cloneJsonOptions);
        if (clone is null)
        {
            return;
        }

        clone.BehaviorId = FrontedBehaviorGuidHelper.NewGuid();
        clone.Name = string.Format(
            Localize("Designer.Behaviors.CopyOfFormat", "Copy of {0}"),
            string.IsNullOrWhiteSpace(clone.Name) ? behavior.Name : clone.Name);
        RegenerateGraphIds(clone.Graph);
        RegenerateGraphIds(clone.StartGraph);
        RegenerateGraphIds(clone.LoopGraph);
        RegenerateGraphIds(clone.StopGraph);
        RegenerateGraphIds(clone.ExitGraph);
        RegenerateGraphIds(clone.EnterGraph);

        var index = _currentSet.Behaviors.IndexOf(behavior.Model);
        _currentSet.Behaviors.Insert(index + 1, clone);
        RefreshFromSet(_currentSet, clone);
        MarkBehaviorsDirty();
    }

    /// <summary>
    /// 将行为复制到共享设计器行为剪贴板。
    /// </summary>
    /// <param name="behavior">要复制到剪贴板的行为行。</param>
}
