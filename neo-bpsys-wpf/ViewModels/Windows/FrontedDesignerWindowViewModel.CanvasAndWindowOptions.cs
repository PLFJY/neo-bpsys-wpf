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
/// Fronted Designer 的画布与窗口设置业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    partial void OnWindowAllowTransparencyChanged(bool value)
    {
        if (_isLoadingWindowOptions || SelectedWindow is null)
        {
            return;
        }

        _ = SaveWindowOptionsAsync(restartWindowForTransparencyChange: true, applyBackgroundImmediately: false);
    }

    partial void OnWindowBackgroundColorEditTextChanged(string value)
    {
        if (FrontedPropertyColorHelper.TryParseArgbColor(value, out var color))
        {
            WindowBackgroundColorValue = color;
        }
    }

    partial void OnWindowBackgroundColorValueChanged(Color value)
    {
        WindowBackgroundColorEditText = FrontedPropertyColorHelper.ToArgbString(value);
    }

    partial void OnEnableBoModeStatesChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBoModeStateSelectorVisible));
        OnPropertyChanged(nameof(CanCopyBo5ToBo3));
        CopyBo5ToBo3Command.NotifyCanExecuteChanged();

        if (_isUpdatingBoModeStateUi || CurrentDocument is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var config = _designConverter.ToConfig(CurrentDocument);
        config.EnableBoModeStates = value;
        if (value)
        {
            EnsureBo3State(config);
        }

        var nextState = value ? CurrentDocument.EditingBoModeState : FrontedCanvasBoModeState.Bo5;
        RebuildDocumentFromConfig(config, nextState, preserveDirty: true, selectedControlName: SelectedDesignItem?.Name);
        CanvasPropertiesStatus = value
            ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Canvas.BoModeStatesEnabled")
            : I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Canvas.BoModeStatesDisabledConfirm");
    }

    partial void OnSelectedBoModeStateOptionChanged(FrontedCanvasBoModeStateOption? value)
    {
        if (_isUpdatingBoModeStateUi || CurrentDocument is null || value is null)
        {
            return;
        }

        if (CurrentDocument.EditingBoModeState == value.State)
        {
            return;
        }

        var config = _designConverter.ToConfig(CurrentDocument);
        RebuildDocumentFromConfig(
            config,
            value.State,
            preserveDirty: CurrentDocument.IsDirty,
            selectedControlName: SelectedDesignItem?.Name);
    }
    [RelayCommand]
    private void ApplyCanvasSize()
    {
        ApplyCanvasSizeEdit(CanvasWidthEditText, CanvasHeightEditText);
    }

    /// <summary>
    /// 应用画布设置编辑器中的 Canvas 宽高文本。
    /// </summary>
    /// <param name="widthText">Canvas 宽度文本。</param>
    /// <param name="heightText">Canvas 高度文本。</param>
    /// <returns>两个值均有效并已应用时返回 <see langword="true"/>。</returns>
    public bool ApplyCanvasSizeEdit(string widthText, string heightText)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        if (!TryParsePositiveDouble(widthText, out var width)
            || !TryParsePositiveDouble(heightText, out var height))
        {
            CanvasPropertiesStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CanvasSizeMustBePositive");
            return false;
        }

        if (Math.Abs(CurrentDocument.CanvasConfig.CanvasWidth - width) < 0.0001D
            && Math.Abs(CurrentDocument.CanvasConfig.CanvasHeight - height) < 0.0001D)
        {
            RefreshCanvasPropertyBuffers();
            return true;
        }

        CaptureUndoSnapshot();
        CurrentDocument.CanvasConfig.CanvasWidth = width;
        CurrentDocument.CanvasConfig.CanvasHeight = height;
        CurrentDocument.IsDirty = true;
        RefreshCanvasPropertyBuffers();
        FinishCanvasConfigEdit(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CanvasPropertiesApplied"));
        return true;
    }

    [RelayCommand]
    private void ApplyWindowSize()
    {
        ApplyWindowSizeEdit(WindowWidthEditText, WindowHeightEditText);
    }

    /// <summary>
    /// 应用窗口设置编辑器中的前台窗口宽高文本。
    /// </summary>
    /// <param name="widthText">窗口宽度文本。</param>
    /// <param name="heightText">窗口高度文本。</param>
    /// <returns>两个值均有效并已应用时返回 <see langword="true"/>。</returns>
    public bool ApplyWindowSizeEdit(string widthText, string heightText)
    {
        if (SelectedWindow is null)
        {
            return false;
        }

        if (!TryParseOptionalPositiveDouble(widthText).HasValue
            && !TryParseOptionalPositiveDouble(heightText).HasValue)
        {
            // 允许同时清空两个字段，并保存为 null。
        }
        else if (!TryParseOptionalPositiveDouble(widthText).HasValue
                 || !TryParseOptionalPositiveDouble(heightText).HasValue)
        {
            WindowOptionsStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "WindowSizeMustBePositive");
            return false;
        }

        _ = SaveWindowOptionsAsync(
            restartWindowForTransparencyChange: false,
            applyBackgroundImmediately: false,
            applyWindowSizeImmediately: true);
        return true;
    }

    /// <summary>
    /// 将字符串解析为正数 double；字符串为空或空白时返回 <c>null</c>。
    /// </summary>
    /// <param name="text">输入文本，或 <c>null</c>。</param>
    /// <returns>正数 double；输入为空或空白时返回 <c>null</c>。</returns>
    /// <summary>
    /// 从编辑器文本解析可选正数。
    /// </summary>
    /// <param name="text">要解析的文本。</param>
    /// <returns>解析后的值；文本为空或无效时返回 <see langword="null"/>。</returns>
    private static double? TryParseOptionalPositiveDouble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return TryParsePositiveDouble(text!, out var value) ? value : null;
    }

    [RelayCommand]
    private async Task ApplyBackgroundImageAsync()
    {
        await ApplyCanvasBackgroundEditAsync(BackgroundImageEditText);
    }

    /// <summary>
    /// 根据文本输入应用 Canvas 背景图片 URI。
    /// </summary>
    /// <param name="backgroundImage">背景图片 URI 或路径。</param>
    /// <returns>编辑被接受时返回 <see langword="true"/>。</returns>
    public bool ApplyCanvasBackgroundEdit(string? backgroundImage)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        if (IsAbsoluteFilePath(backgroundImage))
        {
            return StoreLocalBackgroundImage(backgroundImage!);
        }

        var rawValue = string.IsNullOrWhiteSpace(backgroundImage) ? null : backgroundImage.Trim();
        var normalizedValue = rawValue is null
            ? null
            : FrontedTextLimitHelper.Clamp(rawValue, FrontedLayoutLimits.MaxResourcePathLength);
        if (!string.Equals(rawValue, normalizedValue, StringComparison.Ordinal))
        {
            CanvasPropertiesStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "InputTruncated");
        }

        if (string.Equals(GetEditingStateBackground(CurrentDocument), normalizedValue, StringComparison.Ordinal))
        {
            BackgroundImageEditText = normalizedValue ?? string.Empty;
            return true;
        }

        CaptureUndoSnapshot();
        SetEditingStateBackground(CurrentDocument, normalizedValue);
        CurrentDocument.IsDirty = true;
        BackgroundImageEditText = normalizedValue ?? string.Empty;
        FinishCanvasConfigEdit(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CanvasPropertiesApplied"));
        return true;
    }

    /// <summary>
    /// 应用资源浏览器选中的 Canvas 背景图片。
    /// </summary>
    /// <param name="selectedResourcePath">选中的资源 URI 或文件路径。</param>
    /// <returns><see langword="true"/> when the resource was accepted.</returns>
    public bool ApplyCanvasBackgroundResourceSelection(string selectedResourcePath)
    {
        return IsAbsoluteFilePath(selectedResourcePath)
            ? StoreLocalBackgroundImage(selectedResourcePath)
            : ApplyCanvasBackgroundEdit(selectedResourcePath);
    }

    /// <summary>
    /// 应用资源浏览器选中的 Canvas 背景图片，并在本地超限图片时提供压缩选项。
    /// </summary>
    /// <param name="selectedResourcePath">选中的资源 URI 或文件路径。</param>
    /// <returns>资源已接受时返回 <see langword="true"/>。</returns>
    public async Task<bool> ApplyCanvasBackgroundResourceSelectionAsync(string selectedResourcePath)
    {
        return IsAbsoluteFilePath(selectedResourcePath)
            ? await StoreLocalBackgroundImageAsync(selectedResourcePath)
            : ApplyCanvasBackgroundEdit(selectedResourcePath);
    }

    /// <summary>
    /// 根据文本输入应用 Canvas 背景图片 URI，并在绝对本地图片超限时提供压缩选项。
    /// </summary>
    /// <param name="backgroundImage">背景图片 URI 或路径。</param>
    /// <returns>编辑被接受时返回 <see langword="true"/>。</returns>
    public async Task<bool> ApplyCanvasBackgroundEditAsync(string? backgroundImage)
    {
        return IsAbsoluteFilePath(backgroundImage)
            ? await StoreLocalBackgroundImageAsync(backgroundImage!)
            : ApplyCanvasBackgroundEdit(backgroundImage);
    }

    [RelayCommand]
    private void ClearBackgroundImage()
    {
        ClearCanvasBackground();
    }

    /// <summary>
    /// 清除活动 Canvas 状态的背景图片。
    /// </summary>
    /// <returns>文档可用并已更新时返回 <see langword="true"/>。</returns>
    public bool ClearCanvasBackground()
    {
        return ApplyCanvasBackgroundEdit(null);
    }

    /// <summary>
    /// 将本地背景图片复制到可编辑包资源存储，并应用其 BPUI URI。
    /// </summary>
    /// <param name="sourcePath">用户选择的本地图片文件。</param>
    /// <returns>文件导入并应用时返回 <see langword="true"/>。</returns>
    public bool StoreLocalBackgroundImage(string sourcePath)
    {
        if (_localResourceStore is null)
        {
            return false;
        }

        var validationMessage = ValidateLocalImageForStorage(sourcePath);
        if (validationMessage is not null)
        {
            CanvasPropertiesStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {validationMessage}";
            return false;
        }

        try
        {
            var result = _localResourceStore.StoreImageWithResult(sourcePath);
            var applied = ApplyCanvasBackgroundEdit(result.ResourceUri);
            RecordPendingImportedResource(result, "Canvas BackgroundImage", applied);
            return applied;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store local fronted canvas background image.");
            CanvasPropertiesStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 将本地背景图片复制到可编辑包资源存储，并在超限时由用户选择是否压缩后应用其 BPUI URI。
    /// </summary>
    /// <param name="sourcePath">用户选择的本地图片文件。</param>
    /// <returns>文件导入并应用时返回 <see langword="true"/>。</returns>
    public async Task<bool> StoreLocalBackgroundImageAsync(string sourcePath)
    {
        var (result, errorMessage) = await StoreLocalImageWithOptionalCompressionAsync(sourcePath);
        if (result is null)
        {
            CanvasPropertiesStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {errorMessage}";
            return false;
        }

        var applied = ApplyCanvasBackgroundEdit(result.ResourceUri);
        RecordPendingImportedResource(result, "Canvas BackgroundImage", applied);
        if (result.WasCompressed)
        {
            CanvasPropertiesStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ImageCompressed");
        }

        return applied;
    }

    [RelayCommand(CanExecute = nameof(CanCopyBo5ToBo3))]
    private void CopyBo5ToBo3()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var config = _designConverter.ToConfig(CurrentDocument);
        EnsureBo3State(config);
        config.EnableBoModeStates = true;
        config.BoModeStates[FrontedCanvasRuntimeStateResolver.Bo3StateKey] = new FrontedCanvasStateConfig
        {
            BackgroundImage = config.BackgroundImage,
            RequiredPlugins = DeepClone(config.RequiredPlugins),
            Controls = CloneControls(config.Controls)
        };

        RebuildDocumentFromConfig(
            config,
            FrontedCanvasBoModeState.Bo3,
            preserveDirty: true,
            selectedControlName: SelectedDesignItem?.Name);
        CanvasPropertiesStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Canvas.Bo3LayoutCopied");
    }

    /// <summary>
    /// 将资源浏览器结果应用到图片/资源属性行。
    /// </summary>
    /// <param name="item">接收资源值的属性行。</param>
    /// <param name="selectedResourcePath">选中的资源 URI 或文件路径。</param>
    /// <returns>属性已更新时返回 <see langword="true"/>。</returns>
    public bool ApplyPropertyResourceSelection(FrontedPropertyEditorItem item, string selectedResourcePath)
    {
        if (IsAbsoluteFilePath(selectedResourcePath))
        {
            if (_localResourceStore is null)
            {
                return false;
            }

            var validationMessage = ValidateLocalImageForStorage(selectedResourcePath);
            if (validationMessage is not null)
            {
                SetPropertyEditError(
                    item,
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {validationMessage}",
                    selectedResourcePath);
                return false;
            }

            try
            {
                var result = _localResourceStore.StoreImageWithResult(selectedResourcePath);
                var applied = ApplyPropertyEdit(item, result.ResourceUri);
                RecordPendingImportedResource(result, item.PropertyName, applied);
                return applied;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to store local fronted control resource for property {PropertyName}.",
                    item.PropertyName);
                SetPropertyEditError(
                    item,
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {ex.Message}",
                    selectedResourcePath);
                return false;
            }
        }

        return ApplyPropertyEdit(item, selectedResourcePath);
    }

    /// <summary>
    /// 将资源浏览器结果应用到图片/资源属性行，并在本地超限图片时提供压缩选项。
    /// </summary>
    /// <param name="item">接收资源值的属性行。</param>
    /// <param name="selectedResourcePath">选中的资源 URI 或文件路径。</param>
    /// <returns>属性已更新时返回 <see langword="true"/>。</returns>
    public async Task<bool> ApplyPropertyResourceSelectionAsync(
        FrontedPropertyEditorItem item,
        string selectedResourcePath)
    {
        if (!IsAbsoluteFilePath(selectedResourcePath))
        {
            return ApplyPropertyEdit(item, selectedResourcePath);
        }

        var (result, errorMessage) = await StoreLocalImageWithOptionalCompressionAsync(selectedResourcePath);
        if (result is null)
        {
            SetPropertyEditError(
                item,
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {errorMessage}",
                selectedResourcePath);
            return false;
        }

        var applied = ApplyPropertyEdit(item, result.ResourceUri);
        RecordPendingImportedResource(result, item.PropertyName, applied);
        if (result.WasCompressed)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ImageCompressed");
        }

        return applied;
    }

    /// <summary>
    /// 将字体文件导入活动布局包，并应用第一个发现的字体族。
    /// </summary>
    /// <param name="item">字体族属性行。</param>
    /// <param name="sourcePath">源字体文件路径。</param>
    /// <returns>是否已导入并应用字体。</returns>
    /// <summary>
    /// 将字体导入当前布局包，并把其 BPUI 字体 URI 应用到属性行。
    /// </summary>
    /// <param name="item">接收导入字体 URI 的字体族属性行。</param>
    /// <param name="sourcePath">本地字体文件路径。</param>
    /// <returns>导入并应用属性成功时返回 <see langword="true"/>。</returns>
    public async Task<bool> ImportAndApplyPackageFontAsync(FrontedPropertyEditorItem item, string sourcePath)
    {
        if (_localResourceStore is null || _packageManager is null)
        {
            return false;
        }

        if (item.EditorKind != FrontedPropertyEditorKind.FontFamily)
        {
            return false;
        }

        try
        {
            var package = await _packageManager.EnsureWritableActivePackageAsync();
            var packageRoot = Path.Combine(_packageManager.GetPackageRootFolder(), package.PackageId);
            var results = _localResourceStore.StorePackageFontWithResult(sourcePath, package.PackageId, packageRoot);
            var first = results.FirstOrDefault();
            if (first is null)
            {
                SetPropertyEditError(item, "UnsupportedFontFormat", sourcePath);
                return false;
            }

            _propertyGridBuilder.ClearFontFamilyOptionCache();
            item.Options = _propertyGridBuilder.GetFontFamilyOptions();
            item.Value = first.ResourceUri;
            item.EditText = first.FontFamilyName;
            return ApplyPropertyEdit(item, first.ResourceUri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to import package font.");
            SetPropertyEditError(item, $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Editor.ImportFontFailed")}: {ex.Message}", sourcePath);
            return false;
        }
    }

    /// <summary>
    /// 刷新当前可见字体族编辑行的字体选项。
    /// </summary>
    /// <summary>
    /// 包字体变化后重建字体族编辑器选项。
    /// </summary>
    public void RefreshFontFamilyEditorOptions()
    {
        _propertyGridBuilder.ClearFontFamilyOptionCache();
        var options = _propertyGridBuilder.GetFontFamilyOptions();
        foreach (var item in PropertyEditorItems.Where(item => item.EditorKind == FrontedPropertyEditorKind.FontFamily))
        {
            item.Options = options;
        }
    }

    [RelayCommand]
    private void ResetWindowOptions()
    {
        if (SelectedWindow is null)
        {
            return;
        }

        _ = ResetWindowOptionsAsync();
    }
    private void LoadWindowOptions(string windowTypeName)
    {
        WindowOptionsWindowTypeName = $"{ResolveWindowOptionDisplayName(windowTypeName)} ({windowTypeName})";
        _isLoadingWindowOptions = true;
        try
        {
            var settings = _currentWindowSettings;
            WindowAllowTransparency = settings.AllowsTransparency;
            WindowWidthEditText = settings.WindowWidth.ToString("0.##", CultureInfo.InvariantCulture);
            WindowHeightEditText = settings.WindowHeight.ToString("0.##", CultureInfo.InvariantCulture);

            var configuredBackgroundColor = settings.BackgroundColor;
            _windowBackgroundColorConfigured = !string.IsNullOrWhiteSpace(configuredBackgroundColor);
            var backgroundColor = configuredBackgroundColor ?? "#00000000";
            if (!FrontedPropertyColorHelper.TryParseArgbColor(configuredBackgroundColor, out var color))
            {
                backgroundColor = "#00000000";
                color = Colors.Transparent;
                _windowBackgroundColorConfigured = false;
            }
            else
            {
                backgroundColor = FrontedPropertyColorHelper.ToArgbString(color);
            }

            WindowBackgroundColorEditText = backgroundColor;
            WindowBackgroundColorValue = color;
            WindowOptionsStatus = string.Empty;
        }
        finally
        {
            _isLoadingWindowOptions = false;
        }
    }

    public async Task<bool> ApplyWindowBackgroundColorEditAsync()
    {
        if (!FrontedPropertyColorHelper.TryParseArgbColor(WindowBackgroundColorEditText, out var color))
        {
            WindowOptionsStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Validation.InvalidArgbColor");
            return false;
        }

        WindowBackgroundColorEditText = FrontedPropertyColorHelper.ToArgbString(color);
        WindowBackgroundColorValue = color;
        _windowBackgroundColorConfigured = true;
        await SaveWindowOptionsAsync(
            restartWindowForTransparencyChange: false,
            applyBackgroundImmediately: true);
        return true;
    }

    public bool ApplyTextBindingEdit(
        FrontedPropertyEditorItem item,
        Core.Models.FrontedLayout.Binding.FrontedTextBindingExpression expression)
    {
        if (CurrentDocument is null || SelectedDesignItem is null || item.IsReadOnly)
        {
            return false;
        }

        if (!item.IsMultiSelectionBatchEditable || item.IsMultiSelectionMixedValue)
        {
            return false;
        }

        var property = SelectedDesignItem.Config.GetType().GetProperty(item.PropertyName);
        if (property?.PropertyType != typeof(Core.Models.FrontedLayout.Binding.FrontedTextBindingExpression)
            || !property.CanWrite)
        {
            return false;
        }

        var oldJson = JsonSerializer.Serialize(property.GetValue(SelectedDesignItem.Config));
        var newJson = JsonSerializer.Serialize(expression);
        if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
        {
            return true;
        }

        CaptureUndoSnapshot();
        property.SetValue(SelectedDesignItem.Config, expression);
        item.Value = expression;
        item.DisplayValue = expression.GetActiveSources().Count == 0
            ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.TextBinding.None")
            : string.Format(
                CultureInfo.CurrentCulture,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.TextBinding.SourceSummary"),
                expression.GetActiveSources().Count,
                string.Join(", ", expression.GetActiveSources().Select(source => source.Path)));
        CurrentDocument.IsDirty = true;
        FinishPropertyEdit(item.PropertyName);
        return true;
    }

    private async Task SaveWindowOptionsAsync(
        bool restartWindowForTransparencyChange,
        bool applyBackgroundImmediately,
        bool applyWindowSizeImmediately = false)
    {
        if (SelectedWindow is null)
        {
            return;
        }

        try
        {
            var windowWidth = TryParseOptionalPositiveDouble(WindowWidthEditText);
            var windowHeight = TryParseOptionalPositiveDouble(WindowHeightEditText);
            var settings = CloneWindowSettings(_currentWindowSettings);
            settings.WindowWidth = windowWidth ?? settings.WindowWidth;
            settings.WindowHeight = windowHeight ?? settings.WindowHeight;
            settings.AllowsTransparency = WindowAllowTransparency;
            settings.BackgroundColor = _windowBackgroundColorConfigured
                ? WindowBackgroundColorEditText
                : null;

            var config = await _layoutService.LoadWindowConfigAsync(SelectedWindow.WindowTypeName);
            config.WindowSettings = CloneWindowSettings(settings);

            await _layoutService.SaveWindowConfigAsync(
                SelectedWindow.WindowTypeName,
                config);
            _currentWindowSettings = CloneWindowSettings(settings);

            if (applyBackgroundImmediately)
            {
                await (_frontedWindowService?.ApplyWindowBackgroundColorAsync(SelectedWindow.WindowTypeName) ?? Task.FromResult(false));
            }

            if (applyWindowSizeImmediately)
            {
                await (_frontedWindowService?.ApplyWindowSizeAsync(SelectedWindow.WindowTypeName) ?? Task.FromResult(false));
            }

            if (applyWindowSizeImmediately)
            {
                await (_frontedWindowService?.ReloadFrontedLayoutsAsync() ?? Task.CompletedTask);
            }

            if (restartWindowForTransparencyChange)
            {
                await (_frontedWindowService?.RestartWindowForTransparencyChangeAsync(SelectedWindow.WindowTypeName)
                       ?? Task.FromResult(false));
            }

            WindowOptionsStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "WindowOptionsApplied");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save fronted window layout options.");
            WindowOptionsStatus = ex.Message;
        }
    }

    private async Task ResetWindowOptionsAsync()
    {
        if (SelectedWindow is null)
        {
            return;
        }

        try
        {
            FrontedWindowConfig? builtInConfig = null;
            var builtInPath = GetBuiltInPackageLayoutPath(SelectedWindow.WindowTypeName);
            if (File.Exists(builtInPath))
            {
                builtInConfig = JsonSerializer.Deserialize<FrontedWindowConfig>(
                    await File.ReadAllTextAsync(builtInPath));
            }
            _currentWindowSettings = CloneWindowSettings(builtInConfig?.WindowSettings ?? new FrontedWindowSettings());
            var config = await _layoutService.LoadWindowConfigAsync(SelectedWindow.WindowTypeName);
            config.WindowSettings = CloneWindowSettings(_currentWindowSettings);
            await _layoutService.SaveWindowConfigAsync(SelectedWindow.WindowTypeName, config);
            LoadWindowOptions(SelectedWindow.WindowTypeName);
            await (_frontedWindowService?.RestartWindowForTransparencyChangeAsync(SelectedWindow.WindowTypeName)
                   ?? Task.FromResult(false));
            WindowOptionsStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "WindowOptionsApplied");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset fronted window layout options.");
            WindowOptionsStatus = ex.Message;
        }
    }

}
