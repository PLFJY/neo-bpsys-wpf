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
/// Fronted Designer 的布局加载、保存、重载与重置业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    /// <summary>
    /// 通过命令包装器重新加载当前布局。
    /// </summary>
    /// <returns>重新加载完成后结束的任务。</returns>
    [RelayCommand]
    private async Task ReloadLayoutAsync()
    {
        await ReloadLayoutCoreAsync();
    }

    /// <summary>
    /// 重新加载选中布局、行为文档、预览状态、校验消息和编辑器选择。
    /// </summary>
    /// <returns>布局完成加载或清除后结束的任务。</returns>
    public async Task ReloadLayoutCoreAsync()
    {
        if (SelectedWindow is null || _selectedCatalogEntry is null)
        {
            ClearLoadedLayout(CreateMessage(
                FrontedLayoutValidationSeverity.Error,
                "LayoutSelectionMissing",
                "Window selection is required."));
            return;
        }

        var entry = _selectedCatalogEntry;
        CurrentWindowCanvasDisplay = ResolveEntryDisplayName(entry);
        DirtyIndicatorText = string.Empty;
        var reloadVersion = StartReloadLayoutRequest();
        var cancellationToken = _reloadLayoutCancellation?.Token ?? CancellationToken.None;

        try
        {
            var loadResult = await _layoutService.LoadWindowConfigWithMetadataAsync(
                entry.CanonicalWindowId,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || reloadVersion != _reloadLayoutVersion)
            {
                return;
            }

            ApplyLayoutSource(loadResult, entry);

            var windowConfig = loadResult.Config;
            var document = _designConverter.FromConfig(
                entry.CanonicalWindowId,
                FrontedLayoutConstants.BaseCanvasName,
                FrontedWindowConfigCanvasAdapter.ToCanvasConfig(windowConfig));

            _currentWindowSettings = CloneWindowSettings(windowConfig.WindowSettings);
            ControlFilterText = string.Empty;
            CurrentDocument = document;
            CurrentDocument.IsDirty = false;
            LoadWindowOptions(entry.CanonicalWindowId);
            var behaviorDocument = await _behaviorService.LoadDocumentAsync(
                entry.CanonicalWindowId,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || reloadVersion != _reloadLayoutVersion)
            {
                return;
            }

            ResetBehaviorDocument(behaviorDocument);
            SelectDesignItem(null);
            var validationMessages = _validator.Validate(document).ToList();
            if (!string.IsNullOrWhiteSpace(loadResult.Error))
            {
                validationMessages.Add(CreateMessage(
                    FrontedLayoutValidationSeverity.Warning,
                    "UserLayoutLoadFailed",
                    loadResult.Error));
            }

            ApplyValidationMessages(validationMessages);
            RequestPreviewRender(FrontedWindowConfigCanvasAdapter.ToCanvasConfig(windowConfig), entry);
            RefreshDirtyState();
        }
        catch (OperationCanceledException)
        {
            // 更新的窗口/画布选择已经取代本次加载请求。
        }
        catch (Exception ex)
        {
            if (reloadVersion != _reloadLayoutVersion)
            {
                return;
            }

            _logger.LogError(
                ex,
                "Failed to load fronted designer layout. Window: {WindowTypeName}",
                entry.CanonicalWindowId);

            ClearLoadedLayout(CreateMessage(
                FrontedLayoutValidationSeverity.Error,
                "LayoutLoadFailed",
                ex.Message));
        }
    }

    /// <summary>
    /// 通过命令包装器保存当前布局，并保留可见的校验失败消息。
    /// </summary>
    /// <returns>保存成功或失败后结束的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanSaveLayout))]
    private async Task SaveLayoutAsync()
    {
        await SaveCurrentLayoutAsync();
    }

    /// <summary>
    /// 校验当前布局和行为文档，并保存到活动布局包。
    /// </summary>
    /// <returns>保存成功完成时返回 <see langword="true"/>。</returns>
    public async Task<bool> SaveCurrentLayoutAsync()
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        var shouldSaveLayout = CurrentDocument.IsDirty;
        var shouldSaveBehaviors = AreBehaviorsDirty;
        if (!shouldSaveLayout && !shouldSaveBehaviors)
        {
            var messages = _validator.Validate(CurrentDocument);
            ApplyValidationMessages(messages);
            if (messages.Any(message => message.Severity == FrontedLayoutValidationSeverity.Error))
            {
                StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotSaveInvalidLayout");
                return false;
            }

            return true;
        }

        if (shouldSaveLayout)
        {
            var messages = _validator.Validate(CurrentDocument);
            ApplyValidationMessages(messages);
            if (messages.Any(message => message.Severity == FrontedLayoutValidationSeverity.Error))
            {
                StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotSaveInvalidLayout");
                return false;
            }
        }

        try
        {
            var wasBuiltInSource = string.Equals(
                LayoutSourceDisplay,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceBuiltIn"),
                StringComparison.Ordinal);

            if (shouldSaveLayout)
            {
                var config = _designConverter.ToConfig(CurrentDocument);
                config.Version = 3;
                var windowConfig = FrontedWindowConfigCanvasAdapter.FromCanvasConfig(config);
                windowConfig.WindowSettings = CloneWindowSettings(_currentWindowSettings);
                await _layoutService.SaveWindowConfigAsync(
                    CurrentDocument.WindowTypeName,
                    windowConfig);

                CleanupPendingImportedResources(includeCurrentDocument: true);
                CurrentDocument.IsDirty = false;
            }

            if (shouldSaveBehaviors)
            {
                BehaviorPanel.CurrentDocument.WindowType = CurrentDocument.WindowTypeName;
                BehaviorPanel.CurrentDocument.CanvasName = FrontedLayoutConstants.BaseCanvasName;
                await _behaviorService.SaveDocumentAsync(BehaviorPanel.CurrentDocument);
                AreBehaviorsDirty = false;
            }

            if (shouldSaveLayout || wasBuiltInSource)
            {
                var savedResult = await _layoutService.LoadWindowConfigWithMetadataAsync(
                    CurrentDocument.WindowTypeName);
                if (_selectedCatalogEntry is not null)
                {
                    ApplyLayoutSource(savedResult, _selectedCatalogEntry);
                }
                else
                {
                    LayoutSourceDisplay = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceUser");
                    LayoutSourcePath = savedResult.Path ?? string.Empty;
                }
            }

            StatusMessage = wasBuiltInSource
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "EditableLayoutSchemeCreated")
                : I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSaved");
            if (shouldSaveLayout || wasBuiltInSource)
            {
                WeakReferenceMessenger.Default.Send(new FrontedLayoutPackagesChangedMessage(this, null));
            }

            RefreshDirtyState();

            if ((shouldSaveLayout || shouldSaveBehaviors) && _frontedWindowService is not null)
            {
                _frontedWindowService.MarkWindowLayoutDirty(CurrentDocument.WindowTypeName);
                await _frontedWindowService.ReloadFrontedLayoutsAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save fronted designer user layout. Window: {WindowTypeName}",
                CurrentDocument.WindowTypeName);
            StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSaveFailed")}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 通过命令包装器重置当前布局。
    /// </summary>
    /// <returns>重置处理完成后结束的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanResetToBuiltIn))]
    private async Task ResetToBuiltInAsync()
    {
        await ResetToBuiltInCoreAsync();
    }

    /// <summary>
    /// 使用内置包版本替换当前可编辑布局。
    /// </summary>
    /// <returns>重置成功时返回 <see langword="true"/>。</returns>
    public async Task<bool> ResetToBuiltInCoreAsync()
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        var windowTypeName = CurrentDocument.WindowTypeName;
        var canvasName = FrontedLayoutConstants.BaseCanvasName;
        var config = await LoadBuiltInLayoutForResetAsync(windowTypeName, canvasName);
        if (config is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "MissingLayout");
            return false;
        }

        var document = _designConverter.FromConfig(
            windowTypeName,
            canvasName,
            config);
        document.IsDirty = false;
        var behaviorDocument = await _behaviorService.LoadBuiltInDocumentAsync(windowTypeName);

        ControlFilterText = string.Empty;
        CurrentDocument = document;
        ResetBehaviorDocument(behaviorDocument);
        SelectDesignItem(null);
        ApplyValidationMessages(_validator.Validate(document));
        RequestPreviewRender(config, _selectedCatalogEntry);
        LayoutSourceDisplay = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceBuiltIn");
        LayoutSourcePath = GetBuiltInPackageLayoutPath(windowTypeName);
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutReset");
        ClearUndoRedo();
        CleanupPendingImportedResources(includeCurrentDocument: false);
        RefreshDirtyState();
        return true;
    }

    /// <summary>
    /// 更新临时 Shift 键吸附状态，而不改变已持久化的吸附开关。
    /// </summary>
    /// <param name="isActive">当前 Shift 吸附是否处于活动状态。</param>
    public void UpdateShiftSnapActive(bool isActive)
    {
        IsShiftSnapActive = isActive;
    }
}
