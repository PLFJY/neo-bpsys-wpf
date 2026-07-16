using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.SmartBpModule;
using neo_bpsys_wpf.Tutorial;
using System.IO;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// SmartBP 外壳页视图模型；模块已加载时承载模块内容，否则显示安装遮罩。
/// </summary>
public partial class SmartBpPageViewModel : ViewModelBase
{
    private readonly SmartBpModuleManager _moduleManager = null!;
    private readonly IFilePickerService _filePickerService = null!;
    private readonly IInfoBarService? _infoBarService;
    private readonly ILogger<SmartBpPageViewModel>? _logger;
    private readonly IGlobalRestartService _globalRestartService = null!;

    /// <summary>
    /// 初始化 <see cref="SmartBpPageViewModel"/> 类的设计时实例。
    /// </summary>
    public SmartBpPageViewModel()
    {
        SelectedModulePath = SmartBpModuleManager.GetDefaultModuleRoot();
        ConfigureLocalOnlyOverlayForDebugOrPreview();
    }

    /// <summary>
    /// 初始化 <see cref="SmartBpPageViewModel"/> 类的新实例。
    /// </summary>
    /// <param name="moduleManager">SmartBP 模块管理器。</param>
    /// <param name="filePickerService">文件选择服务。</param>
    /// <param name="infoBarService">信息提示条服务，用于在模块版本过时等情况下向用户展示提示。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="globalRestartService">全局重启状态服务。</param>
    public SmartBpPageViewModel(
        SmartBpModuleManager moduleManager,
        IFilePickerService filePickerService,
        IInfoBarService infoBarService,
        ILogger<SmartBpPageViewModel> logger,
        IGlobalRestartService globalRestartService)
    {
        _moduleManager = moduleManager;
        _filePickerService = filePickerService;
        _infoBarService = infoBarService;
        _logger = logger;
        _globalRestartService = globalRestartService;
        SelectedModulePath = _moduleManager.GetPreferredModuleRoot();
        ConfigureLocalOnlyOverlayForDebugOrPreview();
        _moduleManager.ModuleStateChanged += (_, _) => SyncModuleState();
        _moduleManager.ModuleVersionOutdated += OnModuleVersionOutdated;
        _ = InitializeAsync();
        _ = InspectSelectedPathAsync();
    }

    /// <summary>
    /// 获取或设置 SmartBP 模块是否已加载。
    /// </summary>
    [ObservableProperty]
    public partial bool IsModuleLoaded { get; set; }

    partial void OnIsModuleLoadedChanged(bool value)
    {
        if (value)
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.SmartBpModuleLoaded);
        }
    }

    /// <summary>
    /// 获取或设置已加载的模块内容。
    /// </summary>
    [ObservableProperty]
    public partial object? ModuleContent { get; set; }

    /// <summary>
    /// 获取或设置当前选择的模块路径。
    /// </summary>
    [ObservableProperty]
    public partial string SelectedModulePath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置遮罩提示消息。
    /// </summary>
    [ObservableProperty]
    public partial string OverlayMessage { get; set; } = L("SmartBpModuleRequired");

    /// <summary>
    /// 获取或设置主按钮文本。
    /// </summary>
    [ObservableProperty]
    public partial string PrimaryActionText { get; set; } = L("SmartBpModuleDownloadAndInstall");

    /// <summary>
    /// 获取或设置已安装模块文件夹选择按钮是否可见。
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelectInstalledModuleButtonVisible { get; set; } = true;

    /// <summary>
    /// 获取或设置操作进度是否可见。
    /// </summary>
    [ObservableProperty]
    public partial bool IsProgressVisible { get; set; }

    /// <summary>
    /// 获取或设置 SmartBP 模块版本显示文本。
    /// </summary>
    [ObservableProperty]
    public partial string ModuleVersionText { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 SmartBP 模块版本文本是否可见。
    /// </summary>
    [ObservableProperty]
    public partial bool IsModuleVersionVisible { get; set; }

    /// <summary>
    /// 获取或设置当前操作进度值。
    /// </summary>
    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    /// <summary>
    /// 获取当前是否处于预览本地模式。
    /// </summary>
    public bool IsPreviewMode
    {
        get
        {
#if PREVIEW
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 获取当前是否处于调试模式。
    /// </summary>
    public bool IsDebugMode
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
    /// 尝试加载已持久化的 SmartBP 模块；调试构建下会回退到模块项目输出目录。
    /// </summary>
    /// <returns>启动加载尝试完成后结束的任务。</returns>
    private async Task InitializeAsync()
    {
        IsProgressVisible = true;
        try
        {
            if (await _moduleManager.TryLoadPersistedModuleAsync())
            {
                return;
            }

            if (IsDebugMode && !_moduleManager.HasPersistedModuleRoot())
            {
                var debugPath = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory,
                    "..", "..", "..", "..",
                    "neo-bpsys-wpf.SmartBp.Module",
                    "bin",
                    "Debug",
                    "net10.0-windows10.0.20348"));
                if (Directory.Exists(debugPath) && await _moduleManager.LoadModuleFromDirectoryAsync(debugPath, "DevelopmentDirectory"))
                {
                    return;
                }
            }
        }
        finally
        {
            IsProgressVisible = false;
        }
    }

    /// <summary>
    /// 打开文件夹选择器，并把选中的 SmartBP 模块目录写入安装路径文本框。
    /// </summary>
    [RelayCommand]
    private void BrowseModulePath()
    {
        var folder = _filePickerService.PickFolder();
        if (!string.IsNullOrWhiteSpace(folder))
            SelectedModulePath = folder;
    }

    /// <summary>
    /// 执行遮罩主操作：加载本地模块、下载并安装发布模块，或提示当前无法继续的原因。
    /// </summary>
    /// <returns>加载或安装尝试完成后结束的任务。</returns>
    [RelayCommand]
    private async Task PrimaryActionAsync()
    {
        IsProgressVisible = true;
        ProgressValue = 0;
        if (await _moduleManager.LoadModuleFromDirectoryAsync(SelectedModulePath))
        {
            IsProgressVisible = false;
            return;
        }

        if (IsDebugMode)
        {
            IsProgressVisible = false;
            OverlayMessage = BuildLocalLoadFailureMessage(
                L("SmartBpModuleDebugLocalLoadFailed"));
            return;
        }

        if (!IsPreviewMode)
        {
            var installed = await _moduleManager.DownloadAndInstallCurrentModuleAsync(
                SelectedModulePath,
                new Progress<double>(value => ProgressValue = value));
            IsProgressVisible = false;
            if (installed)
            {
                if (_moduleManager.IsRestartRequiredForPendingModuleImport)
                {
                    _globalRestartService.IsRestartRequired = true;
                    OverlayMessage = L("SmartBpModuleArchiveImportRestartPrepared");
                    await OfferSmartBpModuleArchiveImportRestartAsync();
                }

                return;
            }
        }

        IsProgressVisible = false;
        OverlayMessage = IsPreviewMode
            ? L("SmartBpModulePreviewLoadOrImportArchive")
            : L("SmartBpModuleDownloadInstallFailed");
    }

    /// <summary>
    /// 允许用户指定已安装的 SmartBP 模块文件夹，并立即尝试加载。
    /// </summary>
    /// <returns>选中文件夹完成校验和加载后结束的任务。</returns>
    [RelayCommand]
    private async Task SelectInstalledModuleFolderAsync()
    {
        var folder = _filePickerService.PickFolder();
        if (string.IsNullOrWhiteSpace(folder))
            return;

        SelectedModulePath = folder;
        if (!await _moduleManager.LoadModuleFromDirectoryAsync(folder))
        {
            OverlayMessage = IsDebugMode
                ? BuildLocalLoadFailureMessage(L("SmartBpModuleDebugFolderLoadFailed"))
                : BuildLocalLoadFailureMessage(L("SmartBpModuleSelectedDirectoryInvalid"));
        }
    }

    /// <summary>
    /// 导入打包的 SmartBP 模块压缩包；需要安全替换活动模块时提示重启。
    /// </summary>
    /// <returns>压缩包导入以及可能的重启提示处理完成后结束的任务。</returns>
    [RelayCommand]
    private async Task ImportModuleZipAsync()
    {
        var archivePath = _filePickerService.PickSmartBpModuleArchiveFile();
        if (string.IsNullOrWhiteSpace(archivePath))
            return;

        IsProgressVisible = true;
        ProgressValue = 20;
        if (await _moduleManager.ImportArchiveAsync(archivePath, SelectedModulePath))
        {
            if (_moduleManager.IsRestartRequiredForPendingModuleImport)
            {
                _globalRestartService.IsRestartRequired = true;
                OverlayMessage = L("SmartBpModuleArchiveImportRestartPrepared");
                ProgressValue = 100;
                IsProgressVisible = false;
                await OfferSmartBpModuleArchiveImportRestartAsync();
                return;
            }

            OverlayMessage = L("SmartBpModuleArchiveImportSucceeded");
        }
        else
        {
            OverlayMessage = string.IsNullOrWhiteSpace(_moduleManager.LastFailureMessage)
                ? L("SmartBpModuleArchiveImportFailed")
                : $"{L("SmartBpModuleArchiveImportFailed")}{_moduleManager.LastFailureMessage}";
        }

        ProgressValue = 100;
        IsProgressVisible = false;
    }

    partial void OnSelectedModulePathChanged(string value)
    {
        TryPersistSelectedModulePathPreference(value);
        _ = InspectSelectedPathAsync();
    }

    /// <summary>
    /// 校验当前路径文本，并在不加载模块的情况下更新遮罩文案和按钮状态。
    /// </summary>
    /// <returns>本地校验以及可选的 manifest 版本比较完成后结束的任务。</returns>
    private async Task InspectSelectedPathAsync()
    {
        if (_moduleManager == null)
            return;

        if (SmartBpModuleManager.IsUnsafeInstallPath(SelectedModulePath))
        {
            PrimaryActionText = L("SmartBpModuleReinstall");
            OverlayMessage = L("SmartBpModulePathUnsafe");
            IsSelectInstalledModuleButtonVisible = true;
            return;
        }

        if (_moduleManager.ValidateModuleDirectory(
                SelectedModulePath,
                allowDevelopmentDirectory: IsDebugMode,
                out var manifest,
                out var error))
        {
            var requiredManifest = await _moduleManager.TryFetchRequiredModuleManifestAsync();
            if (manifest != null &&
                requiredManifest != null &&
                !SmartBpModuleManager.IsModuleVersionAllowed(manifest.ModuleVersion, requiredManifest.ModuleVersion))
            {
                PrimaryActionText = L("SmartBpModuleUpdateAndInstall");
                OverlayMessage = string.Format(
                    L("SmartBpModuleOutdatedFormat"),
                    requiredManifest.ModuleVersion);
                IsSelectInstalledModuleButtonVisible = true;
                return;
            }

            PrimaryActionText = L("SmartBpModuleLoadLocal");
            OverlayMessage = L("SmartBpModuleCompatibleFound");
            IsSelectInstalledModuleButtonVisible = false;
            return;
        }

        PrimaryActionText = error.Contains("ABI", StringComparison.OrdinalIgnoreCase) ||
                            error.Contains("RID", StringComparison.OrdinalIgnoreCase)
            ? L("SmartBpModuleReinstall")
            : IsDebugMode || IsPreviewMode ? L("SmartBpModuleLoadLocal") : L("SmartBpModuleDownloadAndInstall");
        OverlayMessage = string.IsNullOrWhiteSpace(error)
            ? L("SmartBpModuleRequired")
            : string.Format(L("SmartBpModulePathModuleInvalidFormat"), error);
        IsSelectInstalledModuleButtonVisible = true;
    }

    /// <summary>
    /// 模块版本过时事件处理：在 UI 线程上通过 InfoBar 提示用户更新 SmartBP 模块。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">包含本地版本与要求版本的事件参数。</param>
    private void OnModuleVersionOutdated(object? sender, ModuleVersionOutdatedEventArgs args)
    {
        if (_infoBarService == null)
            return;

        var message = string.Format(L("SmartBpModuleOutdatedFormat"), args.RequiredVersion);
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
                _infoBarService.ShowWarningInfoBar(message);
            else
                dispatcher.Invoke(() => _infoBarService.ShowWarningInfoBar(message));
        }
        else
        {
            _infoBarService.ShowWarningInfoBar(message);
        }
    }

    /// <summary>
    /// 将模块管理器当前加载状态同步到页面可绑定属性。
    /// </summary>
    private void SyncModuleState()
    {
        IsModuleLoaded = _moduleManager.IsModuleLoaded;
        ModuleContent = _moduleManager.ModuleContent;
        SyncModuleVersionText();
        var persistedRoot = _moduleManager.GetPreferredModuleRoot();
        if (!IsModuleLoaded &&
            !string.IsNullOrWhiteSpace(persistedRoot) &&
            !string.Equals(SelectedModulePath, persistedRoot, StringComparison.OrdinalIgnoreCase))
        {
            SelectedModulePath = persistedRoot;
        }
    }

    /// <summary>
    /// 根据已加载 manifest 或持久化模块状态刷新可见的模块版本标签。
    /// </summary>
    private void SyncModuleVersionText()
    {
        if (!IsModuleLoaded)
        {
            ModuleVersionText = string.Empty;
            IsModuleVersionVisible = false;
            return;
        }

        var version = TryReadLoadedModuleVersion() ?? _moduleManager.ReadState()?.ModuleVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            ModuleVersionText = string.Empty;
            IsModuleVersionVisible = false;
            return;
        }

        ModuleVersionText = string.Format(L("SmartBpModuleVersionFormat"), version);
        IsModuleVersionVisible = true;
    }

    /// <summary>
    /// 在当前已加载模块目录仍有效时读取 manifest 版本。
    /// </summary>
    /// <returns>已加载模块版本；无法读取时返回 <see langword="null"/>。</returns>
    private string? TryReadLoadedModuleVersion()
    {
        if (string.IsNullOrWhiteSpace(_moduleManager.ModuleRoot))
        {
            return null;
        }

        return _moduleManager.ValidateModuleDirectory(
            _moduleManager.ModuleRoot,
            allowDevelopmentDirectory: IsDebugMode,
            out var manifest,
            out _)
            ? manifest?.ModuleVersion
            : null;
    }

    /// <summary>
    /// 构建本地化的模块加载失败消息；存在管理器诊断详情时会追加到消息中。
    /// </summary>
    /// <param name="fallback">没有详细错误时使用的本地化兜底消息。</param>
    /// <returns>面向用户显示的失败消息。</returns>
    private string BuildLocalLoadFailureMessage(string fallback)
    {
        return string.IsNullOrWhiteSpace(_moduleManager.LastFailureMessage)
            ? fallback
            : string.Format(L("SmartBpModuleLocalFailureErrorFormat"), fallback, _moduleManager.LastFailureMessage);
    }

    /// <summary>
    /// 在调试和预览构建下将遮罩切换为本地加载模式。
    /// </summary>
    private void ConfigureLocalOnlyOverlayForDebugOrPreview()
    {
        if (!IsDebugMode && !IsPreviewMode)
            return;

        PrimaryActionText = L("SmartBpModuleLoadLocal");
        OverlayMessage = IsDebugMode
            ? L("SmartBpModuleDebugLoadDirectory")
            : L("SmartBpModulePreviewLoadOrImportArchive");
    }

    /// <summary>
    /// 从主应用资源表解析本地化字符串。
    /// </summary>
    /// <param name="key">资源键。</param>
    /// <returns>本地化字符串；资源缺失时返回资源键本身。</returns>
    private static string L(string key) => I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, key);

    /// <summary>
    /// 显示 SmartBP 模块压缩包暂存替换后的重启提示。
    /// </summary>
    /// <returns>提示处理完成后结束的任务。</returns>
    private static async Task OfferSmartBpModuleArchiveImportRestartAsync()
    {
        if (await MessageBoxHelper.ShowConfirmAsync(
                L("SmartBpModuleArchiveImportRestartPrompt"),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "RestartNeeded"),
                L("RestartNow"),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")))
        {
            AppBase.Current.Restart();
        }
    }

    /// <summary>
    /// 持久化完整且安全的 SmartBP 模块路径，同时忽略未输入完成的用户文本。
    /// </summary>
    /// <param name="value">用户当前输入或选择的路径。</param>
    private void TryPersistSelectedModulePathPreference(string value)
    {
        if (_moduleManager == null || string.IsNullOrWhiteSpace(value))
            return;

        try
        {
            if (!Path.IsPathFullyQualified(value) || SmartBpModuleManager.IsUnsafeInstallPath(value))
                return;

            _moduleManager.PersistModuleRootPreference(value);
        }
        catch
        {
            // 用户输入过程中可能出现不完整或非法路径，这里直接忽略。
        }
    }
}
