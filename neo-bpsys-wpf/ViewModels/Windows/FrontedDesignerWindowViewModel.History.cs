using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// Fronted Designer 的撤销、重做与快照恢复业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    /// <summary>
    /// 恢复上一个 Designer 撤销快照。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (CurrentDocument is null || _undoStack.Count == 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotUndo");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Undo", "start");
        var currentSnapshot = CreateSnapshot();
        LogDesignerPerf("Undo", "create current snapshot", Elapsed(total));
        if (currentSnapshot is not null)
        {
            PushRedoSnapshot(currentSnapshot);
        }

        RestoreSnapshot(
            _undoStack.Pop(),
            FrontedDesignerSnapshotRestoreMode.PreferGeometryFastPathThenScheduledAtomicPreview,
            "Undo");
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Undo");
        LogDesignerPerf("Undo", "total", Elapsed(total));
    }

    /// <summary>
    /// 恢复下一个 Designer 重做快照。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (CurrentDocument is null || _redoStack.Count == 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotRedo");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Redo", "start");
        var currentSnapshot = CreateSnapshot();
        LogDesignerPerf("Redo", "create current snapshot", Elapsed(total));
        if (currentSnapshot is not null)
        {
            PushUndoSnapshot(currentSnapshot);
        }

        RestoreSnapshot(
            _redoStack.Pop(),
            FrontedDesignerSnapshotRestoreMode.PreferGeometryFastPathThenScheduledAtomicPreview,
            "Redo");
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Redo");
        LogDesignerPerf("Redo", "total", Elapsed(total));
    }
    public void CaptureUndoSnapshot()
    {
        var snapshot = CreateSnapshot();
        if (snapshot is null)
        {
            return;
        }

        if (_undoStack.TryPeek(out var previous) && previous == snapshot)
        {
            return;
        }

        PushUndoSnapshot(snapshot);
        _redoStack.Clear();
        NotifyUndoRedoCommands();
    }

    private static void PushLimited(Stack<string> stack, string snapshot)
    {
        stack.Push(snapshot);
        if (stack.Count <= FrontedLayoutLimits.MaxDesignerUndoSnapshots)
        {
            return;
        }

        var retained = stack
            .Take(FrontedLayoutLimits.MaxDesignerUndoSnapshots)
            .Reverse()
            .ToList();

        stack.Clear();
        foreach (var item in retained)
        {
            stack.Push(item);
        }
    }

    private void PushUndoSnapshot(string snapshot)
    {
        PushLimited(_undoStack, snapshot);
    }

    private void PushRedoSnapshot(string snapshot)
    {
        PushLimited(_redoStack, snapshot);
    }
    private string? CreateSnapshot()
    {
        if (CurrentDocument is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(new FrontedDesignerUndoSnapshot
        {
            CanvasConfig = _designConverter.ToConfig(CurrentDocument),
            BehaviorDocument = BehaviorPanel.CurrentDocument
        });
    }

    private static bool CanCopyControl(FrontedControlDesignItem? item)
    {
        return item is
        {
            IsSelectableInEditor: true,
            IsEditableInEditor: true,
        };
    }

    private static string GeneratePasteName(string sourceName, string controlType, FrontedCanvasDesignDocument document)
    {
        var existingNames = document.Controls.Select(control => control.Name).ToHashSet(StringComparer.Ordinal);
        if (!existingNames.Contains(sourceName) && ValidControlNameRegex.IsMatch(sourceName))
        {
            return sourceName;
        }

        var match = Regex.Match(sourceName, "^(.*?)(\\d+)$", RegexOptions.CultureInvariant);
        var baseName = match.Success ? match.Groups[1].Value : sourceName;
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = GetNameSeed(controlType);
        }

        var index = match.Success && int.TryParse(match.Groups[2].Value, out var parsed) ? parsed + 1 : 1;
        var separator = match.Success ? string.Empty : "_";

        while (true)
        {
            var suffix = $"{separator}{index}";
            var truncatedBaseName = FrontedTextLimitHelper.Clamp(
                baseName,
                Math.Max(1, FrontedLayoutLimits.MaxControlNameLength - suffix.Length));
            var candidate = $"{truncatedBaseName}{suffix}";
            if (!existingNames.Contains(candidate) && ValidControlNameRegex.IsMatch(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static string GetNameSeed(string controlType)
    {
        return FrontedPluginControlType.TryParse(controlType, out var parsed)
            ? parsed.ControlTypeName
            : controlType;
    }

    private static object? ClampEditorPropertyValue(
        string propertyName,
        string? controlType,
        object? newValue,
        out bool wasClamped)
    {
        wasClamped = false;
        if (newValue is not string text)
        {
            return newValue;
        }

        var maxLength = FrontedTextLimitHelper.GetMaxLengthForProperty(propertyName, controlType);
        if (maxLength == int.MaxValue)
        {
            return newValue;
        }

        var clamped = FrontedTextLimitHelper.Clamp(text, maxLength);
        wasClamped = !string.Equals(text, clamped, StringComparison.Ordinal);
        return clamped;
    }

    private void RestoreSnapshot(
        string snapshot,
        FrontedDesignerSnapshotRestoreMode mode,
        string traceOperation = "RestoreSnapshot")
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var total = StartDesignerPerfTrace();
        var selectedName = SelectedDesignItem?.Name;
        var windowTypeName = CurrentDocument.WindowTypeName;
        var canvasName = CurrentDocument.CanvasName;
        var restoreSnapshot = DeserializeUndoSnapshot(snapshot);
        var config = restoreSnapshot?.CanvasConfig;
        var behaviorDocument = restoreSnapshot?.BehaviorDocument;
        LogDesignerPerf(traceOperation, "restore snapshot deserialize", Elapsed(total));
        if (config is null)
        {
            return;
        }

        var behaviorDocumentChanged = behaviorDocument is not null
            && !BehaviorDocumentsEqual(BehaviorPanel.CurrentDocument, behaviorDocument);
        if (mode == FrontedDesignerSnapshotRestoreMode.PreferGeometryFastPathThenScheduledAtomicPreview
            && !behaviorDocumentChanged
            && TryRestoreGeometryOnlySnapshot(config, traceOperation, total))
        {
            LogDesignerPerf(traceOperation, "total", Elapsed(total));
            return;
        }

        var shouldNotifyUndoRedoInFinally = true;
        SetIsRestoringSnapshotVisuals(true);
        try
        {
            var document = _designConverter.FromConfig(
                windowTypeName,
                canvasName,
                config);
            LogDesignerPerf(traceOperation, "design document rebuild", Elapsed(total));
            document.IsDirty = true;
            CurrentDocument = document;
            SelectDesignItem(document.Controls.FirstOrDefault(control =>
                string.Equals(control.Name, selectedName, StringComparison.Ordinal)));
            NormalizeSelectionState();
            RestoreBehaviorDocumentSnapshot(behaviorDocument, windowTypeName, canvasName);

            switch (mode)
            {
                case FrontedDesignerSnapshotRestoreMode.PreferGeometryFastPathThenScheduledAtomicPreview:
                    _clearRestoreVisualsAfterScheduledPreview = true;
                    shouldNotifyUndoRedoInFinally = false;
                    ScheduleValidationAndPreviewRender(traceOperation);
                    LogDesignerPerf(traceOperation, "scheduled full restore", Elapsed(total));
                    break;

                case FrontedDesignerSnapshotRestoreMode.ImmediatePreviewThenScheduledValidation:
                    RequestPreviewRender(config, _selectedCatalogEntry);
                    LogDesignerPerf(traceOperation, "preview render execution", Elapsed(total));
                    SetIsRestoringSnapshotVisuals(false);
                    ScheduleValidationOnly(traceOperation);
                    LogDesignerPerf(traceOperation, "validation scheduling", Elapsed(total));
                    break;

                case FrontedDesignerSnapshotRestoreMode.ScheduledValidationAndPreview:
                    _clearRestoreVisualsAfterScheduledPreview = true;
                    shouldNotifyUndoRedoInFinally = false;
                    ScheduleValidationAndPreviewRender(traceOperation);
                    LogDesignerPerf(traceOperation, "validation scheduling", Elapsed(total));
                    LogDesignerPerf(traceOperation, "preview render scheduling", Elapsed(total));
                    break;

                case FrontedDesignerSnapshotRestoreMode.ImmediateValidationAndPreview:
                    ApplyValidationMessages(_validator.Validate(document));
                    LogDesignerPerf(traceOperation, "validation execution", Elapsed(total));
                    RequestPreviewRender(config, _selectedCatalogEntry);
                    LogDesignerPerf(traceOperation, "preview render execution", Elapsed(total));
                    SetIsRestoringSnapshotVisuals(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            RefreshDirtyState();
            LogDesignerPerf(traceOperation, "total", Elapsed(total));
        }
        finally
        {
            if (shouldNotifyUndoRedoInFinally)
            {
                SetIsRestoringSnapshotVisuals(false);
                NotifyUndoRedoCommands();
            }
        }
    }

    private static FrontedDesignerUndoSnapshot? DeserializeUndoSnapshot(string snapshot)
    {
        try
        {
            var undoSnapshot = JsonSerializer.Deserialize<FrontedDesignerUndoSnapshot>(snapshot);
            if (undoSnapshot?.CanvasConfig is not null)
            {
                return undoSnapshot;
            }
        }
        catch (JsonException)
        {
            // 较旧的内存快照只存储了画布配置 JSON。
        }

        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(snapshot);
        return config is null
            ? null
            : new FrontedDesignerUndoSnapshot { CanvasConfig = config };
    }

    private static bool BehaviorDocumentsEqual(
        FrontedBehaviorDocument current,
        FrontedBehaviorDocument snapshot) =>
        string.Equals(
            JsonSerializer.Serialize(current),
            JsonSerializer.Serialize(snapshot),
            StringComparison.Ordinal);

    private void RestoreBehaviorDocumentSnapshot(
        FrontedBehaviorDocument? behaviorDocument,
        string windowTypeName,
        string canvasName)
    {
        if (behaviorDocument is null)
        {
            return;
        }

        behaviorDocument.WindowType = windowTypeName;
        behaviorDocument.CanvasName = canvasName;
        BehaviorPanel.SetDocument(behaviorDocument);
        BehaviorPanel.SetCopyContext(windowTypeName, CurrentDocument?.Controls);
        BehaviorPanel.SetSelectedControl(SelectedDesignItem);
        RebuildAnimationPartEditorItems();
        AreBehaviorsDirty = true;
    }

    private bool TryRestoreGeometryOnlySnapshot(
        FrontedCanvasConfig targetConfig,
        string traceOperation,
        Stopwatch? total)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        var plan = FrontedDesignerSnapshotRestorePlanner.CreatePlan(
            _designConverter.ToConfig(CurrentDocument),
            targetConfig);
        LogDesignerPerf(traceOperation, $"diff plan: {plan.Reason}", Elapsed(total));
        if (!plan.CanRestoreGeometryOnly)
        {
            return false;
        }

        var currentItemsByName = CurrentDocument.Controls.ToDictionary(item => item.Name, StringComparer.Ordinal);
        var selectedName = SelectedDesignItem?.Name;
        var changedItems = new List<FrontedControlDesignItem>();
        var restoreAppliedToPreview = false;
        var shouldKeepRestoreSuppression = false;

        SetIsRestoringSnapshotVisuals(true);
        try
        {
            foreach (var (name, targetControl) in targetConfig.Controls)
            {
                var item = currentItemsByName[name];
                if (ApplyGeometryPatch(item.Config, targetControl))
                {
                    changedItems.Add(item);
                }
            }

            if (plan.OrderChanged)
            {
                ReorderCurrentDocumentControls(targetConfig.Controls.Keys, currentItemsByName);
                changedItems = CurrentDocument.Controls.ToList();
            }

            CurrentDocument.CanvasConfig.RequiredPlugins = targetConfig.RequiredPlugins;
            CurrentDocument.IsDirty = true;
            RefreshDirtyState();
            if (plan.OrderChanged || plan.ZIndexChanged)
            {
                RebuildFilteredDesignItems();
            }
            else
            {
                RebuildLayerGroups();
            }

            var selectedItem = selectedName is null
                ? null
                : CurrentDocument.Controls.FirstOrDefault(control =>
                    string.Equals(control.Name, selectedName, StringComparison.Ordinal));
            if (!ReferenceEquals(SelectedDesignItem, selectedItem))
            {
                SelectDesignItem(selectedItem);
            }
            else
            {
                NormalizeSelectionState();
                RefreshSelectedControlDisplay();
            }

            OnPropertyChanged(nameof(CanReorderLayers));
            OnPropertyChanged(nameof(LayerReorderHint));
            DeleteSelectedControlCommand.NotifyCanExecuteChanged();
            CopySelectedControlCommand.NotifyCanExecuteChanged();

            var args = new FrontedDesignerGeometryPatchRequestedEventArgs(
                changedItems,
                rebuildLayerPanel: plan.OrderChanged || plan.ZIndexChanged,
                rebuildInteractionLayer: plan.OrderChanged || plan.ZIndexChanged,
                updateSelection: changedItems.Any(item => ReferenceEquals(item, SelectedDesignItem))
                                 || plan.OrderChanged
                                 || plan.ZIndexChanged,
                zIndexChanged: plan.ZIndexChanged);
            DesignerGeometryPatchRequested?.Invoke(this, args);

            if (!args.Applied)
            {
                shouldKeepRestoreSuppression = true;
                _clearRestoreVisualsAfterScheduledPreview = true;
                ScheduleValidationAndPreviewRender(traceOperation);
                LogDesignerPerf(
                    traceOperation,
                    $"scheduled full restore after geometry patch failed: {args.FailureReason}",
                    Elapsed(total));
                return true;
            }

            restoreAppliedToPreview = true;
            ScheduleValidationOnly(traceOperation);
            LogDesignerPerf(traceOperation, $"geometry fast restore: {changedItems.Count} item(s)", Elapsed(total));
            return true;
        }
        finally
        {
            if (!shouldKeepRestoreSuppression)
            {
                SetIsRestoringSnapshotVisuals(false);
                NotifyUndoRedoCommands();
            }
            else if (!restoreAppliedToPreview)
            {
                NotifyUndoRedoCommands();
            }
        }
    }

    private bool ApplyGeometryPatch(
        FrontedControlConfigBase current,
        FrontedControlConfigBase target)
    {
        var changed = false;
        if (!DoubleEquals(current.Left, target.Left))
        {
            current.Left = target.Left;
            changed = true;
        }

        if (!DoubleEquals(current.Top, target.Top))
        {
            current.Top = target.Top;
            changed = true;
        }

        if (!NullableDoubleEquals(current.Width, target.Width))
        {
            current.Width = target.Width;
            changed = true;
        }

        if (!NullableDoubleEquals(current.Height, target.Height))
        {
            current.Height = target.Height;
            changed = true;
        }

        if (current.ZIndex != target.ZIndex)
        {
            current.ZIndex = target.ZIndex;
            changed = true;
        }

        // Generic Part geometry patch: iterate Part definitions and copy
        // geometry values from target to current via storage accessors.
        // This replaces control-specific branches (e.g. BorderedImage ImageWidth/ImageHeight)
        // with a unified Part-driven approach.
        // 通过 _selectionBuilder.GetParts 走统一 Registry 链路，让插件 Part 也能被回滚时正确补齐几何。
        if (current.GetType() == target.GetType())
        {
            foreach (var part in _selectionBuilder.GetParts(current))
            {
                changed |= PatchPartGeometry(part, current, target);
            }
        }

        return changed;
    }

    private static bool PatchPartGeometry(
        FrontedV3PartDefinition part,
        FrontedControlConfigBase current,
        FrontedControlConfigBase target)
    {
        var changed = false;

        if (part.WidthStorage is not null)
        {
            var targetValue = ToNullableDouble(part.WidthStorage.GetValue(target));
            var currentValue = ToNullableDouble(part.WidthStorage.GetValue(current));
            if (!NullableDoubleEquals(currentValue, targetValue))
            {
                part.WidthStorage.SetValue(current, part.WidthStorage.GetValue(target));
                changed = true;
            }
        }

        if (part.HeightStorage is not null)
        {
            var targetValue = ToNullableDouble(part.HeightStorage.GetValue(target));
            var currentValue = ToNullableDouble(part.HeightStorage.GetValue(current));
            if (!NullableDoubleEquals(currentValue, targetValue))
            {
                part.HeightStorage.SetValue(current, part.HeightStorage.GetValue(target));
                changed = true;
            }
        }

        if (part.XStorage is not null)
        {
            var targetValue = ToNullableDouble(part.XStorage.GetValue(target));
            var currentValue = ToNullableDouble(part.XStorage.GetValue(current));
            if (!NullableDoubleEquals(currentValue, targetValue))
            {
                part.XStorage.SetValue(current, part.XStorage.GetValue(target));
                changed = true;
            }
        }

        if (part.YStorage is not null)
        {
            var targetValue = ToNullableDouble(part.YStorage.GetValue(target));
            var currentValue = ToNullableDouble(part.YStorage.GetValue(current));
            if (!NullableDoubleEquals(currentValue, targetValue))
            {
                part.YStorage.SetValue(current, part.YStorage.GetValue(target));
                changed = true;
            }
        }

        return changed;
    }

    private static double? ToNullableDouble(object? value)
    {
        return value is null ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    private static bool DoubleEquals(double left, double right)
    {
        return Math.Abs(left - right) < 0.0001D;
    }

    private static bool NullableDoubleEquals(double? left, double? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return left.HasValue == right.HasValue;
        }

        return Math.Abs(left.Value - right.Value) < 0.0001D;
    }

    private void ReorderCurrentDocumentControls(
        IEnumerable<string> targetOrder,
        IReadOnlyDictionary<string, FrontedControlDesignItem> currentItemsByName)
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var reordered = targetOrder.Select(name => currentItemsByName[name]).ToList();
        CurrentDocument.Controls.Clear();
        foreach (var item in reordered)
        {
            CurrentDocument.Controls.Add(item);
        }
    }

    private void SetIsRestoringSnapshotVisuals(bool value)
    {
        if (_isRestoringSnapshot == value)
        {
            return;
        }

        _isRestoringSnapshot = value;
        OnPropertyChanged(nameof(IsRestoringSnapshotVisuals));
    }

    private void ClearUndoRedo()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        NotifyUndoRedoCommands();
    }

    private void NotifyUndoRedoCommands()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool HasIncomingReferences(string controlName)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        return _referenceScanner.GetIncomingReferences(controlName).Count > 0;
    }
}
