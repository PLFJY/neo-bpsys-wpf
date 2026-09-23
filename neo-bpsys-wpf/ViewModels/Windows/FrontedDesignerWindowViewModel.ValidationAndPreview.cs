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
/// Fronted Designer 的校验、调度与预览渲染业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    /// <summary>
    /// 运行完整布局校验并更新校验面板。
    /// </summary>
    [RelayCommand]
    private void ValidateLayout()
    {
        if (CurrentDocument is null)
        {
            ApplyValidationMessages(_lastValidationMessages);
            return;
        }

        ValidateCurrentDocument();
    }
    /// <summary>
    /// 将非致命渲染错误添加到校验/状态面板。
    /// </summary>
    /// <summary>
    /// 将预览渲染异常转换为编辑器校验消息。
    /// </summary>
    /// <param name="exception">渲染异常。</param>
    public void ReportRenderFailure(Exception exception)
    {
        _logger.LogError(exception, "Failed to render fronted designer preview.");

        var messages = _lastValidationMessages
            .Concat(
            [
                CreateMessage(
                    FrontedLayoutValidationSeverity.Error,
                    "RenderFailed",
                    exception.Message)
            ])
            .ToArray();

        ApplyValidationMessages(messages);
        StatusMessage = exception.Message;
    }
    private void ApplyLayoutSource(
        FrontedLayoutLoadResult loadResult,
        FrontedDesignerLayoutCatalogEntry entry)
    {
        LayoutSourceDisplay = loadResult.Source switch
        {
            FrontedLayoutSource.User => I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceUser"),
            FrontedLayoutSource.BuiltIn => I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceBuiltIn"),
            _ => I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceError")
        };
        LayoutSourcePath = loadResult.Path
            ?? GetBuiltInPackageLayoutPath(entry.CanonicalWindowId);

        if (!string.IsNullOrWhiteSpace(loadResult.Error))
        {
            StatusMessage = loadResult.Error;
        }
    }

    private async Task<FrontedCanvasConfig?> LoadBuiltInLayoutForResetAsync(
        string windowTypeName,
        string canvasName)
    {
        var builtInPath = GetBuiltInPackageLayoutPath(windowTypeName);
        if (File.Exists(builtInPath))
        {
            var json = await File.ReadAllTextAsync(builtInPath);
            var config = JsonSerializer.Deserialize<FrontedWindowConfig>(json);
            if (config is not null)
            {
                return FrontedWindowConfigCanvasAdapter.ToCanvasConfig(config);
            }
        }

        _logger.LogWarning(
            "Built-in layout not found for reset. Window: {WindowTypeName}, Canvas: {CanvasName}, Path: {Path}",
            windowTypeName,
            canvasName,
            builtInPath);
        return null;
    }

    private string GetBuiltInPackageLayoutPath(string windowTypeName)
    {
        if (_packageManager is null)
        {
            return string.Empty;
        }

        return _packageManager.GetPackageLayoutPath(
            FrontedLayoutPackageManager.BuiltInPackageId,
            windowTypeName);
    }

    private void ClearLoadedLayout(FrontedLayoutValidationMessage message)
    {
        ControlFilterText = string.Empty;
        CurrentDocument = null;
        SelectDesignItem(null);
        ResetBehaviorDocument();
        ApplyValidationMessages([message]);
        RequestPreviewRender(null, _selectedCatalogEntry);
    }

    private void ApplyValidationMessages(
        IReadOnlyList<FrontedLayoutValidationMessage> messages,
        bool refreshPropertyGrid = true)
    {
        _lastValidationMessages = messages;
        ValidationMessages.Clear();
        foreach (var message in messages.Take(FrontedLayoutLimits.MaxValidationMessagesShown))
        {
            ValidationMessages.Add(new FrontedLayoutValidationMessage
            {
                Severity = message.Severity,
                Code = message.Code,
                ControlName = message.ControlName,
                PropertyName = message.PropertyName,
                Message = FrontedTextLimitHelper.Clamp(
                    message.Message,
                    FrontedLayoutLimits.MaxValidationMessageLength)
            });
        }

        if (messages.Count > FrontedLayoutLimits.MaxValidationMessagesShown)
        {
            ValidationMessages.Add(CreateMessage(
                FrontedLayoutValidationSeverity.Info,
                "ValidationMessagesTruncated",
                string.Format(
                    CultureInfo.InvariantCulture,
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ValidationMessagesTruncated"),
                    messages.Count - FrontedLayoutLimits.MaxValidationMessagesShown)));
        }

        ErrorCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Error);
        WarningCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Warning);
        InfoCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Info);
        StatusMessage =
            $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Errors")}: {ErrorCount}  "
            + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Warnings")}: {WarningCount}  "
            + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Infos")}: {InfoCount}";
        RefreshSelectedControlDisplay();
        if (refreshPropertyGrid)
        {
            RebuildPropertyEditorItems();
        }
    }

    private int StartReloadLayoutRequest()
    {
        _reloadLayoutCancellation?.Cancel();
        _reloadLayoutCancellation?.Dispose();
        _reloadLayoutCancellation = new CancellationTokenSource();
        return ++_reloadLayoutVersion;
    }

    private void ValidateCurrentDocument(bool refreshPropertyGrid = true)
    {
        if (CurrentDocument is null || _validator is null)
        {
            return;
        }

        ApplyValidationMessages(_validator.Validate(CurrentDocument), refreshPropertyGrid);
    }

    private void ScheduleValidationAndPreviewRender(string reason)
    {
        ScheduleDesignerWork(reason, validate: true, preview: true);
    }

    private void ScheduleValidationOnly(string reason)
    {
        ScheduleDesignerWork(reason, validate: true, preview: false);
    }

    private void ScheduleDesignerWork(string reason, bool validate, bool preview)
    {
        _scheduledValidationRequested |= validate;
        _scheduledPreviewRequested |= preview;
        if (_scheduledValidationAndPreviewPending)
        {
            LogDesignerPerf(reason, "designer work already scheduled");
            return;
        }

        _scheduledValidationAndPreviewPending = true;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        var priority = preview ? DispatcherPriority.Render : DispatcherPriority.Background;
        dispatcher.BeginInvoke(
            new Action(ExecuteScheduledValidationAndPreviewRender),
            priority);
    }

    public void ExecuteScheduledDesignerWorkForTests()
    {
        if (!_scheduledValidationAndPreviewPending)
        {
            return;
        }

        ExecuteScheduledValidationAndPreviewRender();
    }

    private void ExecuteScheduledValidationAndPreviewRender()
    {
        if (!_scheduledValidationAndPreviewPending)
        {
            return;
        }

        _scheduledValidationAndPreviewPending = false;
        var shouldValidate = _scheduledValidationRequested;
        var shouldPreview = _scheduledPreviewRequested;
        var shouldClearRestoreVisuals = _clearRestoreVisualsAfterScheduledPreview;
        _scheduledValidationRequested = false;
        _scheduledPreviewRequested = false;
        _clearRestoreVisualsAfterScheduledPreview = false;

        if (CurrentDocument is null)
        {
            if (shouldClearRestoreVisuals)
            {
                SetIsRestoringSnapshotVisuals(false);
            }

            return;
        }

        try
        {
            var total = StartDesignerPerfTrace();
            if (shouldPreview)
            {
                RequestPreviewRenderCurrentDocument();
                ScheduledDesignerPreviewExecutionCount++;
                LogDesignerPerf("ScheduledDesignerWork", "preview render execution", Elapsed(total));
            }

            if (shouldValidate)
            {
                ValidateCurrentDocument();
                ScheduledDesignerValidationExecutionCount++;
                LogDesignerPerf("ScheduledDesignerWork", "validation execution", Elapsed(total));
            }
        }
        finally
        {
            if (shouldClearRestoreVisuals)
            {
                SetIsRestoringSnapshotVisuals(false);
                NotifyUndoRedoCommands();
            }
        }
    }
    private void RequestPreviewRender(FrontedCanvasConfig? config, FrontedDesignerLayoutCatalogEntry? entry)
    {
        PreviewRenderRequested?.Invoke(
            this,
            new FrontedDesignerPreviewRenderRequestedEventArgs(
                config,
                BehaviorPanel.CurrentDocument,
                entry is null
                    ? null
                    : new FrontedRenderContext
                    {
                        WindowId = entry.CanonicalWindowId,
                        WindowTypeName = entry.CanonicalWindowId,
                        CanvasName = FrontedLayoutConstants.BaseCanvasName,
                        SharedDataServiceOverride = _designerPreviewSharedDataService,
                        RenderMissingPluginPlaceholders = true,
                        IsDesignerPreview = true
                    }));
    }

    private void RebuildAddControlCatalog()
    {
        AddControlCatalogGroups.Clear();
        foreach (var group in _defaultConfigFactory.GetCatalog())
        {
            AddControlCatalogGroups.Add(group);
        }
    }
}
