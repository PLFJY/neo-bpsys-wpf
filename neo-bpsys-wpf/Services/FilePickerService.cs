using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Helpers;
using System.IO;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 文件选择服务，实现了 <see cref="IFilePickerService"/> 接口
/// 用于封装文件选择操作
/// </summary>
public class FilePickerService : IFilePickerService
{
    /// <summary>
    /// 选择图片
    /// </summary>
    /// <returns>返回图片文件路径</returns>
    public string? PickImage()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter =
                $"{I18nHelper.GetLocalizedString("ImageFiles")} (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff;*.svg;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff;*.svg;*.webp",
        };

        return openFileDialog.ShowDialog() != true ? null : openFileDialog.FileName;
    }

    /// <summary>
    /// 选择JSON文件
    /// </summary>
    /// <returns>返回JSON文件路径</returns>
    public string? PickJsonFile()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = $"{I18nHelper.GetLocalizedString("JSONFiles")} (*.json) | *.json",
            DefaultDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
        };

        return openFileDialog.ShowDialog() != true ? null : openFileDialog.FileName;
    }

    /// <summary>
    /// 选择 ZIP 文件。
    /// </summary>
    /// <returns>返回 ZIP 文件路径。</returns>
    public string? PickZipFile()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = $"{I18nHelper.GetLocalizedString("ZipFiles")} (*.zip) | *.zip",
        };

        return openFileDialog.ShowDialog() != true ? null : openFileDialog.FileName;
    }

    /// <summary>
    /// 选择 BPUI 文件。
    /// </summary>
    /// <returns>返回 BPUI 文件路径。</returns>
    public string? PickBpuiFile()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = $"{I18nHelper.GetLocalizedString("BpuiFiles")} (*.bpui) |*.bpui|{I18nHelper.GetLocalizedString("ZipFiles")} (*.zip) | *.zip|All Files(*.*)|*.*",
        };

        return openFileDialog.ShowDialog() != true ? null : openFileDialog.FileName;
    }

    /// <summary>
    /// 选择 JSON 导出保存路径。
    /// </summary>
    /// <param name="defaultFileName">默认文件名。</param>
    /// <returns>返回导出文件路径；取消时返回 <see langword="null"/>。</returns>
    public string? SaveJsonFile(string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = $"{I18nHelper.GetLocalizedString("JSONFiles")} (*.json) | *.json",
            FileName = string.IsNullOrWhiteSpace(defaultFileName) ? "config.json" : defaultFileName,
            DefaultExt = ".json",
            AddExtension = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>
    /// 选择 BPUI 导出保存路径。
    /// </summary>
    /// <param name="defaultFileName">默认文件名。</param>
    /// <returns>返回导出文件路径；取消时返回 <see langword="null"/>。</returns>
    public string? SaveBpuiFile(string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = $"{I18nHelper.GetLocalizedString("BpuiFiles")} (*.bpui) |*.bpui|All Files(*.*)|*.*",
            DefaultExt = ".bpui",
            AddExtension = true,
            DefaultDirectory = AppConstants.AppOutputPath,
            Title = I18nHelper.GetLocalizedString("SaveAs"),
            FileName = string.IsNullOrWhiteSpace(defaultFileName) ? "saved_ui" : defaultFileName,
            OverwritePrompt = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}


