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
/// Fronted Designer 的行为文档与预览动画作用域业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    private void RefreshDirtyState()
    {
        DirtyIndicatorText = CurrentDocument?.IsDirty == true || AreBehaviorsDirty
            ? $"* {(CurrentDocument?.IsDirty == true ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Unsaved") : I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Behaviors.UnsavedBehaviorChanges"))}"
            : string.Empty;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        NotifyLayoutCommandState();
    }

    private BehaviorPanelViewModel CreateBehaviorPanel()
    {
        return new BehaviorPanelViewModel(
            _localizationService,
            _behaviorEventCatalog,
            MarkLayoutDirtyFromBehaviorPanel,
            MarkBehaviorsDirty,
            animationRuntime: _animationRuntime,
            previewAnimationScope: _previewAnimationScope,
            saveBehaviorAsync: SaveBehaviorDocumentAsync,
            behaviorClipboard: _behaviorClipboard,
            copyPasteService: _behaviorCopyPasteService,
            captureUndoSnapshot: CaptureUndoSnapshot);
    }

    /// <summary>
    /// 持久化当前行为文档，并清除外层已修改标记。
    /// 由动画编辑器保存流程调用。
    /// </summary>
    /// <returns><c>true</c> if the save succeeded; otherwise <c>false</c>.</returns>
    private async Task<bool> SaveBehaviorDocumentAsync()
    {
        try
        {
            BehaviorPanel.CurrentDocument.WindowType = CurrentDocument?.WindowTypeName;
            BehaviorPanel.CurrentDocument.CanvasName = CurrentDocument?.CanvasName;
            await _behaviorService.SaveDocumentAsync(BehaviorPanel.CurrentDocument);
            AreBehaviorsDirty = false;
            RefreshDirtyState();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save behavior document from animation editor.");
            return false;
        }
    }

    private void MarkLayoutDirtyFromBehaviorPanel()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
    }

    private void MarkBehaviorsDirty()
    {
        AreBehaviorsDirty = true;
        RefreshDirtyState();
    }

    private void ResetBehaviorDocument(FrontedBehaviorDocument? document = null)
    {
        BehaviorPanel.SetDocument(document ?? new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = CurrentDocument?.WindowTypeName,
            CanvasName = CurrentDocument?.CanvasName
        });
        AreBehaviorsDirty = false;
        BehaviorPanel.SetCopyContext(CurrentDocument?.WindowTypeName, CurrentDocument?.Controls);
        BehaviorPanel.SetSelectedControl(SelectedDesignItem);
    }

    /// <summary>将当前预览视觉树设为动画目标作用域。</summary>
    /// <param name="previewRoot">预览根元素。</param>
    public void UpdateBehaviorPreviewAnimationScope(FrameworkElement previewRoot)
    {
        _previewAnimationScope?.Update(
            previewRoot,
            SelectedDesignItem,
            _selectedCatalogEntry?.CanonicalWindowId,
            FrontedLayoutConstants.BaseCanvasName,
            CurrentDocument?.Controls ?? [],
            BehaviorPanel.CurrentDocument);
    }

    /// <summary>仅同步当前选择的动画 Self 身份，不扫描整个预览树。</summary>
    internal void UpdateBehaviorPreviewSelection() => _previewAnimationScope?.UpdateSelection(SelectedDesignItem);

    /// <summary>释放预览动画会话并清除预览视觉树引用。</summary>
    public void ClearBehaviorPreviewAnimationScope()
    {
        try
        {
            if (_previewAnimationScope?.Root is { } root)
                _animationRuntime?.Release(root);
        }
        finally { _previewAnimationScope?.Clear(); }
    }

    /// <summary>结束关闭窗口的预览会话，取消未完成的布局加载并释放预览作用域。</summary>
    public void EndDesignerPreviewSession()
    {
        _scheduledDesignerWorkOperation?.Abort();
        _scheduledDesignerWorkOperation = null;
        _scheduledValidationAndPreviewPending = false;
        _scheduledValidationRequested = false;
        _scheduledPreviewRequested = false;
        _clearRestoreVisualsAfterScheduledPreview = false;
        try
        {
            ++_reloadLayoutVersion;
            var cancellation = _reloadLayoutCancellation;
            _reloadLayoutCancellation = null;
            try { cancellation?.Cancel(); }
            finally { cancellation?.Dispose(); }
        }
        finally { ClearBehaviorPreviewAnimationScope(); }
    }
}
