using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.SmartBpModule;
using System.IO;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SettingPageViewModel
{
    /// <summary>
    /// SmartBP 模块安装和加载目录。
    /// </summary>
    [ObservableProperty]
    public partial string SmartBpModuleRoot { get; set; } = string.Empty;

    /// <summary>
    /// SmartBP 模块路径设置状态文本。
    /// </summary>
    [ObservableProperty]
    public partial string SmartBpModulePathStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsModuleExtracting { get; set; } = false;

    /// <summary>
    /// 获取是否为调试构建，用于控制 SmartBP 调试工具的可见性。
    /// </summary>
    public bool IsDebugBuild
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 调试用：切换 SmartBP 模块版本过旧状态。
    /// </summary>
    [RelayCommand]
    private void ToggleSmartBpVersionOutdatedDebug()
    {
#if DEBUG
        var target = !_smartBpModuleManager.IsModuleVersionOutdated;
        _smartBpModuleManager.SetVersionOutdatedForDebug(target);
        SmartBpModulePathStatus = target
            ? "[DEBUG] SmartBP 模块已标记为版本过旧"
            : "[DEBUG] SmartBP 模块版本过旧标记已清除";
#endif
    }

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
            SmartBpModulePathStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathInvalid");
            _logger.LogWarning("Rejected invalid SmartBP module root from settings: {ModuleRoot}", SmartBpModuleRoot);
            return;
        }

        var normalizedRoot = Path.GetFullPath(SmartBpModuleRoot);
        SmartBpModuleRoot = normalizedRoot;

        var migrationChoice = await ConfirmSmartBpModuleMigrationAsync(normalizedRoot);
        if (migrationChoice == null)
        {
            SmartBpModulePathStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathSaveCanceled");
            return;
        }

        if (migrationChoice == false)
        {
            _smartBpModuleManager.PersistModuleRootPreference(normalizedRoot);
            SmartBpModulePathStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathSavedWithoutMigration");
            _logger.LogInformation(
                "SmartBP module root saved from settings without migrating files: {ModuleRoot}",
                normalizedRoot);
            return;
        }

        SmartBpModulePathStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathMigrating");
        if (await _smartBpModuleManager.MigrateModuleRootPreferenceAsync(normalizedRoot))
        {
            SmartBpModulePathStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathMigrationPrepared");
            _logger.LogInformation(
                "SmartBP module root migration requested from settings: {ModuleRoot}",
                normalizedRoot);
            return;
        }

        SmartBpModulePathStatus = string.IsNullOrWhiteSpace(_smartBpModuleManager.LastFailureMessage)
            ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathMigrationFailed")
            : $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathMigrationFailed")}{_smartBpModuleManager.LastFailureMessage}";
        _logger.LogWarning(
            "SmartBP module root migration request failed from settings. ModuleRoot={ModuleRoot}, Error={Error}",
            normalizedRoot,
            _smartBpModuleManager.LastFailureMessage);
    }

    private async Task<bool?> ConfirmSmartBpModuleMigrationAsync(string normalizedTarget)
    {
        var sourceRoot = _smartBpModuleManager.ReadState()?.ModuleRoot;
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            return true;
        }

        var normalizedSource = Path.GetFullPath(sourceRoot);
        if (string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var result = await MessageBoxHelper.ShowThreeOptionAsync(
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathMigrationChoiceMessage"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathMigrationChoiceTitle"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathMigrateFiles"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathSaveOnly"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"),
            primaryButtonIcon: SymbolRegular.ArrowSync24,
            secondaryButtonIcon: SymbolRegular.Save24);

        return result switch
        {
            MessageBoxResult.Primary => true,
            MessageBoxResult.Secondary => false,
            _ => null
        };
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
    /// 自动从主程序所在目录向上查找 SmartBP 模块项目的 Debug 输出目录并设置为模块路径。
    /// </summary>
    [RelayCommand]
    private async Task SetSmartBpModuleRootToDebugPathAsync()
    {
        var debugPath = FindSmartBpModuleDebugOutput();
        if (debugPath == null)
        {
            SmartBpModulePathStatus = I18nHelper.GetLocalizedString(
                AppI18nDictionaries.Settings,
                "SmartBpModuleDebugPathNotFound");
            _logger.LogWarning(
                "Could not locate SmartBP module debug output directory from base directory: {BaseDirectory}",
                AppContext.BaseDirectory);
            return;
        }

        SmartBpModuleRoot = debugPath;
        _logger.LogInformation("Located SmartBP module debug output directory: {DebugPath}", debugPath);
        await SaveSmartBpModuleRootAsync();
    }

    /// <summary>
    /// 从主程序基础目录向上查找 SmartBP 模块项目的 Debug 输出目录。
    /// 优先返回包含 component.json 的完整输出目录，其次返回仅包含入口程序集的开发目录。
    /// </summary>
    /// <returns>找到的 Debug 输出目录完整路径；未找到时返回 <see langword="null"/>。</returns>
    private static string? FindSmartBpModuleDebugOutput()
    {
        const string moduleProjectName = "neo-bpsys-wpf.SmartBp.Module";
        var entryAssembly = SmartBpModuleConstants.EntryAssemblyName;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        for (int i = 0; i < 8 && dir != null; i++)
        {
            var binDebugDirs = new[]
            {
                Path.Combine(dir.FullName, moduleProjectName, "bin", "Debug"),
                Path.Combine(dir.FullName, moduleProjectName, "bin", "x64", "Debug"),
            };

            foreach (var binDebugDir in binDebugDirs)
            {
                if (!Directory.Exists(binDebugDir))
                    continue;

                try
                {
                    string? withManifest = null;
                    string? withoutManifest = null;
                    foreach (var entry in Directory.EnumerateFiles(binDebugDir, entryAssembly, SearchOption.AllDirectories))
                    {
                        var candidate = Path.GetDirectoryName(entry);
                        if (candidate == null)
                            continue;
                        if (File.Exists(Path.Combine(candidate, "component.json")))
                        {
                            withManifest = candidate;
                            break;
                        }

                        withoutManifest ??= candidate;
                    }

                    if (withManifest != null)
                        return withManifest;
                    if (withoutManifest != null)
                        return withoutManifest;
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }
            }

            dir = dir.Parent;
        }

        return null;
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
            SmartBpModulePathStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModulePathInvalid");
            _logger.LogWarning("Rejected invalid SmartBP module import target from settings: {ModuleRoot}", SmartBpModuleRoot);
            return;
        }

        var normalizedRoot = Path.GetFullPath(SmartBpModuleRoot);
        SmartBpModuleRoot = normalizedRoot;
        SmartBpModulePathStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModuleArchiveImporting");
        IsModuleExtracting = true;
        try
        {
            if (await _smartBpModuleManager.ImportArchiveAsync(archivePath, normalizedRoot, "SettingsArchiveImport"))
            {
                if (_smartBpModuleManager.IsRestartRequiredForPendingModuleImport)
                {
                    _globalRestartService.IsRestartRequired = true;
                    SmartBpModulePathStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModuleArchiveImportRestartPrepared");
                    return;
                }

                SmartBpModulePathStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModuleArchiveImportSucceeded");
                _logger.LogInformation(
                    "SmartBP module archive imported from settings. ArchivePath={ArchivePath}, ModuleRoot={ModuleRoot}",
                    archivePath,
                    normalizedRoot);
                return;
            }

            SmartBpModulePathStatus = string.IsNullOrWhiteSpace(_smartBpModuleManager.LastFailureMessage)
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModuleArchiveImportFailed")
                : $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModuleArchiveImportFailed")}{_smartBpModuleManager.LastFailureMessage}";
            _logger.LogWarning(
                "SmartBP module archive import failed from settings. ArchivePath={ArchivePath}, ModuleRoot={ModuleRoot}, Error={Error}",
                archivePath,
                normalizedRoot,
                _smartBpModuleManager.LastFailureMessage);
        }
        catch (Exception ex)
        {
            SmartBpModulePathStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "SmartBpModuleArchiveImportFailed")}{ex.Message}";
            _logger.LogWarning(
                ex,
                "SmartBP module archive import threw from settings. ArchivePath={ArchivePath}, ModuleRoot={ModuleRoot}",
                archivePath,
                normalizedRoot);
        }
        finally
        {
            IsModuleExtracting = false;
        }
    }

}
