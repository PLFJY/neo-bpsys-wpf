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

public sealed partial class LoopPolicyEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;
    private readonly Action _captureUndoSnapshot;

    public LoopPolicyEditorViewModel(
        FrontedLoopPolicy model,
        IReadOnlyList<BehaviorOptionViewModel> stopModeOptions,
        IReadOnlyList<BehaviorOptionViewModel> reentryPolicyOptions,
        Action markDirty,
        Action captureUndoSnapshot)
    {
        Model = model;
        StopModeOptions = stopModeOptions;
        ReentryPolicyOptions = reentryPolicyOptions;
        _markDirty = markDirty;
        _captureUndoSnapshot = captureUndoSnapshot;
    }

    public FrontedLoopPolicy Model { get; }

    public IReadOnlyList<BehaviorOptionViewModel> StopModeOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> ReentryPolicyOptions { get; }

    public int RepeatCount
    {
        get => Model.RepeatCount;
        set
        {
            if (Model.RepeatCount == value)
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.RepeatCount, value, Model, static (model, next) => model.RepeatCount = next))
            {
                _markDirty();
            }
        }
    }

    public bool AutoReverse
    {
        get => Model.AutoReverse;
        set
        {
            if (Model.AutoReverse == value)
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.AutoReverse, value, Model, static (model, next) => model.AutoReverse = next))
            {
                _markDirty();
            }
        }
    }

    public int IntervalMs
    {
        get => Model.IntervalMs;
        set
        {
            if (Model.IntervalMs == value)
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.IntervalMs, value, Model, static (model, next) => model.IntervalMs = next))
            {
                _markDirty();
            }
        }
    }

    public FrontedLoopStopMode StopMode
    {
        get => Model.StopMode;
        set
        {
            if (Model.StopMode == value)
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.StopMode, value, Model, static (model, next) => model.StopMode = next))
            {
                _markDirty();
            }
        }
    }

    public bool ResetOnStop
    {
        get => Model.ResetOnStop;
        set
        {
            if (Model.ResetOnStop == value)
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.ResetOnStop, value, Model, static (model, next) => model.ResetOnStop = next))
            {
                _markDirty();
            }
        }
    }

    public FrontedReentryPolicy ReentryPolicy
    {
        get => Model.ReentryPolicy;
        set
        {
            if (Model.ReentryPolicy == value)
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.ReentryPolicy, value, Model, static (model, next) => model.ReentryPolicy = next))
            {
                _markDirty();
            }
        }
    }
}
