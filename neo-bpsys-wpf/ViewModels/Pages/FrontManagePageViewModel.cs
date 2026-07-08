using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class FrontManagePageViewModel : ViewModelBase, IRecipient<FrontedLayoutPackagesChangedMessage>
{
#pragma warning disable CS8618
    public FrontManagePageViewModel()
#pragma warning restore CS8618
    {
    }

    private readonly IFrontedWindowService _frontedWindowService;
    private readonly ISharedDataService _sharedDataService;
    private readonly IFilePickerService? _filePickerService;
    private readonly IServiceProvider? _serviceProvider;
    private readonly IFrontedLayoutPackageManager? _packageManager;
    private readonly IFrontedLayoutPackageExporter? _packageExporter;
    private readonly IFrontedLayoutPackageImporter? _packageImporter;
    private readonly IFrontedLayoutPackageLegacyConverter? _legacyPackageConverter;
    private readonly IPluginMarketService? _pluginMarketService;
    private readonly IPluginInstallService? _pluginInstallService;
    private readonly IFrontedWindowRegistry? _frontedWindowRegistry;
    private readonly IFrontedBehaviorRuntime? _behaviorRuntime;
    private readonly ISettingsHostService? _settingsHostService;
    private readonly ILogger<FrontManagePageViewModel>? _logger;
    private FrontedDesignerWindow? _frontedDesignerWindow;

    public FrontManagePageViewModel(
        IFrontedWindowService frontedWindowService,
        ISharedDataService sharedDataService,
        IFilePickerService filePickerService,
        IFrontedLayoutPackageManager packageManager,
        IFrontedLayoutPackageExporter packageExporter,
        IFrontedLayoutPackageImporter packageImporter,
        IFrontedLayoutPackageLegacyConverter legacyPackageConverter,
        IPluginMarketService pluginMarketService,
        IPluginInstallService pluginInstallService,
        IFrontedWindowRegistry frontedWindowRegistry,
        IFrontedBehaviorRuntime behaviorRuntime,
        IServiceProvider serviceProvider,
        ILogger<FrontManagePageViewModel> logger)
    {
        _frontedWindowService = frontedWindowService;
        _sharedDataService = sharedDataService;
        _filePickerService = filePickerService;
        _packageManager = packageManager;
        _packageExporter = packageExporter;
        _packageImporter = packageImporter;
        _legacyPackageConverter = legacyPackageConverter;
        _pluginMarketService = pluginMarketService;
        _pluginInstallService = pluginInstallService;
        _frontedWindowRegistry = frontedWindowRegistry;
        _behaviorRuntime = behaviorRuntime;
        _serviceProvider = serviceProvider;
        _settingsHostService = serviceProvider.GetService<ISettingsHostService>();
        _logger = logger;
        RebuildManageableWindows();
        if (_settingsHostService is not null)
        {
            _settingsHostService.LanguageSettingChanged += OnLanguageSettingChanged;
        }

        _ = RefreshPackagesAsync();
    }

    /// <summary>
    /// Initializes a fronted management page view model without a behavior runtime.
    /// </summary>
    /// <param name="frontedWindowService">Fronted window service.</param>
    /// <param name="sharedDataService">Shared data service.</param>
    /// <param name="filePickerService">File picker service.</param>
    /// <param name="packageManager">Layout package manager.</param>
    /// <param name="packageExporter">Layout package exporter.</param>
    /// <param name="packageImporter">Layout package importer.</param>
    /// <param name="legacyPackageConverter">Legacy package converter.</param>
    /// <param name="pluginMarketService">Plugin market service.</param>
    /// <param name="pluginInstallService">Plugin install service.</param>
    /// <param name="frontedWindowRegistry">Fronted window registry.</param>
    /// <param name="serviceProvider">Application service provider.</param>
    /// <param name="logger">Logger.</param>
    public FrontManagePageViewModel(
        IFrontedWindowService frontedWindowService,
        ISharedDataService sharedDataService,
        IFilePickerService filePickerService,
        IFrontedLayoutPackageManager packageManager,
        IFrontedLayoutPackageExporter packageExporter,
        IFrontedLayoutPackageImporter packageImporter,
        IFrontedLayoutPackageLegacyConverter legacyPackageConverter,
        IPluginMarketService pluginMarketService,
        IPluginInstallService pluginInstallService,
        IFrontedWindowRegistry frontedWindowRegistry,
        IServiceProvider serviceProvider,
        ILogger<FrontManagePageViewModel> logger)
        : this(
            frontedWindowService,
            sharedDataService,
            filePickerService,
            packageManager,
            packageExporter,
            packageImporter,
            legacyPackageConverter,
            pluginMarketService,
            pluginInstallService,
            frontedWindowRegistry,
            behaviorRuntime: null!,
            serviceProvider,
            logger)
    {
    }

    public ObservableCollection<FrontedWindowManageItem> ExternalFrontedWindows { get; } = [];

    public ObservableCollection<FrontedWindowManageItem> ManageableWindows { get; } = [];

    /// <summary>
    /// Manageable fronted windows grouped by descriptor group key.
    /// </summary>
    public ObservableCollection<FrontedWindowManageGroup> ManageableWindowGroups { get; } = [];

    public ObservableCollection<FrontedLayoutPackageInfo> LayoutPackages { get; } = [];

    private void OnLanguageSettingChanged(object? sender, Core.Events.LanguageChangedEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(new Action(RebuildManageableWindows));
    }

    private void RebuildManageableWindows()
    {
        if (_frontedWindowRegistry is null)
        {
            return;
        }

        ManageableWindows.Clear();
        ManageableWindowGroups.Clear();
        var manageableWindows = _frontedWindowRegistry.GetManageableWindows() ?? [];
        foreach (var group in FrontedWindowManageGroup.FromDescriptors(manageableWindows, _settingsHostService))
        {
            ManageableWindowGroups.Add(group);
            foreach (var item in group.Windows)
            {
                ManageableWindows.Add(item);
            }
        }

        if (ManageableWindowGroups.Count == 0)
        {
            foreach (var descriptor in manageableWindows)
            {
                ManageableWindows.Add(FrontedWindowManageItem.FromDescriptor(descriptor, _settingsHostService));
            }
        }
    }

    [ObservableProperty]
    public partial FrontedLayoutPackageInfo? SelectedPackage { get; set; }

    [ObservableProperty]
    public partial string ActivePackageDisplay { get; set; } = "builtin";

    [ObservableProperty]
    public partial string PackageManagerStatus { get; set; } = string.Empty;

    [RelayCommand]
    private void ShowAllWindows()
    {
        _frontedWindowService.AllWindowShow();
    }

    [RelayCommand]
    private void HideAllWindows()
    {
        _frontedWindowService.AllWindowHide();
    }

    [RelayCommand]
    private async Task StopAllLoopAnimationsAsync()
    {
        if (_behaviorRuntime is null)
        {
            return;
        }

        try
        {
            var count = await _behaviorRuntime.StopAllLoopBehaviorsAsync(FrontedBehaviorStopReason.ManualClear);
            PackageManagerStatus = count > 0
                ? string.Format(I18nHelper.GetLocalizedString("StoppedLoopAnimationsFormat"), count)
                : I18nHelper.GetLocalizedString("NoActiveLoopAnimations");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to stop all active loop animations.");
            PackageManagerStatus = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenFrontedDesigner()
    {
        if (_serviceProvider is null)
        {
            return;
        }

        if (_frontedDesignerWindow is { IsLoaded: true })
        {
            _frontedDesignerWindow.Activate();
            return;
        }

        try
        {
            var window = ActivatorUtilities.CreateInstance<FrontedDesignerWindow>(_serviceProvider);
            window.Closed += (_, _) => _frontedDesignerWindow = null;
            _frontedDesignerWindow = window;
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open fronted designer window.");
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("WindowLaunchError")}\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshPackagesAsync()
    {
        await RefreshPackagesCoreAsync(preferredPackageId: null);
    }

    private async Task RefreshPackagesCoreAsync(string? preferredPackageId)
    {
        if (_packageManager is null)
        {
            return;
        }

        try
        {
            var previousPackageId = SelectedPackage?.PackageId;
            var packages = await _packageManager.ListPackagesAsync();
            LayoutPackages.Clear();
            foreach (var package in packages)
            {
                LayoutPackages.Add(package);
            }

            var active = packages.FirstOrDefault(package => package.IsActivePackage)
                         ?? packages.FirstOrDefault(package => package.IsBuiltin);
            SelectedPackage = FindPackageById(preferredPackageId)
                              ?? FindPackageById(previousPackageId)
                              ?? active
                              ?? LayoutPackages.FirstOrDefault();
            ActivePackageDisplay = active is null
                ? I18nHelper.GetLocalizedString("SystemBuiltIn")
                : $"{active.Name} ({active.PackageId})";
            PackageManagerStatus = I18nHelper.GetLocalizedString("RefreshPackages");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to refresh fronted layout packages.");
            PackageManagerStatus = ex.Message;
        }

        FrontedLayoutPackageInfo? FindPackageById(string? packageId)
        {
            return string.IsNullOrWhiteSpace(packageId)
                ? null
                : LayoutPackages.FirstOrDefault(package =>
                    string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
        }
    }

    [RelayCommand]
    private async Task ImportPackageAsync()
    {
        if (_filePickerService is null || _packageImporter is null || _packageManager is null)
        {
            return;
        }

        var path = _filePickerService.PickBpuiFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var importedFromLegacy = false;
            var result = await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = path
            });
            result = await HandleMissingPluginImportAsync(path, result, replaceExisting: false);

            if (result.PackageAlreadyExists && !string.IsNullOrWhiteSpace(result.PackageId))
            {
                var replace = await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString("ReplaceExistingPackage"),
                    I18nHelper.GetLocalizedString("PackageAlreadyExists"),
                    I18nHelper.GetLocalizedString("Confirm"),
                    I18nHelper.GetLocalizedString("Cancel"));
                if (!replace)
                {
                    return;
                }

                result = await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
                {
                    PackagePath = path,
                    ReplaceExisting = true
                });
                result = await HandleMissingPluginImportAsync(path, result, replaceExisting: true);
            }

            if (result.IsLegacyPackage)
            {
                importedFromLegacy = true;
                if (_legacyPackageConverter is null)
                {
                    PackageManagerStatus = I18nHelper.GetLocalizedString("LegacyPackageConvertFailed");
                    return;
                }

                var convert = await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString("LegacyPackageConvertMessage"),
                    I18nHelper.GetLocalizedString("LegacyPackageConvertTitle"),
                    I18nHelper.GetLocalizedString("ConvertLegacyPackage"),
                    I18nHelper.GetLocalizedString("Cancel"));
                if (!convert)
                {
                    return;
                }

                var packageId = $"converted.legacy.{DateTime.Now:yyyyMMddHHmm}";
                var packageName = Path.GetFileName(path);
                var convertResult = await _legacyPackageConverter.ConvertAsync(new FrontedLayoutPackageLegacyConvertRequest
                {
                    LegacyPackagePath = path,
                    PackageId = packageId,
                    Name = string.IsNullOrWhiteSpace(packageName) ? packageId : packageName,
                    Description = I18nHelper.GetLocalizedString("LegacyPackageDefaultDescription"),
                    Author = string.Empty,
                    MinVersion = string.Empty,
                    InstallAfterConvert = false,
                    ActivateAfterInstall = false
                });

                if (!convertResult.Success || string.IsNullOrWhiteSpace(convertResult.ConvertedPackagePath))
                {
                    PackageManagerStatus =
                        $"{I18nHelper.GetLocalizedString("LegacyPackageConvertFailed")}: {convertResult.ErrorMessage}";
                    await MessageBoxHelper.ShowErrorAsync(PackageManagerStatus);
                    return;
                }

                result = await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
                {
                    PackagePath = convertResult.ConvertedPackagePath
                });
                result = await HandleMissingPluginImportAsync(
                    convertResult.ConvertedPackagePath,
                    result,
                    replaceExisting: false);

                if (result.PackageAlreadyExists && !string.IsNullOrWhiteSpace(result.PackageId))
                {
                    var replace = await MessageBoxHelper.ShowConfirmAsync(
                        I18nHelper.GetLocalizedString("ReplaceExistingPackage"),
                        I18nHelper.GetLocalizedString("PackageAlreadyExists"),
                        I18nHelper.GetLocalizedString("Confirm"),
                        I18nHelper.GetLocalizedString("Cancel"));
                    if (!replace)
                    {
                        return;
                    }

                    result = await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
                    {
                        PackagePath = convertResult.ConvertedPackagePath,
                        ReplaceExisting = true
                    });
                    result = await HandleMissingPluginImportAsync(
                        convertResult.ConvertedPackagePath,
                        result,
                        replaceExisting: true);
                }

                var technicalDetails = LegacyConversionMessageFormatter.BuildTechnicalDetails(convertResult);
                if (!string.IsNullOrWhiteSpace(technicalDetails))
                {
                    _logger?.LogInformation(
                        "Legacy layout package conversion details for {PackageId}:{NewLine}{Details}",
                        packageId,
                        Environment.NewLine,
                        technicalDetails);
                }

                if (LegacyConversionMessageFormatter.HasUserFacingWarnings(convertResult))
                {
                    await MessageBoxHelper.ShowInfoAsync(
                        LegacyConversionMessageFormatter.BuildUserSummary(convertResult),
                        I18nHelper.GetLocalizedString("LegacyPackageConvertWarnings"));
                }

                PackageManagerStatus =
                    $"{I18nHelper.GetLocalizedString("LegacyPackageConvertSucceeded")}: {packageId} "
                    + $"{I18nHelper.GetLocalizedString("LayoutCount")}: {convertResult.LayoutCount}, "
                    + $"{I18nHelper.GetLocalizedString("ResourceCount")}: {convertResult.ResourceCount}";
                if (!result.Success)
                {
                    PackageManagerStatus =
                        $"{I18nHelper.GetLocalizedString("PackageImportFailed")}: {result.ErrorMessage}";
                    return;
                }
            }

            if (result.IsLegacyPackage)
            {
                return;
            }

            if (result.RequiresNewerApp)
            {
                PackageManagerStatus = I18nHelper.GetLocalizedString("PackageRequiresNewerVersion");
                return;
            }

            if (!result.Success)
            {
                PackageManagerStatus = $"{I18nHelper.GetLocalizedString("PackageImportFailed")}: {result.ErrorMessage}";
                return;
            }

            await RefreshPackagesCoreAsync(result.PackageId);
            SelectedPackage = LayoutPackages.FirstOrDefault(package => package.PackageId == result.PackageId) ?? SelectedPackage;
            PackageManagerStatus =
                $"{I18nHelper.GetLocalizedString("PackageImportSucceeded")}: {result.PackageId} "
                + $"{I18nHelper.GetLocalizedString("LayoutCount")}: {result.LayoutCount}, "
                + $"{I18nHelper.GetLocalizedString("ResourceCount")}: {result.ResourceCount}";
            if (await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString(importedFromLegacy ? "ActivateConvertedPackage" : "ActivateImportedPackage"),
                    I18nHelper.GetLocalizedString("Tips"),
                    I18nHelper.GetLocalizedString("Confirm"),
                    I18nHelper.GetLocalizedString("Cancel"))
                && !string.IsNullOrWhiteSpace(result.PackageId))
            {
                if (_behaviorRuntime is not null)
                {
                    await _behaviorRuntime.StopAllLoopBehaviorsAsync(FrontedBehaviorStopReason.PackageSwitched);
                }

                await _packageManager.ActivatePackageAsync(result.PackageId);
                await _frontedWindowService.ReloadFrontedLayoutsAsync();
                await RefreshPackagesCoreAsync(result.PackageId);
                SelectedPackage = LayoutPackages.FirstOrDefault(package => package.PackageId == result.PackageId) ?? SelectedPackage;
                PackageManagerStatus = $"{I18nHelper.GetLocalizedString("PackageActivatedInstalled")}: {result.PackageId}";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to import fronted layout package.");
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString("PackageImportFailed")}: {ex.Message}";
        }
    }

    private async Task<FrontedLayoutPackageImportResult> HandleMissingPluginImportAsync(
        string packagePath,
        FrontedLayoutPackageImportResult result,
        bool replaceExisting)
    {
        if (!result.Success
            || (!result.HasMissingPluginControls && !result.HasUnsatisfiedPluginDependencies))
        {
            return result;
        }

        var dependencies = BuildDependencyIssues(result);
        IReadOnlyList<PluginMarketItem> marketItems = [];
        var marketUnavailable = false;
        if (_pluginMarketService is not null)
        {
            try
            {
                marketItems = await _pluginMarketService.GetMarketPluginsAsync();
            }
            catch (Exception ex)
            {
                marketUnavailable = true;
                _logger?.LogWarning(ex, "Failed to load plugin market while importing layout package.");
            }
        }

        var installableItems = ClassifyDependencyMarketState(dependencies, marketItems, marketUnavailable);
        var preview = FormatDependencyPreview(dependencies);

        if (installableItems.Count > 0 && _pluginMarketService is not null && _pluginInstallService is not null)
        {
            var installMessage = I18nHelper.GetLocalizedString("MissingPluginImportMessage")
                                 + Environment.NewLine
                                 + Environment.NewLine
                                 + preview
                                 + Environment.NewLine
                                 + Environment.NewLine
                                 + I18nHelper.GetLocalizedString("PluginDependencyInstallAvailableMessage")
                                 + Environment.NewLine
                                 + I18nHelper.GetLocalizedString("PluginDependencyInstallRestartNotice");
            var install = await MessageBoxHelper.ShowConfirmAsync(
                installMessage,
                I18nHelper.GetLocalizedString("MissingPluginImportTitle"),
                I18nHelper.GetLocalizedString("PluginDependencyInstallButton"),
                I18nHelper.GetLocalizedString("Cancel"));
            if (install)
            {
                try
                {
                    await InstallMarketDependenciesAsync(installableItems);
                    await MessageBoxHelper.ShowInfoAsync(
                        I18nHelper.GetLocalizedString("SomeSettingsRequireRestartingTheApplication"),
                        I18nHelper.GetLocalizedString("RestartNeeded"),
                        I18nHelper.GetLocalizedString("Confirm"));
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to install market plugin dependencies for layout import.");
                    await MessageBoxHelper.ShowErrorAsync(ex.Message);
                }
            }
        }

        var message = "Layout imported. Some plugin windows are unavailable until their plugins are installed."
                      + Environment.NewLine
                      + Environment.NewLine
                      + preview;
        await MessageBoxHelper.ShowInfoAsync(
            message,
            I18nHelper.GetLocalizedString("MissingPluginImportTitle"),
            I18nHelper.GetLocalizedString("Close"));
        return result;
    }

    private static List<FrontedLayoutPackagePluginDependencyIssue> BuildDependencyIssues(
        FrontedLayoutPackageImportResult result)
    {
        var dependencies = result.UnsatisfiedPluginDependencies.ToList();
        foreach (var group in result.MissingPluginControls.GroupBy(control => control.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            var dependency = dependencies.FirstOrDefault(item =>
                string.Equals(item.PackageId, group.Key, StringComparison.OrdinalIgnoreCase));
            if (dependency == null)
            {
                dependency = new FrontedLayoutPackagePluginDependencyIssue
                {
                    PackageId = group.Key,
                    DisplayName = group.Key,
                    MarketplaceId = group.Key,
                    IsInstalled = false,
                    IsVersionSatisfied = false
                };
                dependencies.Add(dependency);
            }

            dependency.AffectedControls = dependency.AffectedControls
                .Concat(group)
                .GroupBy(control => $"{control.Window}/{control.ControlName}", StringComparer.Ordinal)
                .Select(grouped => grouped.First())
                .ToList();
        }

        return dependencies;
    }

    private static List<PluginMarketItem> ClassifyDependencyMarketState(
        List<FrontedLayoutPackagePluginDependencyIssue> dependencies,
        IReadOnlyList<PluginMarketItem> marketItems,
        bool marketUnavailable)
    {
        var installable = new List<PluginMarketItem>();
        foreach (var dependency in dependencies)
        {
            dependency.IsMarketUnavailable = marketUnavailable;
            if (marketUnavailable)
            {
                continue;
            }

            var marketId = string.IsNullOrWhiteSpace(dependency.MarketplaceId)
                ? dependency.PackageId
                : dependency.MarketplaceId;
            var item = marketItems.FirstOrDefault(item =>
                string.Equals(item.Id, marketId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Id, dependency.PackageId, StringComparison.OrdinalIgnoreCase));
            if (item == null || !IsMarketVersionSuitable(item.Version, dependency.MinVersion))
            {
                dependency.IsAvailableInMarket = false;
                continue;
            }

            dependency.IsAvailableInMarket = true;
            installable.Add(item);
        }

        return installable
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static string FormatDependencyPreview(IReadOnlyList<FrontedLayoutPackagePluginDependencyIssue> dependencies)
    {
        var lines = dependencies
            .Take(8)
            .Select(dependency =>
            {
                var status = dependency.IsMarketUnavailable
                    ? I18nHelper.GetLocalizedString("PluginDependencyMarketOffline")
                    : dependency.IsAvailableInMarket
                        ? I18nHelper.GetLocalizedString("PluginDependencyMarketAvailable")
                        : dependency.IsInstalled && !dependency.IsVersionSatisfied
                            ? I18nHelper.GetLocalizedString("PluginDependencyUpdateRequired")
                            : I18nHelper.GetLocalizedString("PluginDependencyNotFoundInMarket");
                var controls = dependency.AffectedControls.Count > 0
                    ? string.Join(", ", dependency.AffectedControls.Take(3).Select(control => $"{control.Window} {control.ControlName}"))
                    : string.Join(", ", dependency.RequiredBy.Take(3));
                return $"{dependency.DisplayName ?? dependency.PackageId} [{dependency.PackageId}] "
                       + $"{I18nHelper.GetLocalizedString("PluginDependencyMinVersion")}={dependency.MinVersion ?? "-"} "
                       + $"{I18nHelper.GetLocalizedString("PluginDependencyInstalledVersion")}={dependency.InstalledVersion ?? "-"} {status}"
                       + (string.IsNullOrWhiteSpace(controls) ? string.Empty : $"{Environment.NewLine}  {controls}");
            })
            .ToList();
        if (dependencies.Count > 8)
        {
            lines.Add($"... +{dependencies.Count - 8}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task InstallMarketDependenciesAsync(IReadOnlyList<PluginMarketItem> marketItems)
    {
        if (_pluginMarketService is null || _pluginInstallService is null)
        {
            return;
        }

        var pendingIds = marketItems.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in marketItems)
        {
            await _pluginMarketService.QueuePluginDownloadAsync(item);
        }

        while (pendingIds.Count > 0)
        {
            while (true)
            {
                var download = _pluginMarketService.ConsumeCompletedDownload();
                if (download == null)
                {
                    break;
                }

                try
                {
                    var install = _pluginInstallService.InstallFromExtractedDirectory(download.ExtractedDirectoryPath);
                    pendingIds.Remove(install.Manifest.Id);
                    if (download.QueueItem != null)
                    {
                        download.QueueItem.Status = PluginDownloadQueueStatus.QueueInstalledRestartRequired;
                        download.QueueItem.CanCancel = false;
                        download.QueueItem.SpeedText = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    var pluginId = download.QueueItem?.PluginId;
                    if (string.IsNullOrWhiteSpace(pluginId))
                    {
                        pluginId = Path.GetFileName(download.ExtractedDirectoryPath);
                    }

                    throw new InvalidOperationException(
                        $"{I18nHelper.GetLocalizedString("PluginDependencyInstallFailed")}: {pluginId} {ex.Message}",
                        ex);
                }
                finally
                {
                    CleanupDownloadedPluginPackageResidue(download.ExtractedDirectoryPath);
                }
            }

            var failed = _pluginMarketService.DownloadQueue.FirstOrDefault(item =>
                pendingIds.Contains(item.PluginId)
                && item.Status == PluginDownloadQueueStatus.QueueFailed);
            if (failed != null)
            {
                throw new InvalidOperationException(
                    $"{I18nHelper.GetLocalizedString("PluginDependencyInstallFailed")}: {failed.PluginId} {failed.ErrorMessage}");
            }

            if (!_pluginMarketService.IsDownloading
                && !_pluginMarketService.DownloadQueue.Any(item => pendingIds.Contains(item.PluginId) && item.IsInProgress))
            {
                break;
            }

            await Task.Delay(250);
        }

        if (pendingIds.Count > 0)
        {
            _logger?.LogError("Plugin dependencies install incomplete: {Ids}", string.Join(", ", pendingIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)));
            throw new InvalidOperationException(
                $"{I18nHelper.GetLocalizedString("PluginDependencyInstallIncomplete")}: {string.Join(", ", pendingIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))}");
        }
    }

    private static bool IsMarketVersionSuitable(string marketVersion, string? minVersion)
    {
        if (string.IsNullOrWhiteSpace(minVersion))
        {
            return true;
        }

        return TryParseVersion(marketVersion, out var market)
               && TryParseVersion(minVersion, out var required)
               && market >= required;
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOfAny(['+', '-']);
        if (metadataIndex > 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return Version.TryParse(normalized, out version!);
    }

    private static void CleanupDownloadedPluginPackageResidue(string extractedDirectoryPath)
    {
        try
        {
            if (Directory.Exists(extractedDirectoryPath))
            {
                Directory.Delete(extractedDirectoryPath, true);
            }

            var sessionDirectory = Directory.GetParent(extractedDirectoryPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(sessionDirectory)
                && Directory.Exists(sessionDirectory)
                && !Directory.EnumerateFileSystemEntries(sessionDirectory).Any())
            {
                Directory.Delete(sessionDirectory, true);
            }
        }
        catch
        {
        }
    }

    [RelayCommand]
    private async Task ExportPackageAsync()
    {
        if (_serviceProvider is null || _packageExporter is null)
        {
            return;
        }

        try
        {
            var window = ActivatorUtilities.CreateInstance<FrontedLayoutPackageExportWindow>(_serviceProvider);
            window.Owner = GetShownOwnerWindow();
            if (window.ShowDialog() != true || window.ExportRequest is null)
            {
                return;
            }

            var request = window.ExportRequest;
            if (File.Exists(request.OutputPath)
                && !await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString("ConfirmOverwriteFile"),
                    I18nHelper.GetLocalizedString("Tips"),
                    I18nHelper.GetLocalizedString("Confirm"),
                    I18nHelper.GetLocalizedString("Cancel")))
            {
                return;
            }

            var result = await _packageExporter.ExportAsync(request);
            if (result.Success)
            {
                PackageManagerStatus =
                    $"{I18nHelper.GetLocalizedString("PackageExportSucceeded")}: {result.OutputPath} "
                    + $"{I18nHelper.GetLocalizedString("ExportedLayoutCount")}: {result.LayoutCount}, "
                    + $"{I18nHelper.GetLocalizedString("ExportedResourceCount")}: {result.ResourceCount}";
            }
            else
            {
                PackageManagerStatus = $"{I18nHelper.GetLocalizedString("PackageExportFailed")}: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to export fronted layout package.");
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString("PackageExportFailed")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ActivatePackageAsync()
    {
        await ActivateSelectedPackageAsync(confirm: true);
    }

    public void Receive(FrontedLayoutPackagesChangedMessage message)
    {
        _ = RefreshPackagesAfterExternalChangeAsync(message.ActivePackageId);
    }

    private async Task RefreshPackagesAfterExternalChangeAsync(string? activePackageId)
    {
        await RefreshPackagesCoreAsync(activePackageId);
        var selected = !string.IsNullOrWhiteSpace(activePackageId)
            ? LayoutPackages.FirstOrDefault(package =>
                string.Equals(package.PackageId, activePackageId, StringComparison.OrdinalIgnoreCase))
            : LayoutPackages.FirstOrDefault(package => package.IsActivePackage);
        if (selected is not null)
        {
            SelectedPackage = selected;
        }
    }

    [RelayCommand]
    private async Task ActivateSelectedPackageByDoubleClickAsync()
    {
        await ActivateSelectedPackageAsync(confirm: false);
    }

    private async Task ActivateSelectedPackageAsync(bool confirm)
    {
        if (_packageManager is null || SelectedPackage is null)
        {
            return;
        }

        if (SelectedPackage.IsLocal)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString("PackageActivationNotImplemented");
            return;
        }

        try
        {
            if (confirm
                && !SelectedPackage.IsActivePackage
                && !await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString("ConfirmActivatePackage"),
                    I18nHelper.GetLocalizedString("Tips"),
                    I18nHelper.GetLocalizedString("Confirm"),
                    I18nHelper.GetLocalizedString("Cancel")))
            {
                return;
            }

            var activatedPackageId = SelectedPackage.PackageId;
            var activatedIsBuiltin = SelectedPackage.IsBuiltin;
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.StopAllLoopBehaviorsAsync(FrontedBehaviorStopReason.PackageSwitched);
            }

            await _packageManager.ActivatePackageAsync(activatedPackageId);
            await _frontedWindowService.ReloadFrontedLayoutsAsync();
            await RefreshPackagesCoreAsync(activatedPackageId);
            PackageManagerStatus = activatedIsBuiltin
                ? I18nHelper.GetLocalizedString("PackageActivatedBuiltin")
                : $"{I18nHelper.GetLocalizedString("PackageActivatedInstalled")}: {activatedPackageId}";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to activate fronted layout package {PackageId}.", SelectedPackage.PackageId);
            PackageManagerStatus = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DuplicatePackageAsync()
    {
        if (_packageManager is null || SelectedPackage is null)
        {
            return;
        }

        if (SelectedPackage.IsLocal)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString("CannotDuplicateLocalPackage");
            return;
        }

        try
        {
            var duplicated = await _packageManager.DuplicatePackageAsync(SelectedPackage.PackageId);
            await _frontedWindowService.ReloadFrontedLayoutsAsync();
            await RefreshPackagesCoreAsync(duplicated.PackageId);
            SelectedPackage = LayoutPackages.FirstOrDefault(package => package.PackageId == duplicated.PackageId) ?? SelectedPackage;
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString("LayoutPackageDuplicated")}: {duplicated.Name}";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to duplicate fronted layout package {PackageId}.", SelectedPackage.PackageId);
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString("DuplicateLayoutPackageFailed")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeletePackageAsync()
    {
        if (_packageManager is null || SelectedPackage is null)
        {
            return;
        }

        if (SelectedPackage.IsBuiltin)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString("CannotDeleteBuiltinPackage");
            return;
        }

        if (SelectedPackage.IsLocal)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString("CannotDeleteLocalPackage");
            return;
        }

        var packageId = SelectedPackage.PackageId;
        try
        {
            var confirmMessage = SelectedPackage.IsActivePackage
                ? I18nHelper.GetLocalizedString("ConfirmDeleteActivePackage")
                : I18nHelper.GetLocalizedString("ConfirmDeletePackage");
            if (!await MessageBoxHelper.ShowConfirmAsync(
                    confirmMessage,
                    I18nHelper.GetLocalizedString("Tips"),
                    I18nHelper.GetLocalizedString("Confirm"),
                    I18nHelper.GetLocalizedString("Cancel")))
            {
                return;
            }

            await _packageManager.DeletePackageAsync(packageId);
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.StopAllLoopBehaviorsAsync(FrontedBehaviorStopReason.PackageSwitched);
            }

            await _frontedWindowService.ReloadFrontedLayoutsAsync();
            PackageManagerStatus = I18nHelper.GetLocalizedString("PackageDeleted");
            SelectedPackage = null;
            await RefreshPackagesAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete fronted layout package {PackageId}.", packageId);
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString("PackageDeleteFailed")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenPackageFolder()
    {
        var folder = SelectedPackage?.InstallPath;
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = _packageManager?.GetPackageRootFolder() ?? AppConstants.FrontedLayoutPackagesPath;
        }

        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to open fronted layout package folder {Folder}.", folder);
            PackageManagerStatus = ex.Message;
        }
    }

    [RelayCommand]
    private void ShowWindow(object? windowInfo)
    {
        switch (windowInfo)
        {
            case FrontedWindowType windowType:
                _frontedWindowService.ShowWindow(windowType);
                PublishBpWindowOpenedIfTarget(windowType);
                break;
            case string id:
                _frontedWindowService.ShowWindow(id);
                PublishBpWindowOpenedIfTarget(id);
                break;
        }
    }

    private static void PublishBpWindowOpenedIfTarget(FrontedWindowType windowType)
    {
        if (windowType is FrontedWindowType.BpWindow)
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.BpWindowOpened, new { Window = windowType.ToString() });
            ActivateMainWindow();
        }
    }

    private static void PublishBpWindowOpenedIfTarget(string windowId)
    {
        var bpWindowId = FrontedWindowHelper.GetFrontedWindowGuid(FrontedWindowType.BpWindow);
        if (string.Equals(windowId, bpWindowId, StringComparison.Ordinal))
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.BpWindowOpened, new { WindowId = windowId });
            ActivateMainWindow();
        }
    }

    private static void ActivateMainWindow()
    {
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow is null)
        {
            return;
        }

        if (mainWindow.WindowState is WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
    }

    [RelayCommand]
    private void HideWindow(object? windowInfo)
    {
        switch (windowInfo)
        {
            case FrontedWindowType windowType:
                _frontedWindowService.HideWindow(windowType);
                break;
            case string id:
                _frontedWindowService.HideWindow(id);
                break;
        }
    }

    private static Window? GetShownOwnerWindow()
    {
        var current = Application.Current;
        if (current is null)
        {
            return null;
        }

        return current.Windows
                   .OfType<Window>()
                   .FirstOrDefault(window => window.IsActive && window.IsVisible)
               ?? (current.MainWindow?.IsVisible == true ? current.MainWindow : null)
               ?? current.Windows
                   .OfType<Window>()
                   .FirstOrDefault(window => window.IsVisible);
    }
}

/// <summary>
/// Fronted window group displayed by FrontManagePage.
/// </summary>
public sealed class FrontedWindowManageGroup
{
    /// <summary>
    /// Stable group key provided by the window descriptor or fallback rules.
    /// </summary>
    public string GroupKey { get; init; } = string.Empty;

    /// <summary>
    /// User-facing group display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Window cards in this group.
    /// </summary>
    public ObservableCollection<FrontedWindowManageItem> Windows { get; } = [];

    /// <summary>
    /// Builds grouped FrontManagePage items from window descriptors.
    /// </summary>
    /// <param name="descriptors">Window descriptors to group.</param>
    /// <param name="settingsHostService">Optional settings service used to resolve localized window display names.</param>
    /// <returns>Grouped fronted window manage items.</returns>
    public static IReadOnlyList<FrontedWindowManageGroup> FromDescriptors(
        IEnumerable<IFrontedWindowDescriptor> descriptors,
        ISettingsHostService? settingsHostService = null)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var groups = new List<FrontedWindowManageGroup>();
        var byKey = new Dictionary<string, FrontedWindowManageGroup>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            var key = GetStableGroupKey(descriptor);
            if (!byKey.TryGetValue(key, out var group))
            {
                group = new FrontedWindowManageGroup
                {
                    GroupKey = key,
                    DisplayName = GetGroupDisplayName(key)
                };

                byKey.Add(key, group);
                groups.Add(group);
            }

            group.Windows.Add(FrontedWindowManageItem.FromDescriptor(descriptor, settingsHostService));
        }

        return groups;
    }

    private static string GetStableGroupKey(IFrontedWindowDescriptor descriptor)
    {
        if (!string.IsNullOrWhiteSpace(descriptor.GroupKey))
        {
            return descriptor.GroupKey;
        }

        return descriptor.IsPlugin ? "Plugin" : "BuiltIn";
    }

    private static string GetGroupDisplayName(string groupKey)
    {
        return groupKey switch
        {
            "BuiltIn" => I18nHelper.GetLocalizedString("SystemBuiltIn"),
            "Plugin" => I18nHelper.GetLocalizedString("Plugins"),
            _ => groupKey
        };
    }
}

/// <summary>
/// Fronted window card displayed by FrontManagePage.
/// </summary>
public sealed class FrontedWindowManageItem
{
    /// <summary>
    /// Stable runtime window id.
    /// </summary>
    public string WindowId { get; init; } = string.Empty;

    /// <summary>
    /// User-facing window display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// User-facing descriptor kind label.
    /// </summary>
    public string KindDisplay { get; init; } = string.Empty;

    /// <summary>
    /// Full window type name used by layout paths.
    /// </summary>
    public string FullWindowType { get; init; } = string.Empty;

    /// <summary>
    /// Whether this window can be customized by Designer v3.
    /// </summary>
    public bool CanCustomize { get; init; }

    /// <summary>
    /// Creates a card item from a registry descriptor.
    /// </summary>
    /// <param name="descriptor">Window descriptor.</param>
    /// <param name="settingsHostService">Optional settings service used to resolve localized window display names.</param>
    /// <returns>A card item for FrontManagePage.</returns>
    public static FrontedWindowManageItem FromDescriptor(
        IFrontedWindowDescriptor descriptor,
        ISettingsHostService? settingsHostService = null)
    {
        return new FrontedWindowManageItem
        {
            WindowId = descriptor.WindowId,
            DisplayName = GetDescriptorDisplayName(descriptor, settingsHostService),
            FullWindowType = descriptor.FullWindowType,
            KindDisplay = descriptor.Kind switch
            {
                FrontedWindowKind.PluginXaml => I18nHelper.GetLocalizedString("FrontedWindowKind.PluginXaml"),
                FrontedWindowKind.PluginLayout => I18nHelper.GetLocalizedString("FrontedWindowKind.PluginLayout"),
                _ => I18nHelper.GetLocalizedString("FrontedWindowKind.BuiltIn")
            },
            CanCustomize = descriptor.IsV3LayoutWindow && descriptor.Customizable
        };
    }

    private static string GetDescriptorDisplayName(
        IFrontedWindowDescriptor descriptor,
        ISettingsHostService? settingsHostService)
    {
        var settings = settingsHostService?.Settings;
        return FrontedWindowDisplayNameResolver.ResolveDisplayName(
            descriptor,
            settings?.Language ?? LanguageKey.System,
            settings?.CultureInfo);
    }
}
