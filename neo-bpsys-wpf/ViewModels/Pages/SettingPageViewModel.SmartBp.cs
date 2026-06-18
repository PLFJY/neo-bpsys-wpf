using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.SmartBpModule;
using System.IO;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SettingPageViewModel
{
    /// <summary>
    /// SmartBP 模块安装和加载目录。
    /// </summary>
    [ObservableProperty]
    private string _smartBpModuleRoot = string.Empty;

    /// <summary>
    /// SmartBP 模块路径设置状态文本。
    /// </summary>
    [ObservableProperty]
    private string _smartBpModulePathStatus = string.Empty;

    /// <summary>
    /// 选择 SmartBP 模块目录。
    /// </summary>
    [RelayCommand]
    private void BrowseSmartBpModuleRoot()
    {
        var folder = _filePickerService.PickFolder();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        SmartBpModuleRoot = folder;
    }

    /// <summary>
    /// 保存 SmartBP 模块目录偏好。
    /// </summary>
    [RelayCommand]
    private async Task SaveSmartBpModuleRootAsync()
    {
        if (string.IsNullOrWhiteSpace(SmartBpModuleRoot) ||
            !Path.IsPathFullyQualified(SmartBpModuleRoot) ||
            SmartBpModuleManager.IsUnsafeInstallPath(SmartBpModuleRoot))
        {
            SmartBpModulePathStatus = I18nHelper.GetLocalizedString("SmartBpModulePathInvalid");
            _logger.LogWarning("Rejected invalid SmartBP module root from settings: {ModuleRoot}", SmartBpModuleRoot);
            return;
        }

        var normalizedRoot = Path.GetFullPath(SmartBpModuleRoot);
        SmartBpModuleRoot = normalizedRoot;
        SmartBpModulePathStatus = I18nHelper.GetLocalizedString(
            IsDebugBuild()
                ? "SmartBpModuleDevelopmentPathSaving"
                : "SmartBpModulePathMigrating");
        if (await _smartBpModuleManager.MigrateModuleRootPreferenceAsync(normalizedRoot))
        {
            SmartBpModulePathStatus = I18nHelper.GetLocalizedString(
                IsDebugBuild()
                    ? "SmartBpModuleDevelopmentPathSaved"
                    : "SmartBpModulePathMigrationPrepared");
            _logger.LogInformation(
                IsDebugBuild()
                    ? "SmartBP development module root saved from settings without copying files: {ModuleRoot}"
                    : "SmartBP module root migration requested from settings: {ModuleRoot}",
                normalizedRoot);
            return;
        }

        SmartBpModulePathStatus = string.IsNullOrWhiteSpace(_smartBpModuleManager.LastFailureMessage)
            ? I18nHelper.GetLocalizedString("SmartBpModulePathMigrationFailed")
            : $"{I18nHelper.GetLocalizedString("SmartBpModulePathMigrationFailed")}{_smartBpModuleManager.LastFailureMessage}";
        _logger.LogWarning(
            "SmartBP module root migration request failed from settings. ModuleRoot={ModuleRoot}, Error={Error}",
            normalizedRoot,
            _smartBpModuleManager.LastFailureMessage);
    }

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// 将 SmartBP 模块目录恢复为默认路径。
    /// </summary>
    [RelayCommand]
    private async Task ResetSmartBpModuleRootAsync()
    {
        SmartBpModuleRoot = SmartBpModuleManager.GetDefaultModuleRoot();
        await SaveSmartBpModuleRootAsync();
    }

    /// <summary>
    /// 导入 SmartBP 模块归档并安装到当前模块目录。
    /// </summary>
    [RelayCommand]
    private async Task ImportSmartBpModuleArchiveAsync()
    {
        var archivePath = _filePickerService.PickSmartBpModuleArchiveFile();
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SmartBpModuleRoot) ||
            !Path.IsPathFullyQualified(SmartBpModuleRoot) ||
            SmartBpModuleManager.IsUnsafeInstallPath(SmartBpModuleRoot))
        {
            SmartBpModulePathStatus = I18nHelper.GetLocalizedString("SmartBpModulePathInvalid");
            _logger.LogWarning("Rejected invalid SmartBP module import target from settings: {ModuleRoot}", SmartBpModuleRoot);
            return;
        }

        var normalizedRoot = Path.GetFullPath(SmartBpModuleRoot);
        SmartBpModuleRoot = normalizedRoot;
        SmartBpModulePathStatus = I18nHelper.GetLocalizedString("SmartBpModuleArchiveImporting");
        try
        {
            if (await _smartBpModuleManager.ImportArchiveAsync(archivePath, normalizedRoot, "SettingsArchiveImport"))
            {
                SmartBpModulePathStatus = I18nHelper.GetLocalizedString("SmartBpModuleArchiveImportSucceeded");
                _logger.LogInformation(
                    "SmartBP module archive imported from settings. ArchivePath={ArchivePath}, ModuleRoot={ModuleRoot}",
                    archivePath,
                    normalizedRoot);
                return;
            }

            SmartBpModulePathStatus = string.IsNullOrWhiteSpace(_smartBpModuleManager.LastFailureMessage)
                ? I18nHelper.GetLocalizedString("SmartBpModuleArchiveImportFailed")
                : $"{I18nHelper.GetLocalizedString("SmartBpModuleArchiveImportFailed")}{_smartBpModuleManager.LastFailureMessage}";
            _logger.LogWarning(
                "SmartBP module archive import failed from settings. ArchivePath={ArchivePath}, ModuleRoot={ModuleRoot}, Error={Error}",
                archivePath,
                normalizedRoot,
                _smartBpModuleManager.LastFailureMessage);
        }
        catch (Exception ex)
        {
            SmartBpModulePathStatus = $"{I18nHelper.GetLocalizedString("SmartBpModuleArchiveImportFailed")}{ex.Message}";
            _logger.LogWarning(
                ex,
                "SmartBP module archive import threw from settings. ArchivePath={ArchivePath}, ModuleRoot={ModuleRoot}",
                archivePath,
                normalizedRoot);
        }
    }
}
