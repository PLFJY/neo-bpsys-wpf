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
/// 行为面板的CopyPaste逻辑。
/// </summary>
public sealed partial class BehaviorPanelViewModel
{
    [RelayCommand]
    public void CopyBehavior(BehaviorEditorViewModel? behavior)
    {
        if (behavior is null || SelectedControl is null || SelectedControl.Config.BehaviorGuid == Guid.Empty)
        {
            return;
        }

        _behaviorClipboard.Set(_copyPasteService.Copy(CurrentWindowType, SelectedControl, behavior.Model, CurrentDocument));
        OnPropertyChanged(nameof(CanPasteBehavior));
        PasteBehaviorCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 将剪贴板行为粘贴到选中控件。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPasteBehavior))]
    public void PasteBehavior()
    {
        if (SelectedControl is null || _behaviorClipboard.Payload is null)
        {
            return;
        }

        PasteBehaviorToTargets([SelectedControl], new FrontedBehaviorPasteOptions());
    }

    /// <summary>
    /// 启动多目标行为复制流程。
    /// </summary>
    /// <param name="behavior">要复制到其他目标控件的行为行。</param>
    [RelayCommand]
    public void CopyBehaviorTo(BehaviorEditorViewModel? behavior)
    {
        CopyBehavior(behavior);
        if (_behaviorClipboard.Payload is null)
        {
            return;
        }

        var previews = AvailableControls
            .Where(control => !ReferenceEquals(control, SelectedControl))
            .Select(control => _copyPasteService.Preview(
                _behaviorClipboard.Payload,
                control,
                new FrontedBehaviorPasteOptions()))
            .ToArray();
        CopyBehaviorToRequested?.Invoke(new FrontedBehaviorCopyToRequest(this, previews));
    }

    /// <summary>
    /// 将当前行为剪贴板 payload 粘贴到多个控件。
    /// </summary>
    /// <param name="targets">选中的目标控件。</param>
    /// <param name="options">粘贴选项。</param>
    /// <returns>粘贴结果，包含因不兼容而跳过的目标。</returns>
    public IReadOnlyList<FrontedBehaviorPasteResult> PasteBehaviorToTargets(
        IEnumerable<FrontedControlDesignItem> targets,
        FrontedBehaviorPasteOptions options)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(options);
        if (_behaviorClipboard.Payload is null)
        {
            return [];
        }

        var targetList = targets.Distinct().ToArray();
        if (targetList.Length == 0)
        {
            return [];
        }

        CaptureUndoSnapshot();
        var results = new List<FrontedBehaviorPasteResult>();
        foreach (var target in targetList)
        {
            var oldGuid = target.Config.BehaviorGuid;
            var result = _copyPasteService.Paste(_behaviorClipboard.Payload, target, CurrentDocument, options);
            results.Add(result);
            if (result.Succeeded && oldGuid == Guid.Empty && target.Config.BehaviorGuid != Guid.Empty)
            {
                _markLayoutDirty();
            }
        }

        if (results.Any(result => result.Succeeded))
        {
            RefreshForSelectedControl();
            MarkBehaviorsDirty();
        }

        PasteStatus = string.Format(
            Localize("Designer.Behaviors.PasteSummary", "Copied to {0} controls. Skipped {1} incompatible controls."),
            results.Count(result => result.Succeeded),
            results.Count(result => !result.Succeeded));
        return results;
    }

    /// <summary>
    /// 使用当前行为剪贴板为给定目标控件创建粘贴预览。
    /// </summary>
    /// <param name="targets">要预览的控件。</param>
    /// <param name="options">粘贴选项。</param>
    /// <returns>兼容性与重写预览。</returns>
    public IReadOnlyList<FrontedBehaviorPastePreview> PreviewBehaviorTargets(
        IEnumerable<FrontedControlDesignItem> targets,
        FrontedBehaviorPasteOptions options)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(options);
        if (_behaviorClipboard.Payload is null)
        {
            return [];
        }

        return targets
            .Select(target => _copyPasteService.Preview(_behaviorClipboard.Payload, target, options))
            .ToArray();
    }

    partial void OnSelectedControlChanged(FrontedControlDesignItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedControl));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(CanPasteBehavior));
        PasteBehaviorCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 获取或创建选中控件的行为集合，并确保控件拥有稳定的行为 GUID。
    /// </summary>
    /// <returns>选中控件的行为集合；没有选中控件时返回 <see langword="null"/>。</returns>
}
