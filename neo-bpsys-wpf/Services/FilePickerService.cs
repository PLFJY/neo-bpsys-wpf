using Microsoft.Win32;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Tutorial;
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
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ImageFiles")} (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff;*.svg;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff;*.svg;*.webp",
        };

        return openFileDialog.ShowDialog() != true ? null : openFileDialog.FileName;
    }

    /// <inheritdoc />
    public string? PickFontFile()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "FontFiles")} (*.ttf;*.otf;*.ttc)|*.ttf;*.otf;*.ttc",
        };

        return openFileDialog.ShowDialog() != true ? null : openFileDialog.FileName;
    }

    /// <summary>
    /// 选择JSON文件
    /// </summary>
    /// <returns>返回JSON文件路径</returns>
    public string? PickJsonFile(string? initialDirectory = null)
    {
        var tutorialHint = TutorialFilePickerHints.ConsumeNextJsonPickerHint();
        var resolvedInitialDirectory = ResolveExistingDirectory(
            initialDirectory
            ?? tutorialHint.InitialDirectory
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Examples"));

        OpenFileDialog openFileDialog = new()
        {
            Filter = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "JSONFiles")} (*.json) | *.json",
            DefaultDirectory = resolvedInitialDirectory,
            InitialDirectory = resolvedInitialDirectory,
        };

        if (!string.IsNullOrWhiteSpace(tutorialHint.Title))
        {
            openFileDialog.Title = tutorialHint.Title;
        }

        return openFileDialog.ShowDialog() != true ? null : openFileDialog.FileName;
    }

    private static string ResolveExistingDirectory(string directory)
    {
        return Directory.Exists(directory)
            ? directory
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
    }

    /// <summary>
    /// 选择 ZIP 文件。
    /// </summary>
    /// <returns>返回 ZIP 文件路径。</returns>
    public string? PickZipFile()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ZipFiles")} (*.zip) | *.zip",
        };

        return openFileDialog.ShowDialog() != true ? null : openFileDialog.FileName;
    }

    /// <inheritdoc />
    public string? PickSmartBpModuleArchiveFile()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter =
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModuleArchiveFiles")} (*.7z;*.zip)|*.7z;*.zip|{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "SevenZipArchiveFiles")} (*.7z)|*.7z|{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ZipFiles")} (*.zip)|*.zip|{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "AllFiles")} (*.*)|*.*",
        };

        return openFileDialog.ShowDialog() != true ? null : openFileDialog.FileName;
    }

    /// <inheritdoc />
    public string? PickPluginPackageFile()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter =
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginPackageFiles")} (*.7z;*.zip)|*.7z;*.zip|{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "SevenZipArchiveFiles")} (*.7z)|*.7z|{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ZipFiles")} (*.zip)|*.zip|{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "AllFiles")} (*.*)|*.*",
        };

        return openFileDialog.ShowDialog() != true ? null : openFileDialog.FileName;
    }

    /// <inheritdoc />
    public string? PickExecutableFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ExecutableFiles")} (*.exe)|*.exe|{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "AllFiles")} (*.*)|*.*"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog();
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    /// <summary>
    /// 选择 BPUI 文件。
    /// </summary>
    /// <returns>返回 BPUI 文件路径。</returns>
    public string? PickBpuiFile()
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "BpuiFiles")} (*.bpui) |*.bpui|{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ZipFiles")} (*.zip) | *.zip|All Files(*.*)|*.*",
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
            Filter = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "JSONFiles")} (*.json) | *.json",
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
            Filter = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "BpuiFiles")} (*.bpui) |*.bpui|All Files(*.*)|*.*",
            DefaultExt = ".bpui",
            AddExtension = true,
            DefaultDirectory = AppConstants.AppOutputPath,
            Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "SaveAs"),
            FileName = string.IsNullOrWhiteSpace(defaultFileName) ? "saved_ui" : defaultFileName,
            OverwritePrompt = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}


