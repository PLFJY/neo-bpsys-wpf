using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class FrontManagePageViewModel : ViewModelBase
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
        _serviceProvider = serviceProvider;
        _logger = logger;
        ExternalFrontedWindows = new ObservableCollection<FrontedWindowInfo>(FrontedWindowRegistryService.RegisteredWindow
            .Where(x => !x.IsBuiltIn)
            .ToList());
        _ = RefreshPackagesAsync();
    }

    public ObservableCollection<FrontedWindowInfo> ExternalFrontedWindows { get; } = [];

    public ObservableCollection<FrontedLayoutPackageInfo> LayoutPackages { get; } = [];

    [ObservableProperty]
    private FrontedLayoutPackageInfo? _selectedPackage;

    [ObservableProperty]
    private string _activePackageDisplay = "builtin";

    [ObservableProperty]
    private string _packageManagerStatus = string.Empty;

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
            window.Owner = Application.Current.MainWindow;
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
        if (_packageManager is null)
        {
            return;
        }

        try
        {
            var packages = await _packageManager.ListPackagesAsync();
            LayoutPackages.Clear();
            foreach (var package in packages)
            {
                LayoutPackages.Add(package);
            }

            SelectedPackage ??= LayoutPackages.FirstOrDefault();
            var active = packages.FirstOrDefault(package => package.IsActive)
                         ?? packages.FirstOrDefault(package => package.IsBuiltin);
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

                if (convertResult.Warnings.Count > 0)
                {
                    await MessageBoxHelper.ShowInfoAsync(
                        string.Join(Environment.NewLine, convertResult.Warnings.Take(12)),
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

            await RefreshPackagesAsync();
            SelectedPackage = LayoutPackages.FirstOrDefault(package => package.PackageId == result.PackageId) ?? SelectedPackage;
            PackageManagerStatus =
                $"{I18nHelper.GetLocalizedString("PackageImportSucceeded")}: {result.PackageId} "
                + $"{I18nHelper.GetLocalizedString("LayoutCount")}: {result.LayoutCount}, "
                + $"{I18nHelper.GetLocalizedString("ResourceCount")}: {result.ResourceCount}";
            if (result.RemovedPluginControls.Count > 0)
            {
                PackageManagerStatus += $", {I18nHelper.GetLocalizedString("RemovedPluginControlCount")}: {result.RemovedPluginControls.Count}";
                await MessageBoxHelper.ShowInfoAsync(
                    string.Join(
                        Environment.NewLine,
                        result.RemovedPluginControls
                            .Take(12)
                            .Select(control => $"{control.Window}/{control.Canvas} {control.ControlName}: {control.ControlType}")),
                    I18nHelper.GetLocalizedString("RemovedPluginControlsReport"),
                    I18nHelper.GetLocalizedString("Close"));
            }

            if (await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString(importedFromLegacy ? "ActivateConvertedPackage" : "ActivateImportedPackage"),
                    I18nHelper.GetLocalizedString("Tips"),
                    I18nHelper.GetLocalizedString("Confirm"),
                    I18nHelper.GetLocalizedString("Cancel"))
                && !string.IsNullOrWhiteSpace(result.PackageId))
            {
                await _packageManager.ActivatePackageAsync(result.PackageId);
                await _frontedWindowService.ReloadFrontedLayoutsAsync();
                await RefreshPackagesAsync();
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
        if (_packageImporter is null
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
                                 + "可从插件市场安装或更新的插件需要用户确认。安装或更新插件后通常需要重启，当前导入不会自动继续。";
            var install = await MessageBoxHelper.ShowConfirmAsync(
                installMessage,
                I18nHelper.GetLocalizedString("MissingPluginImportTitle"),
                "安装/更新插件",
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

                return result;
            }
        }

        var message = I18nHelper.GetLocalizedString("MissingPluginImportMessage")
                      + Environment.NewLine
                      + Environment.NewLine
                      + preview
                      + Environment.NewLine
                      + Environment.NewLine
                      + "可选择强制导入并删除这些插件控件，原始 .bpui 文件不会被修改。";

        var force = await MessageBoxHelper.ShowConfirmAsync(
            message,
            I18nHelper.GetLocalizedString("MissingPluginImportTitle"),
            I18nHelper.GetLocalizedString("ForceImportRemoveMissingControls"),
            I18nHelper.GetLocalizedString("Cancel"));
        if (!force)
        {
            return result;
        }

        return await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
        {
            PackagePath = packagePath,
            ReplaceExisting = replaceExisting,
            MissingPluginPolicy = FrontedLayoutPackageMissingPluginPolicy.ForceRemoveMissingControls
        });
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
                .GroupBy(control => $"{control.Window}/{control.Canvas}/{control.ControlName}", StringComparer.Ordinal)
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
                    ? "market offline"
                    : dependency.IsAvailableInMarket
                        ? "market available"
                        : dependency.IsInstalled && !dependency.IsVersionSatisfied
                            ? "update required"
                            : "not found in market";
                var controls = dependency.AffectedControls.Count > 0
                    ? string.Join(", ", dependency.AffectedControls.Take(3).Select(control => $"{control.Window}/{control.Canvas} {control.ControlName}"))
                    : string.Join(", ", dependency.RequiredBy.Take(3));
                return $"{dependency.DisplayName ?? dependency.PackageId} [{dependency.PackageId}] "
                       + $"min={dependency.MinVersion ?? "-"} installed={dependency.InstalledVersion ?? "-"} {status}"
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
                throw new InvalidOperationException(failed.ErrorMessage);
            }

            if (!_pluginMarketService.IsDownloading
                && !_pluginMarketService.DownloadQueue.Any(item => pendingIds.Contains(item.PluginId) && item.IsInProgress))
            {
                break;
            }

            await Task.Delay(250);
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
            window.Owner = Application.Current.MainWindow;
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
            if (!SelectedPackage.IsActive
                && !await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString("ConfirmActivatePackage"),
                    I18nHelper.GetLocalizedString("Tips"),
                    I18nHelper.GetLocalizedString("Confirm"),
                    I18nHelper.GetLocalizedString("Cancel")))
            {
                return;
            }

            await _packageManager.ActivatePackageAsync(SelectedPackage.PackageId);
            await _frontedWindowService.ReloadFrontedLayoutsAsync();
            PackageManagerStatus = SelectedPackage.IsBuiltin
                ? I18nHelper.GetLocalizedString("PackageActivatedBuiltin")
                : $"{I18nHelper.GetLocalizedString("PackageActivatedInstalled")}: {SelectedPackage.PackageId}";
            await RefreshPackagesAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to activate fronted layout package {PackageId}.", SelectedPackage.PackageId);
            PackageManagerStatus = ex.Message;
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
            var confirmMessage = SelectedPackage.IsActive
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
                break;
            case string id:
                _frontedWindowService.ShowWindow(id);
                break;
        }
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
}
