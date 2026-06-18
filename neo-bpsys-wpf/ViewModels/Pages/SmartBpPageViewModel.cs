using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.Services.SmartBpModule;
using System.IO;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// SmartBP shell page view model. It hosts module content when loaded and shows the install overlay otherwise.
/// </summary>
public partial class SmartBpPageViewModel : ViewModelBase
{
    private readonly SmartBpModuleManager _moduleManager = null!;
    private readonly IFilePickerService _filePickerService = null!;

    /// <summary>
    /// Initializes a design-time instance of the <see cref="SmartBpPageViewModel"/> class.
    /// </summary>
    public SmartBpPageViewModel()
    {
        SelectedModulePath = SmartBpModuleManager.GetDefaultModuleRoot();
        ConfigureLocalOnlyOverlayForDebugOrPreview();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartBpPageViewModel"/> class.
    /// </summary>
    /// <param name="moduleManager">SmartBP module manager.</param>
    /// <param name="filePickerService">File picker service.</param>
    public SmartBpPageViewModel(SmartBpModuleManager moduleManager, IFilePickerService filePickerService)
    {
        _moduleManager = moduleManager;
        _filePickerService = filePickerService;
        SelectedModulePath = _moduleManager.GetPreferredModuleRoot();
        ConfigureLocalOnlyOverlayForDebugOrPreview();
        _moduleManager.ModuleStateChanged += (_, _) => SyncModuleState();
        _ = InitializeAsync();
        _ = InspectSelectedPathAsync();
    }

    /// <summary>
    /// Whether the SmartBP module is loaded.
    /// </summary>
    [ObservableProperty] private bool _isModuleLoaded;

    /// <summary>
    /// Loaded module content.
    /// </summary>
    [ObservableProperty] private object? _moduleContent;

    /// <summary>
    /// Selected module path.
    /// </summary>
    [ObservableProperty] private string _selectedModulePath = string.Empty;

    /// <summary>
    /// Overlay message.
    /// </summary>
    [ObservableProperty] private string _overlayMessage = "SmartBP 需要安装独立模块后使用。";

    /// <summary>
    /// Primary button text.
    /// </summary>
    [ObservableProperty] private string _primaryActionText = "下载并安装";

    /// <summary>
    /// Whether the installed-module-folder button is visible.
    /// </summary>
    [ObservableProperty] private bool _isSelectInstalledModuleButtonVisible = true;

    /// <summary>
    /// Whether operation progress is visible.
    /// </summary>
    [ObservableProperty] private bool _isProgressVisible;

    /// <summary>
    /// Operation progress value.
    /// </summary>
    [ObservableProperty] private double _progressValue;

    /// <summary>
    /// Whether preview-local mode is active.
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
    /// Whether debug mode is active.
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
    /// Whether the select zip button is visible.
    /// </summary>
    public bool IsZipImportVisible => IsPreviewMode;

    private async Task InitializeAsync()
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
                return;
        }
    }

    [RelayCommand]
    private void BrowseModulePath()
    {
        var folder = _filePickerService.PickFolder();
        if (!string.IsNullOrWhiteSpace(folder))
            SelectedModulePath = folder;
    }

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
                "Debug 版本仅加载本地 SmartBP 模块目录。当前目录加载失败，请确认模块 Debug 输出目录存在且入口程序集可加载。");
            return;
        }

        if (!IsPreviewMode)
        {
            var installed = await _moduleManager.DownloadAndInstallCurrentModuleAsync(
                SelectedModulePath,
                new Progress<double>(value => ProgressValue = value));
            IsProgressVisible = false;
            if (installed)
                return;
        }

        IsProgressVisible = false;
        OverlayMessage = IsPreviewMode
            ? "Preview 版本请加载本地模块目录或导入 SmartBpModule.zip。"
            : "无法下载或安装 SmartBP 模块。请检查网络后重试，或手动选择已安装模块路径。";
    }

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
                ? BuildLocalLoadFailureMessage("Debug 版本加载本地模块目录失败，请确认入口程序集存在且可以被当前主程序加载。")
                : BuildLocalLoadFailureMessage("选择的 SmartBP 模块目录不可用，请检查 component.json、RID、ABI 和入口程序集。");
        }
    }

    [RelayCommand]
    private async Task ImportModuleZipAsync()
    {
        var zip = _filePickerService.PickZipFile();
        if (string.IsNullOrWhiteSpace(zip))
            return;

        IsProgressVisible = true;
        ProgressValue = 20;
        await _moduleManager.ImportZipAsync(zip, SelectedModulePath);
        ProgressValue = 100;
        IsProgressVisible = false;
    }

    partial void OnSelectedModulePathChanged(string value)
    {
        TryPersistSelectedModulePathPreference(value);
        _ = InspectSelectedPathAsync();
    }

    private async Task InspectSelectedPathAsync()
    {
        if (_moduleManager == null)
            return;

        if (SmartBpModuleManager.IsUnsafeInstallPath(SelectedModulePath))
        {
            PrimaryActionText = "重新安装";
            OverlayMessage = "当前路径不适合安装 SmartBP 模块，请选择可写的用户目录。";
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
                PrimaryActionText = "更新并安装";
                OverlayMessage = $"当前 SmartBP 模块版本过旧，需要更新到 {requiredManifest.ModuleVersion}。";
                IsSelectInstalledModuleButtonVisible = true;
                return;
            }

            PrimaryActionText = "加载本地模块";
            OverlayMessage = "检测到兼容的 SmartBP 模块，可以直接加载。";
            IsSelectInstalledModuleButtonVisible = false;
            return;
        }

        PrimaryActionText = error.Contains("ABI", StringComparison.OrdinalIgnoreCase) ||
                            error.Contains("RID", StringComparison.OrdinalIgnoreCase)
            ? "重新安装"
            : IsDebugMode || IsPreviewMode ? "加载本地模块" : "下载并安装";
        OverlayMessage = string.IsNullOrWhiteSpace(error)
            ? "SmartBP 需要安装独立模块后使用。"
            : $"当前路径模块不可用：{error}";
        IsSelectInstalledModuleButtonVisible = true;
    }

    private void SyncModuleState()
    {
        IsModuleLoaded = _moduleManager.IsModuleLoaded;
        ModuleContent = _moduleManager.ModuleContent;
        var persistedRoot = _moduleManager.GetPreferredModuleRoot();
        if (!IsModuleLoaded &&
            !string.IsNullOrWhiteSpace(persistedRoot) &&
            !string.Equals(SelectedModulePath, persistedRoot, StringComparison.OrdinalIgnoreCase))
        {
            SelectedModulePath = persistedRoot;
        }
    }

    private string BuildLocalLoadFailureMessage(string fallback)
    {
        return string.IsNullOrWhiteSpace(_moduleManager.LastFailureMessage)
            ? fallback
            : $"{fallback}错误：{_moduleManager.LastFailureMessage}";
    }

    private void ConfigureLocalOnlyOverlayForDebugOrPreview()
    {
        if (!IsDebugMode && !IsPreviewMode)
            return;

        PrimaryActionText = "加载本地模块";
        OverlayMessage = IsDebugMode
            ? "Debug 版本请加载 SmartBP 模块 Debug 输出目录。"
            : "Preview 版本请加载本地模块目录或导入 SmartBpModule.zip。";
    }

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
            // Ignore partial or invalid path input while the user is typing.
        }
    }
}
