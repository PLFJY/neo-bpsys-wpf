using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.Archives;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Services.SmartBpModule;
using neo_bpsys_wpf.Tutorial;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
    private readonly ISettingsHostService _settingsHostService = null!;
    private readonly IPluginMarketService _pluginMarketService = null!;
    private bool _isSyncingGhProxyMirror;

    /// <summary>
    /// 镜像连通性测试使用的浏览器 User-Agent，避免部分 ghproxy 镜像拦截无 User-Agent 的请求。
    /// </summary>
    private const string MirrorLatencyTestUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36";

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
    /// <param name="settingsHostService">设置宿主服务，用于共享 GhProxy 设置。</param>
    /// <param name="pluginMarketService">插件市场服务，用于在镜像切换后清除下载地址缓存。</param>
    public SmartBpPageViewModel(
        SmartBpModuleManager moduleManager,
        IFilePickerService filePickerService,
        IInfoBarService infoBarService,
        ILogger<SmartBpPageViewModel> logger,
        IGlobalRestartService globalRestartService,
        ISettingsHostService settingsHostService,
        IPluginMarketService pluginMarketService)
    {
        _moduleManager = moduleManager;
        _filePickerService = filePickerService;
        _infoBarService = infoBarService;
        _logger = logger;
        _globalRestartService = globalRestartService;
        _settingsHostService = settingsHostService;
        _pluginMarketService = pluginMarketService;
        SelectedModulePath = _moduleManager.GetPreferredModuleRoot();
        ConfigureLocalOnlyOverlayForDebugOrPreview();
        _moduleManager.ModuleStateChanged += (_, _) => SyncModuleState();
        _moduleManager.ModuleVersionOutdated += OnModuleVersionOutdated;
        _settingsHostService.Settings.PropertyChanged += Settings_PropertyChanged;
        SyncGhProxyMirrorFromSettings();
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
    public partial string OverlayMessage { get; set; } = L("SmartBpModuleNotInstalledDescription");

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
    /// 获取或设置高级选项区域是否展开。
    /// </summary>
    [ObservableProperty]
    public partial bool IsAdvancedOptionsExpanded { get; set; }

    /// <summary>
    /// 获取或设置 SmartBP 模块下载使用的 GhProxy 镜像地址。
    /// 该值与软件更新和插件市场共用 <c>Settings.GhProxyMirror</c>。
    /// </summary>
    [ObservableProperty]
    public partial string SelectedSmartBpModuleMirror { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 SmartBP 模块 GhProxy 选择器是否可见。
    /// </summary>
    [ObservableProperty]
    public partial bool IsSmartBpModuleMirrorSettingVisible { get; set; }

    /// <summary>
    /// 获取或设置 SmartBP 模块是否正在测试 GhProxy 镜像延迟。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestSmartBpModuleMirrorLatencyCommand))]
    public partial bool IsTestingSmartBpModuleMirrorLatency { get; set; }

    /// <summary>
    /// 获取 SmartBP 模块下载可选择的 GhProxy 镜像列表。
    /// </summary>
    public ObservableCollection<PluginMarketMirrorOption> SmartBpModuleMirrorOptions { get; } =
        new(DownloadMirrorPresets.GhProxyMirrorList.Select(
            mirror => new PluginMarketMirrorOption
            {
                DisplayNameKey = string.IsNullOrWhiteSpace(mirror)
                    ? "PluginMarketDirectConnectionNoProxy"
                    : mirror,
                Value = mirror
            }));

    partial void OnSelectedSmartBpModuleMirrorChanged(string value)
    {
        if (_isSyncingGhProxyMirror || _settingsHostService == null)
        {
            return;
        }

        _ = PersistSmartBpModuleMirrorAsync(value);
    }

    /// <summary>
    /// 切换高级选项展开状态。
    /// </summary>
    [RelayCommand]
    private void ToggleAdvancedOptions()
    {
        IsAdvancedOptionsExpanded = !IsAdvancedOptionsExpanded;
    }

    /// <summary>
    /// 保存 SmartBP 模块选择的 GhProxy 镜像地址。
    /// </summary>
    /// <param name="value">要保存的镜像地址；空字符串表示直连。</param>
    /// <returns>持久化完成后结束的任务。</returns>
    private async Task PersistSmartBpModuleMirrorAsync(string value)
    {
        _settingsHostService.Settings.GhProxyMirror = value;
        _pluginMarketService.ResetMirrorCache();
        await _settingsHostService.SaveConfigAsync();
    }

    /// <summary>
    /// 将共享的 GhProxy 镜像设置同步到 SmartBP 模块下载选项。
    /// </summary>
    private void SyncGhProxyMirrorFromSettings()
    {
        _isSyncingGhProxyMirror = true;
        try
        {
            SelectedSmartBpModuleMirror = _settingsHostService.Settings.GhProxyMirror;
            IsSmartBpModuleMirrorSettingVisible = IsChineseCultureForGitHubMirror();
        }
        finally
        {
            _isSyncingGhProxyMirror = false;
        }
    }

    /// <summary>
    /// 处理共享设置变更，并在 UI 线程同步 SmartBP 模块下载选项。
    /// </summary>
    /// <param name="sender">设置对象。</param>
    /// <param name="e">设置属性变更事件参数。</param>
    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(_settingsHostService.Settings.GhProxyMirror)
            && e.PropertyName != nameof(_settingsHostService.Settings.CultureInfo))
        {
            return;
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            SyncGhProxyMirrorFromSettings();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(SyncGhProxyMirrorFromSettings);
        }
    }

    /// <summary>
    /// 判断当前文化是否需要显示 GitHub 镜像设置。
    /// </summary>
    /// <returns>中文文化时返回 <see langword="true"/>。</returns>
    private bool IsChineseCultureForGitHubMirror() =>
        _settingsHostService.Settings.CultureInfo.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 测试 SmartBP 模块下载可用的所有 GhProxy 镜像延迟。
    /// </summary>
    /// <returns>延迟测试完成后结束的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanTestSmartBpModuleMirrorLatency))]
    private async Task TestSmartBpModuleMirrorLatencyAsync()
    {
        if (IsTestingSmartBpModuleMirrorLatency)
        {
            return;
        }

        IsTestingSmartBpModuleMirrorLatency = true;
        try
        {
            foreach (var item in SmartBpModuleMirrorOptions)
            {
                item.LatencyMs = null;
            }

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(MirrorLatencyTestUserAgent);

            var tasks = SmartBpModuleMirrorOptions.Select(async item =>
            {
                var testUrl = string.IsNullOrWhiteSpace(item.Value)
                    ? "https://github.com/"
                    : item.Value;

                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    using var request = new HttpRequestMessage(HttpMethod.Head, testUrl);
                    using var response = await httpClient.SendAsync(request);
                    stopwatch.Stop();
                    item.LatencyMs = response.IsSuccessStatusCode
                        ? (int)stopwatch.ElapsedMilliseconds
                        : -1;
                }
                catch
                {
                    item.LatencyMs = -1;
                }
            });

            await Task.WhenAll(tasks);
        }
        finally
        {
            IsTestingSmartBpModuleMirrorLatency = false;
        }
    }

    /// <summary>
    /// 判断 SmartBP 模块 GhProxy 延迟测试是否可以执行。
    /// </summary>
    /// <returns>未执行中的测试可以开始。</returns>
    private bool CanTestSmartBpModuleMirrorLatency() => !IsTestingSmartBpModuleMirrorLatency;

    /// <summary>
    /// 获取或设置操作进度是否可见。
    /// </summary>
    [ObservableProperty]
    public partial bool IsProgressVisible { get; set; }

    /// <summary>
    /// 获取或设置进度条是否为不确定模式。不确定模式下显示滚动动画，不显示具体百分比；确定模式下绑定 <see cref="ProgressValue"/> 显示真实进度。
    /// </summary>
    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; } = true;

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
        IsProgressIndeterminate = true;
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
    /// 版本过旧时跳过本地加载，直接进入下载安装流程。
    /// </summary>
    /// <returns>加载或安装尝试完成后结束的任务。</returns>
    [RelayCommand]
    private async Task PrimaryActionAsync()
    {
        IsProgressVisible = true;
        IsProgressIndeterminate = false;
        ProgressValue = 0;

        if (!_moduleManager.IsModuleVersionOutdated)
        {
            if (await _moduleManager.LoadModuleFromDirectoryAsync(SelectedModulePath))
            {
                IsProgressVisible = false;
                return;
            }
        }

        if (IsDebugMode)
        {
            IsProgressVisible = false;
            if (_moduleManager.IsModuleVersionOutdated)
            {
                _moduleManager.SetVersionOutdatedForDebug(false);
                await _moduleManager.LoadModuleFromDirectoryAsync(SelectedModulePath);
                OverlayMessage = "[DEBUG] 版本过旧标记已清除，模块已加载";
            }
            else
            {
                OverlayMessage = BuildLocalLoadFailureMessage(
                    L("SmartBpModuleDebugLocalLoadFailed"));
            }
            return;
        }

        if (!IsPreviewMode)
        {
            // 解压阶段映射到 80→98，98→100 由后续验证安装补齐。
            // Progress<double> 的 5/70/80/100 报告点保持不变。
            var extractionProgress = new Progress<ArchiveProgress>(p =>
            {
                ProgressValue = 80 + p.Percentage * 0.18;
                OverlayMessage = string.Format(L("SmartBpModuleArchiveExtractingFormat"), p.Percentage);
            });
            var installed = await _moduleManager.DownloadAndInstallCurrentModuleAsync(
                SelectedModulePath,
                new Progress<double>(value => ProgressValue = value),
                extractionProgress);
            IsProgressVisible = false;
            if (installed)
            {
                if (_moduleManager.IsRestartRequiredForPendingModuleImport)
                {
                    _globalRestartService.IsRestartRequired = true;
                    OverlayMessage = L("SmartBpModuleArchiveImportRestartPrepared");
                }

                return;
            }

            OverlayMessage = L("SmartBpModuleDownloadInstallFailed");
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
        IsProgressIndeterminate = false;
        ProgressValue = 0;
        // 解压阶段映射到 5→98，98→100 由后续逻辑补齐。
        var extractionProgress = new Progress<ArchiveProgress>(p =>
        {
            ProgressValue = 5 + p.Percentage * 0.93;
            OverlayMessage = string.Format(L("SmartBpModuleArchiveExtractingFormat"), p.Percentage);
        });
        if (await _moduleManager.ImportArchiveAsync(archivePath, SelectedModulePath, extractionProgress))
        {
            if (_moduleManager.IsRestartRequiredForPendingModuleImport)
            {
                _globalRestartService.IsRestartRequired = true;
                OverlayMessage = L("SmartBpModuleArchiveImportRestartPrepared");
                ProgressValue = 100;
                IsProgressVisible = false;
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
            ? L("SmartBpModuleNotInstalledDescription")
            : (IsAdvancedOptionsExpanded
                ? string.Format(L("SmartBpModulePathModuleInvalidFormat"), error)
                : L("SmartBpModuleNotInstalledDescription"));
        IsSelectInstalledModuleButtonVisible = true;
    }

    /// <summary>
    /// 模块版本过时事件处理：版本过旧时通过 <see cref="SmartBpModuleManager.ModuleStateChanged"/>
    /// 触发 <see cref="SyncModuleState"/> 显示更新遮罩，此处无需额外处理。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">包含本地版本与要求版本的事件参数。</param>
    private void OnModuleVersionOutdated(object? sender, ModuleVersionOutdatedEventArgs args)
    {
    }

    /// <summary>
    /// 将模块管理器当前加载状态同步到页面可绑定属性。
    /// 版本过旧时移除模块内容、显示更新遮罩文案，表现为模块未加载。
    /// </summary>
    private void SyncModuleState()
    {
        IsModuleLoaded = _moduleManager.IsModuleLoaded;
        ModuleContent = _moduleManager.IsModuleVersionOutdated ? null : _moduleManager.ModuleContent;
        SyncModuleVersionText();

        if (_moduleManager.IsModuleVersionOutdated)
        {
            var requiredVersion = _moduleManager.RequiredModuleVersion ?? string.Empty;
            PrimaryActionText = L("SmartBpModuleUpdateAndInstall");
            OverlayMessage = string.Format(L("SmartBpModuleOutdatedFormat"), requiredVersion);
            IsSelectInstalledModuleButtonVisible = true;
        }

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
