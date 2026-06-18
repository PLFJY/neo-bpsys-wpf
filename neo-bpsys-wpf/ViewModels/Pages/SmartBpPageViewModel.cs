using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.Helpers;
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
    [ObservableProperty] private string _overlayMessage = L("SmartBpModuleRequired");

    /// <summary>
    /// Primary button text.
    /// </summary>
    [ObservableProperty] private string _primaryActionText = L("SmartBpModuleDownloadAndInstall");

    /// <summary>
    /// Whether the installed-module-folder button is visible.
    /// </summary>
    [ObservableProperty] private bool _isSelectInstalledModuleButtonVisible = true;

    /// <summary>
    /// Whether operation progress is visible.
    /// </summary>
    [ObservableProperty] private bool _isProgressVisible;

    /// <summary>
    /// SmartBP module version display text.
    /// </summary>
    [ObservableProperty] private string _moduleVersionText = string.Empty;

    /// <summary>
    /// Whether the SmartBP module version text is visible.
    /// </summary>
    [ObservableProperty] private bool _isModuleVersionVisible;

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
    /// Whether the module archive import button is visible.
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

    private string BuildLocalLoadFailureMessage(string fallback)
    {
        return string.IsNullOrWhiteSpace(_moduleManager.LastFailureMessage)
            ? fallback
            : string.Format(L("SmartBpModuleLocalFailureErrorFormat"), fallback, _moduleManager.LastFailureMessage);
    }

    private void ConfigureLocalOnlyOverlayForDebugOrPreview()
    {
        if (!IsDebugMode && !IsPreviewMode)
            return;

        PrimaryActionText = L("SmartBpModuleLoadLocal");
        OverlayMessage = IsDebugMode
            ? L("SmartBpModuleDebugLoadDirectory")
            : L("SmartBpModulePreviewLoadOrImportArchive");
    }

    private static string L(string key) => I18nHelper.GetLocalizedString(key);

    private static async Task OfferSmartBpModuleArchiveImportRestartAsync()
    {
        if (await MessageBoxHelper.ShowConfirmAsync(
                L("SmartBpModuleArchiveImportRestartPrompt"),
                L("RestartNeeded"),
                L("RestartNow"),
                L("Cancel")))
        {
            AppBase.Current.Restart();
        }
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
