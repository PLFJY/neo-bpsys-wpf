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
/// Fronted Designer 的动画部件与图片导入业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    partial void OnSelectedAnimationPartChanged(FrontedAnimationPartConfig? value)
    {
        if (AnimationPartEditBuffer is not null)
        {
            AnimationPartEditBuffer.ErrorsChanged -= AnimationPartEditBuffer_OnErrorsChanged;
        }

        AnimationPartEditBuffer = value is null
            ? null
            : new FrontedAnimationPartEditorViewModel(value, candidate =>
                GetSelectedBehaviorSet(create: false)?.AnimationParts.All(item =>
                    ReferenceEquals(item, value)
                    || !string.Equals(item.Name, candidate, StringComparison.OrdinalIgnoreCase)) == true);
        if (AnimationPartEditBuffer is not null)
        {
            AnimationPartEditBuffer.ErrorsChanged += AnimationPartEditBuffer_OnErrorsChanged;
        }

        RemoveAnimationPartCommand.NotifyCanExecuteChanged();
        ApplyAnimationPartEditCommand.NotifyCanExecuteChanged();
    }

    private void AnimationPartEditBuffer_OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
    {
        ApplyAnimationPartEditCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddAnimationPart()
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        var set = GetSelectedBehaviorSet(create: true);
        if (set is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var names = set.AnimationParts
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 1;
        var defaultName = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.DefaultName");
        var name = defaultName;
        while (names.Contains(name))
        {
            name = $"{defaultName}{index++}";
        }

        var created = new FrontedAnimationPartConfig
        {
            Name = name,
            Kind = FrontedAnimationPartKind.Rectangle,
            Layer = FrontedAnimationPartLayer.AboveContent,
            Width = 4,
            HeightText = "100%",
            Fill = "#FFFFFFFF",
            Opacity = 1D,
            Visibility = "Hidden",
            ZIndex = 10,
            IsHitTestVisible = false
        };
        set.AnimationParts.Add(created);
        MarkBehaviorsDirty();
        RebuildAnimationPartEditorItems(created);
        FinishBehaviorPartEdit();
    }

    private bool CanRemoveAnimationPart() => SelectedAnimationPart is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveAnimationPart))]
    private void RemoveAnimationPart()
    {
        if (CurrentDocument is null || SelectedDesignItem is null || SelectedAnimationPart is null)
        {
            return;
        }

        var set = GetSelectedBehaviorSet(create: false);
        if (set is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        set.AnimationParts.Remove(SelectedAnimationPart);
        MarkBehaviorsDirty();
        RebuildAnimationPartEditorItems();
        FinishBehaviorPartEdit();
    }

    private bool CanApplyAnimationPartEdit() =>
        SelectedAnimationPart is not null
        && AnimationPartEditBuffer is { HasErrors: false };

    [RelayCommand(CanExecute = nameof(CanApplyAnimationPartEdit))]
    private void ApplyAnimationPartEdit()
    {
        if (CurrentDocument is null
            || SelectedDesignItem is null
            || SelectedAnimationPart is null
            || AnimationPartEditBuffer is null)
        {
            return;
        }

        AnimationPartEditBuffer.ValidateAll();
        if (AnimationPartEditBuffer.HasErrors)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.Validation.FixErrors");
            return;
        }

        CaptureUndoSnapshot();
        AnimationPartEditBuffer.ApplyTo(SelectedAnimationPart);
        MarkBehaviorsDirty();
        RebuildAnimationPartEditorItems(SelectedAnimationPart);
        FinishBehaviorPartEdit();
    }

    private void RebuildAnimationPartEditorItems(FrontedAnimationPartConfig? selected = null)
    {
        AnimationPartEditorItems.Clear();
        foreach (var item in GetSelectedBehaviorSet(create: false)?.AnimationParts ?? [])
        {
            AnimationPartEditorItems.Add(item);
        }

        SelectedAnimationPart = selected ?? AnimationPartEditorItems.FirstOrDefault();
    }

    private ControlBehaviorSet? GetSelectedBehaviorSet(bool create)
    {
        if (SelectedDesignItem is null)
        {
            return null;
        }

        if (SelectedDesignItem.Config.BehaviorGuid == Guid.Empty)
        {
            if (!create)
            {
                return null;
            }

            SelectedDesignItem.Config.BehaviorGuid = FrontedBehaviorGuidHelper.NewGuid();
            CurrentDocument!.IsDirty = true;
        }

        return create
            ? BehaviorPanel.CurrentDocument.GetOrCreateSet(SelectedDesignItem.Config.BehaviorGuid, SelectedDesignItem.Name)
            : BehaviorPanel.CurrentDocument.FindSet(SelectedDesignItem.Config.BehaviorGuid);
    }

    private void FinishBehaviorPartEdit()
    {
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
        _previewAnimationScope?.RefreshTargets();
    }

    /// <summary>
    /// 根据资源浏览器选择更新选中动画部件的图片编辑缓冲。
    /// </summary>
    /// <param name="selectedResourcePath">选中的内置、包内或绝对图片路径。</param>
    /// <returns>编辑缓冲已更新时返回 <see langword="true"/>。</returns>
    public bool ApplyAnimationPartImageResourceSelection(string selectedResourcePath)
    {
        if (AnimationPartEditBuffer is not { IsImage: true } editor)
        {
            return false;
        }

        if (!IsAbsoluteFilePath(selectedResourcePath))
        {
            editor.ImagePath = selectedResourcePath;
            return true;
        }

        return StoreLocalAnimationPartImage(selectedResourcePath);
    }

    /// <summary>
    /// 根据资源浏览器选择更新选中动画部件的图片编辑缓冲，并在本地超限图片时提供压缩选项。
    /// </summary>
    /// <param name="selectedResourcePath">选中的内置、包内或绝对图片路径。</param>
    /// <returns>编辑缓冲已更新时返回 <see langword="true"/>。</returns>
    public async Task<bool> ApplyAnimationPartImageResourceSelectionAsync(string selectedResourcePath)
    {
        if (AnimationPartEditBuffer is not { IsImage: true } editor)
        {
            return false;
        }

        if (!IsAbsoluteFilePath(selectedResourcePath))
        {
            editor.ImagePath = selectedResourcePath;
            return true;
        }

        return await StoreLocalAnimationPartImageAsync(selectedResourcePath);
    }

    /// <summary>
    /// 在导入本地图片到资源存储前进行预校验，校验失败时返回本地化的错误消息。
    /// 与 <see cref="FrontedLocalResourceStore.StoreImageWithResult"/> 内部使用的
    /// <see cref="FrontedImagePurpose.Background"/> 限制保持一致。
    /// </summary>
    /// <param name="sourcePath">本地图片的绝对路径。</param>
    /// <returns>校验失败时返回错误消息；校验通过或未注入校验服务时返回 <see langword="null"/>。</returns>
    private string? ValidateLocalImageForStorage(string sourcePath)
    {
        if (_imageSafetyService is null)
        {
            return null;
        }

        var validation = _imageSafetyService.ValidateFile(sourcePath, FrontedImagePurpose.Background);
        if (validation.IsValid)
        {
            return null;
        }

        return BuildImageValidationFailureMessage(validation);
    }

    private async Task<(FrontedLocalResourceStoreResult? Result, string? ErrorMessage)> StoreLocalImageWithOptionalCompressionAsync(
        string sourcePath)
    {
        if (_localResourceStore is null)
        {
            return (null, "Local resource store is unavailable.");
        }

        var validation = _imageSafetyService?.ValidateFile(sourcePath, FrontedImagePurpose.Background);
        var compressOversizedImage = false;
        if (validation is { IsValid: false })
        {
            if (validation.ErrorCode is not ("ImageTooLarge" or "ImageTooManyPixels"))
            {
                return (null, BuildImageValidationFailureMessage(validation));
            }

            var compress = await MessageBoxHelper.ShowConfirmAsync(
                string.Format(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ImageCompressionMessage"),
                    BuildImageValidationFailureMessage(validation)),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ImageCompressionTitle"),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "CompressAndApplyImage"),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"));
            if (!compress)
            {
                return (null, BuildImageValidationFailureMessage(validation));
            }

            compressOversizedImage = true;
        }

        try
        {
            return (await Task.Run(
                () => _localResourceStore.StoreImageWithResult(sourcePath, compressOversizedImage)), null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store local fronted image.");
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// 根据图片校验结果构建本地化的错误消息。文件大小超限和图片尺寸超限时
    /// 返回包含实际值与目标压缩值的友好提示。
    /// </summary>
    /// <param name="validation">图片校验结果。</param>
    /// <returns>用于错误提示的本地化消息。</returns>
    private static string BuildImageValidationFailureMessage(FrontedImageValidationResult validation)
    {
        if (validation is { IsValid: false, ErrorCode: "ImageTooLarge" })
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ImageFileTooLarge"),
                FormatFileSize(validation.FileBytes),
                FormatFileSize(FrontedLayoutLimits.MaxBackgroundImageBytes));
        }

        if (validation is { IsValid: false, ErrorCode: "ImageTooManyPixels" })
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ImageDimensionsTooLarge"),
                validation.PixelWidth,
                validation.PixelHeight,
                FrontedLayoutLimits.MaxBackgroundImageLongSide);
        }

        return validation.ErrorMessage ?? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture");
    }

    /// <summary>
    /// 将字节数格式化为带二进制单位（B / KiB / MiB）的可读字符串，
    /// 整数字节数省略小数部分。
    /// </summary>
    /// <param name="bytes">字节数。</param>
    /// <returns>带单位的可读大小字符串。</returns>
    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            var mib = bytes / (1024.0 * 1024);
            return Math.Abs(mib - Math.Floor(mib)) < double.Epsilon
                ? $"{(long)mib} MiB"
                : $"{mib:F2} MiB";
        }

        if (bytes >= 1024)
        {
            var kib = bytes / 1024.0;
            return Math.Abs(kib - Math.Floor(kib)) < double.Epsilon
                ? $"{(long)kib} KiB"
                : $"{kib:F2} KiB";
        }

        return $"{bytes} B";
    }

    /// <summary>
    /// 导入本地图片，并更新选中图片动画部件的编辑缓冲。
    /// </summary>
    /// <param name="sourcePath">本地图片的绝对路径。</param>
    /// <returns>图片导入并选中时返回 <see langword="true"/>。</returns>
    public bool StoreLocalAnimationPartImage(string sourcePath)
    {
        if (_localResourceStore is null || AnimationPartEditBuffer is not { IsImage: true } editor)
        {
            return false;
        }

        var validationMessage = ValidateLocalImageForStorage(sourcePath);
        if (validationMessage is not null)
        {
            StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {validationMessage}";
            return false;
        }

        try
        {
            var result = _localResourceStore.StoreImageWithResult(sourcePath);
            editor.ImagePath = result.ResourceUri;
            RecordPendingImportedResource(result, "AnimationPart ImagePath", wasApplied: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store local animation part image.");
            StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 导入本地图片，并在超限时由用户选择是否压缩后更新选中图片动画部件的编辑缓冲。
    /// </summary>
    /// <param name="sourcePath">本地图片的绝对路径。</param>
    /// <returns>图片导入并选中时返回 <see langword="true"/>。</returns>
    public async Task<bool> StoreLocalAnimationPartImageAsync(string sourcePath)
    {
        if (AnimationPartEditBuffer is not { IsImage: true } editor)
        {
            return false;
        }

        var (result, errorMessage) = await StoreLocalImageWithOptionalCompressionAsync(sourcePath);
        if (result is null)
        {
            StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {errorMessage}";
            return false;
        }

        editor.ImagePath = result.ResourceUri;
        RecordPendingImportedResource(result, "AnimationPart ImagePath", wasApplied: true);
        if (result.WasCompressed)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ImageCompressed");
        }

        return true;
    }
}
