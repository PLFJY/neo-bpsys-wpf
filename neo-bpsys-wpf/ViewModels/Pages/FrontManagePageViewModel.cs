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
using neo_bpsys_wpf.ProductTour;
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
    /// 初始化不带行为运行时的前台管理页视图模型。
    /// </summary>
    /// <param name="frontedWindowService">前台窗口服务。</param>
    /// <param name="sharedDataService">共享数据服务。</param>
    /// <param name="filePickerService">文件选择服务。</param>
    /// <param name="packageManager">布局包管理器。</param>
    /// <param name="packageExporter">布局包导出器。</param>
    /// <param name="packageImporter">布局包导入器。</param>
    /// <param name="legacyPackageConverter">旧版包转换器。</param>
    /// <param name="pluginMarketService">插件市场服务。</param>
    /// <param name="pluginInstallService">插件安装服务。</param>
    /// <param name="frontedWindowRegistry">前台窗口注册表。</param>
    /// <param name="serviceProvider">应用程序服务提供程序。</param>
    /// <param name="logger">日志记录器。</param>
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
    /// 按描述符分组键分组后的可管理前台窗口。
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
                ? string.Format(I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "StoppedLoopAnimationsFormat"), count)
                : I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "NoActiveLoopAnimations");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to stop all active loop animations.");
            PackageManagerStatus = ex.Message;
        }
    }

    [RelayCommand]
    private async Task OpenFrontedDesignerAsync()
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
            window.Owner = Application.Current?.MainWindow;
            EventHandler? closedHandler = null;
            closedHandler = (_, _) =>
            {
                window.Closed -= closedHandler;
                _frontedDesignerWindow = null;
            };
            window.Closed += closedHandler;
            _frontedDesignerWindow = window;
            try
            {
                window.Show();
                window.Activate();
                TutorialSignalPublisher.Publish(TutorialSignalIds.FrontManageOpenDesignerClicked);
            }
            catch
            {
                window.Closed -= closedHandler;
                _frontedDesignerWindow = null;
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open fronted designer window.");
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "WindowLaunchError")}\n{ex.Message}");
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
            var result = await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = path
            });
            result = await HandleMissingPluginImportAsync(path, result, replaceExisting: false);

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
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ReplaceExistingPackage"),
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageAlreadyExists"),
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"));
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
            if (await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, importedFromLegacy ? "ActivateConvertedPackage" : "ActivateImportedPackage"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Tips"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"))
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
                PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageActivatedInstalled")}: {result.PackageId}";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to import fronted layout package.");
            PackageManagerStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "PackageImportFailed")}: {ex.Message}";
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
            _logger?.LogWarning(ex, "Failed to export fronted layout package.");
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
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.StopAllLoopBehaviorsAsync(FrontedBehaviorStopReason.PackageSwitched);
            }

            await _packageManager.ActivatePackageAsync(activatedPackageId);
            await _frontedWindowService.ReloadFrontedLayoutsAsync();
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
            var duplicated = await _packageManager.DuplicatePackageAsync(SelectedPackage.PackageId);
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
    private async Task DeletePackageAsync()
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
            var confirmMessage = SelectedPackage.IsActivePackage
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ConfirmDeleteActivePackage")
                : I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ConfirmDeletePackage");
            if (!await MessageBoxHelper.ShowConfirmAsync(
                    confirmMessage,
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Tips"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")))
            {
                return;
            }

            await _packageManager.DeletePackageAsync(packageId);
            if (_behaviorRuntime is not null)
            {
                await _behaviorRuntime.StopAllLoopBehaviorsAsync(FrontedBehaviorStopReason.PackageSwitched);
            }

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
/// 前台管理页显示的前台窗口分组。
/// </summary>
public sealed class FrontedWindowManageGroup
{
    /// <summary>
    /// 由窗口描述符或回退规则提供的稳定分组键。
    /// </summary>
    public string GroupKey { get; init; } = string.Empty;

    /// <summary>
    /// 面向用户的分组显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 此分组中的窗口卡片。
    /// </summary>
    public ObservableCollection<FrontedWindowManageItem> Windows { get; } = [];

    /// <summary>
    /// 根据窗口描述符构建分组后的前台管理页条目。
    /// </summary>
    /// <param name="descriptors">要分组的窗口描述符。</param>
    /// <param name="settingsHostService">可选的设置服务，用于解析本地化的窗口显示名称。</param>
    /// <returns>分组后的前台窗口管理条目。</returns>
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
            "BuiltIn" => I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "SystemBuiltIn"),
            "Plugin" => I18nHelper.GetLocalizedString(AppI18nDictionaries.PluginMarket, "Plugins"),
            _ => groupKey
        };
    }
}

/// <summary>
/// 前台管理页显示的前台窗口卡片。
/// </summary>
public sealed class FrontedWindowManageItem
{
    /// <summary>
    /// 稳定的运行时窗口 ID。
    /// </summary>
    public string WindowId { get; init; } = string.Empty;

    /// <summary>
    /// 面向用户的窗口显示名称。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 面向用户的描述符类型标签。
    /// </summary>
    public string KindDisplay { get; init; } = string.Empty;

    /// <summary>
    /// 布局路径使用的完整窗口类型名。
    /// </summary>
    public string FullWindowType { get; init; } = string.Empty;

    /// <summary>
    /// 此窗口是否可由设计器 v3 自定义。
    /// </summary>
    public bool CanCustomize { get; init; }

    /// <summary>
    /// 根据注册表描述符创建卡片条目。
    /// </summary>
    /// <param name="descriptor">窗口描述符。</param>
    /// <param name="settingsHostService">可选的设置服务，用于解析本地化的窗口显示名称。</param>
    /// <returns>用于前台管理页的卡片条目。</returns>
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
                FrontedWindowKind.PluginXaml => I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "FrontedWindowKind.PluginXaml"),
                FrontedWindowKind.PluginLayout => I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "FrontedWindowKind.PluginLayout"),
                _ => I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "FrontedWindowKind.BuiltIn")
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
