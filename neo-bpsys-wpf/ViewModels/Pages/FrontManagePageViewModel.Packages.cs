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
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 前台管理页的Packages逻辑。
/// </summary>
public sealed partial class FrontManagePageViewModel
{
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
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "SystemBuiltIn")
                : $"{active.Name} ({active.PackageId})";
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "RefreshPackages");
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
            var result = await ImportPackageWithOptionalImageCompressionAsync(path, replaceExisting: false);

            if (result.PackageAlreadyExists && !string.IsNullOrWhiteSpace(result.PackageId))
            {
                var replace = await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ReplaceExistingPackage"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageAlreadyExists"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"));
                if (!replace)
                {
                    return;
                }

                result = await ImportPackageWithOptionalImageCompressionAsync(path, replaceExisting: true);
            }

            if (result.IsLegacyPackage)
            {
                importedFromLegacy = true;
                if (_legacyPackageConverter is null)
                {
                    PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LegacyPackageConvertFailed");
                    return;
                }

                var convert = await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LegacyPackageConvertMessage"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LegacyPackageConvertTitle"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ConvertLegacyPackage"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"));
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
                    Description = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LegacyPackageDefaultDescription"),
                    Author = string.Empty,
                    MinVersion = string.Empty,
                    InstallAfterConvert = false,
                    ActivateAfterInstall = false
                });

                if (!convertResult.Success || string.IsNullOrWhiteSpace(convertResult.ConvertedPackagePath))
                {
                    PackageManagerStatus =
                        $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LegacyPackageConvertFailed")}: {convertResult.ErrorMessage}";
                    await MessageBoxHelper.ShowErrorAsync(PackageManagerStatus);
                    return;
                }

                result = await ImportPackageWithOptionalImageCompressionAsync(
                    convertResult.ConvertedPackagePath,
                    replaceExisting: false);

                if (result.PackageAlreadyExists && !string.IsNullOrWhiteSpace(result.PackageId))
                {
                    var replace = await MessageBoxHelper.ShowConfirmAsync(
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ReplaceExistingPackage"),
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageAlreadyExists"),
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"));
                    if (!replace)
                    {
                        return;
                    }

                    result = await ImportPackageWithOptionalImageCompressionAsync(
                        convertResult.ConvertedPackagePath,
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
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LegacyPackageConvertWarnings"));
                }

                PackageManagerStatus =
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LegacyPackageConvertSucceeded")}: {packageId} "
                    + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LayoutCount")}: {convertResult.LayoutCount}, "
                    + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ResourceCount")}: {convertResult.ResourceCount}";
                if (!result.Success)
                {
                    PackageManagerStatus =
                        $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageImportFailed")}: {result.ErrorMessage}";
                    return;
                }
            }

            if (result.IsLegacyPackage)
            {
                return;
            }

            if (result.RequiresNewerApp)
            {
                PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageRequiresNewerVersion");
                return;
            }

            if (!result.Success)
            {
                PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageImportFailed")}: {result.ErrorMessage}";
                return;
            }

            await RefreshPackagesCoreAsync(result.PackageId);
            SelectedPackage = LayoutPackages.FirstOrDefault(package => package.PackageId == result.PackageId) ?? SelectedPackage;
            PackageManagerStatus =
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageImportSucceeded")}: {result.PackageId} "
                + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LayoutCount")}: {result.LayoutCount}, "
                + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ResourceCount")}: {result.ResourceCount}";
            if (!string.IsNullOrWhiteSpace(result.WarningMessage))
            {
                PackageManagerStatus += $" ({LocalizePackageWarning(result.WarningMessage)})";
            }
            if (result.CompressedImages.Count > 0)
            {
                await MessageBoxHelper.ShowInfoAsync(
                    string.Format(
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageImagesCompressed"),
                        result.CompressedImages.Count),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageImageCompressionTitle"));
            }
            if (await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, importedFromLegacy ? "ActivateConvertedPackage" : "ActivateImportedPackage"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Tips"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"))
                && !string.IsNullOrWhiteSpace(result.PackageId))
            {
                if (!await PrepareDesignerForPackageChangeAsync())
                {
                    return;
                }

                if (_behaviorRuntime is not null)
                {
                    await _behaviorRuntime.StopAllLoopBehaviorsAsync(FrontedBehaviorStopReason.PackageSwitched);
                }

                await _packageManager.ActivatePackageAsync(result.PackageId);
                if (_customWindowSynchronizer is not null)
                {
                    await _customWindowSynchronizer.RefreshAsync();
                }
                await RefreshCustomWindowViewsAsync(reloadDesignerLayout: true);
                await _frontedWindowService.ReloadFrontedLayoutsAsync();
                await RefreshPackagesCoreAsync(result.PackageId);
                SelectedPackage = LayoutPackages.FirstOrDefault(package => package.PackageId == result.PackageId) ?? SelectedPackage;
                PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageActivatedInstalled")}: {result.PackageId}";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to import fronted layout package.");
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageImportFailed")}: {ex.Message}";
        }
    }

    private async Task<FrontedLayoutPackageImportResult> ImportPackageWithOptionalImageCompressionAsync(
        string packagePath,
        bool replaceExisting)
    {
        if (_packageImporter is null)
        {
            return new FrontedLayoutPackageImportResult
            {
                Success = false,
                ErrorMessage = "Package importer is unavailable."
            };
        }

        var result = await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
        {
            PackagePath = packagePath,
            ReplaceExisting = replaceExisting
        });
        if (!result.HasOversizedImages)
        {
            return await HandleMissingPluginImportAsync(packagePath, result, replaceExisting);
        }

        var compress = await MessageBoxHelper.ShowConfirmAsync(
            string.Format(
                I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageImageCompressionMessage"),
                result.OversizedImages.Count),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageImageCompressionTitle"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CompressAndImportPackage"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"));
        if (!compress)
        {
            return result;
        }

        result = await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
        {
            PackagePath = packagePath,
            ReplaceExisting = replaceExisting,
            CompressOversizedImages = true
        });
        return await HandleMissingPluginImportAsync(packagePath, result, replaceExisting);
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
            var installMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "MissingPluginImportMessage")
                                 + Environment.NewLine
                                 + Environment.NewLine
                                 + preview
                                 + Environment.NewLine
                                 + Environment.NewLine
                                 + I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyInstallAvailableMessage")
                                 + Environment.NewLine
                                 + I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyInstallRestartNotice");
            var install = await MessageBoxHelper.ShowConfirmAsync(
                installMessage,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "MissingPluginImportTitle"),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyInstallButton"),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"));
            if (install)
            {
                try
                {
                    await InstallMarketDependenciesAsync(installableItems);
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
            I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "MissingPluginImportTitle"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Close"));
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
                    ? I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyMarketOffline")
                    : dependency.IsAvailableInMarket
                        ? I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyMarketAvailable")
                        : dependency.IsInstalled && !dependency.IsVersionSatisfied
                            ? I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyUpdateRequired")
                            : I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyNotFoundInMarket");
                var controls = dependency.AffectedControls.Count > 0
                    ? string.Join(", ", dependency.AffectedControls.Take(3).Select(control => $"{control.Window} {control.ControlName}"))
                    : string.Join(", ", dependency.RequiredBy.Take(3));
                return $"{dependency.DisplayName ?? dependency.PackageId} [{dependency.PackageId}] "
                       + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyMinVersion")}={dependency.MinVersion ?? "-"} "
                       + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyInstalledVersion")}={dependency.InstalledVersion ?? "-"} {status}"
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
                        $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyInstallFailed")}: {pluginId} {ex.Message}",
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
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyInstallFailed")}: {failed.PluginId} {failed.ErrorMessage}");
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
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "PluginDependencyInstallIncomplete")}: {string.Join(", ", pendingIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))}");
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
        if (_serviceProvider is null || _packageExporter is null || SelectedPackage is null)
        {
            return;
        }

        try
        {
            var selectedPackage = SelectedPackage;
            var window = ActivatorUtilities.CreateInstance<FrontedLayoutPackageExportWindow>(_serviceProvider);
            window.Owner = GetShownOwnerWindow();
            if (window.DataContext is FrontedLayoutPackageExportWindowViewModel exportViewModel)
            {
                exportViewModel.PackageId = selectedPackage.PackageId;
                exportViewModel.PackageName = selectedPackage.Name;
                exportViewModel.Description = selectedPackage.Description;
                exportViewModel.Author = selectedPackage.Author;
                exportViewModel.MinVersion = selectedPackage.MinVersion;
            }

            if (window.ShowDialog() != true || window.ExportRequest is null)
            {
                return;
            }

            var request = window.ExportRequest;
            request.SourcePackageId = selectedPackage.PackageId;
            if (File.Exists(request.OutputPath)
                && !await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ConfirmOverwriteFile"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Tips"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")))
            {
                return;
            }

            var result = await _packageExporter.ExportAsync(request);
            if (result.Success)
            {
                PackageManagerStatus =
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageExportSucceeded")}: {result.OutputPath} "
                    + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ExportedLayoutCount")}: {result.LayoutCount}, "
                    + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ExportedResourceCount")}: {result.ResourceCount}";
            }
            else
            {
                PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageExportFailed")}: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to export fronted layout package {PackageId}.", SelectedPackage.PackageId);
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageExportFailed")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ActivatePackageAsync()
    {
        await ActivateSelectedPackageAsync(confirm: true);
    }

    public void Receive(FrontedLayoutPackagesChangedMessage message)
    {
        if (ReferenceEquals(message.Sender, this))
        {
            return;
        }

        _ = RefreshPackagesAfterExternalChangeAsync(message.ActivePackageId);
    }

    private async Task RefreshPackagesAfterExternalChangeAsync(string? activePackageId)
    {
        await RefreshPackagesCoreAsync(activePackageId);
        await RefreshCustomWindowViewsAsync(reloadDesignerLayout: true);
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
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageActivationNotImplemented");
            return;
        }

        try
        {
            if (confirm
                && !SelectedPackage.IsActivePackage
                && !await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ConfirmActivatePackage"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Tips"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")))
            {
                return;
            }

            var activatedPackageId = SelectedPackage.PackageId;
            var activatedIsBuiltin = SelectedPackage.IsBuiltin;
            if (!await PrepareDesignerForPackageChangeAsync())
            {
                return;
            }

            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.StopAllLoopBehaviorsAsync(FrontedBehaviorStopReason.PackageSwitched);
            }

            await _packageManager.ActivatePackageAsync(activatedPackageId);
            if (_customWindowSynchronizer is not null)
            {
                await _customWindowSynchronizer.RefreshAsync();
            }
            await RefreshCustomWindowViewsAsync(reloadDesignerLayout: true);
            await _frontedWindowService.ReloadFrontedLayoutsAsync();
            WeakReferenceMessenger.Default.Send(new FrontedLayoutPackagesChangedMessage(this, activatedPackageId));
            await RefreshPackagesCoreAsync(activatedPackageId);
            PackageManagerStatus = activatedIsBuiltin
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageActivatedBuiltin")
                : $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageActivatedInstalled")}: {activatedPackageId}";
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
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CannotDuplicateLocalPackage");
            return;
        }

        try
        {
            if (!await PrepareDesignerForPackageChangeAsync())
            {
                return;
            }

            var duplicated = await _packageManager.DuplicatePackageAsync(SelectedPackage.PackageId);
            if (_customWindowSynchronizer is not null)
            {
                await _customWindowSynchronizer.RefreshAsync();
            }
            await RefreshCustomWindowViewsAsync(reloadDesignerLayout: true);
            await _frontedWindowService.ReloadFrontedLayoutsAsync();
            await RefreshPackagesCoreAsync(duplicated.PackageId);
            SelectedPackage = LayoutPackages.FirstOrDefault(package => package.PackageId == duplicated.PackageId) ?? SelectedPackage;
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LayoutPackageDuplicated")}: {duplicated.Name}";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to duplicate fronted layout package {PackageId}.", SelectedPackage.PackageId);
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "DuplicateLayoutPackageFailed")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RenamePackageAsync()
    {
        if (_packageManager is null || SelectedPackage is null)
        {
            return;
        }

        if (SelectedPackage.IsBuiltin)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CannotRenameBuiltinPackage");
            return;
        }

        if (SelectedPackage.IsLocal)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CannotRenameLocalPackage");
            return;
        }

        var contentDialogService = _serviceProvider?.GetService<IContentDialogService>();
        if (contentDialogService is null)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "RenameLayoutPackageFailed");
            return;
        }

        var packageId = SelectedPackage.PackageId;
        var nameTextBox = new TextBox
        {
            PlaceholderText = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageName"),
            Text = SelectedPackage.Name
        };
        var dialog = new ContentDialog
        {
            Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "RenameLayoutPackageDialogTitle"),
            Content = new System.Windows.Controls.StackPanel
            {
                Children =
                {
                    new System.Windows.Controls.TextBlock
                    {
                        Margin = new Thickness(0, 0, 0, 8),
                        Text = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "RenameLayoutPackageHint"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    nameTextBox
                }
            },
            PrimaryButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
            PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24),
            CloseButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"),
            CloseButtonIcon = new SymbolIcon(SymbolRegular.Dismiss24)
        };

        try
        {
            if (await contentDialogService.ShowAsync(dialog) is not ContentDialogResult.Primary)
            {
                return;
            }

            var renamed = await _packageManager.RenamePackageAsync(packageId, nameTextBox.Text);
            await RefreshPackagesCoreAsync(packageId);
            SelectedPackage = LayoutPackages.FirstOrDefault(package =>
                string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase)) ?? SelectedPackage;
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "LayoutPackageRenamed")}: {renamed.Name}";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to rename fronted layout package {PackageId}.", packageId);
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "RenameLayoutPackageFailed")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task EditPackageDescriptionAsync()
    {
        if (_packageManager is null || SelectedPackage is null)
        {
            return;
        }

        if (SelectedPackage.IsBuiltin || SelectedPackage.IsLocal)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CannotEditPackageDescription");
            return;
        }

        var contentDialogService = _serviceProvider?.GetService<IContentDialogService>();
        if (contentDialogService is null)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "EditPackageDescriptionFailed");
            return;
        }

        var packageId = SelectedPackage.PackageId;
        var descriptionTextBox = new TextBox
        {
            AcceptsReturn = true,
            MaxLength = FrontedLayoutLimits.MaxPackageDescriptionLength,
            Height = 96,
            Text = SelectedPackage.Description,
            TextWrapping = TextWrapping.Wrap
        };
        var dialog = new ContentDialog
        {
            Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "EditPackageDescriptionDialogTitle"),
            Width = 560,
            Height = 360,
            Content = descriptionTextBox,
            PrimaryButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
            PrimaryButtonIcon = new SymbolIcon(SymbolRegular.Checkmark24),
            CloseButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"),
            CloseButtonIcon = new SymbolIcon(SymbolRegular.Dismiss24)
        };

        try
        {
            if (await contentDialogService.ShowAsync(dialog) is not ContentDialogResult.Primary)
            {
                return;
            }

            var updated = await _packageManager.UpdatePackageDescriptionAsync(packageId, descriptionTextBox.Text);
            await RefreshPackagesCoreAsync(packageId);
            SelectedPackage = LayoutPackages.FirstOrDefault(package =>
                string.Equals(package.PackageId, packageId, StringComparison.OrdinalIgnoreCase)) ?? SelectedPackage;
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageDescriptionUpdated")}: {updated.Name}";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to update the description for fronted layout package {PackageId}.", packageId);
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "EditPackageDescriptionFailed")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private Task DeletePackageAsync()
    {
        return DeletePackageCoreAsync(requireConfirmation: false);
    }

    [RelayCommand]
    private Task ConfirmDeletePackageAsync()
    {
        return DeletePackageCoreAsync(requireConfirmation: true);
    }

    private async Task DeletePackageCoreAsync(bool requireConfirmation)
    {
        if (_packageManager is null || SelectedPackage is null)
        {
            return;
        }

        if (SelectedPackage.IsBuiltin)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CannotDeleteBuiltinPackage");
            return;
        }

        if (SelectedPackage.IsLocal)
        {
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "CannotDeleteLocalPackage");
            return;
        }

        var packageId = SelectedPackage.PackageId;
        try
        {
            if (requireConfirmation && !await MessageBoxHelper.ShowConfirmAsync(
                    DeletePackageConfirmationText,
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Tips"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")))
            {
                return;
            }

            if (SelectedPackage.IsActivePackage && !await PrepareDesignerForPackageChangeAsync())
            {
                return;
            }

            await _packageManager.DeletePackageAsync(packageId);
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.StopAllLoopBehaviorsAsync(FrontedBehaviorStopReason.PackageSwitched);
            }

            if (_customWindowSynchronizer is not null)
            {
                await _customWindowSynchronizer.RefreshAsync();
            }
            await RefreshCustomWindowViewsAsync(reloadDesignerLayout: true);
            await _frontedWindowService.ReloadFrontedLayoutsAsync();
            PackageManagerStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageDeleted");
            SelectedPackage = null;
            await RefreshPackagesAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete fronted layout package {PackageId}.", packageId);
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageDeleteFailed")}: {ex.Message}";
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

}
