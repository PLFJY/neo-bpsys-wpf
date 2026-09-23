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
    private readonly FrontedCustomWindowRegistrySynchronizer? _customWindowSynchronizer;
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
        _customWindowSynchronizer = serviceProvider.GetService<FrontedCustomWindowRegistrySynchronizer>();
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
    /// 按注册分组键分组后的可管理前台窗口。
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
        foreach (var group in FrontedWindowManageGroup.FromRegistrations(manageableWindows, _settingsHostService))
        {
            ManageableWindowGroups.Add(group);
            foreach (var item in group.Windows)
            {
                ManageableWindows.Add(item);
            }
        }

        if (ManageableWindowGroups.Count == 0)
        {
            foreach (var registration in manageableWindows)
            {
                ManageableWindows.Add(FrontedWindowManageItem.FromRegistration(registration, _settingsHostService));
            }
        }
    }

    private async Task RefreshCustomWindowViewsAsync(bool reloadDesignerLayout = false)
    {
        RebuildManageableWindows();
        if (_frontedDesignerWindow is { IsLoaded: true })
        {
            await _frontedDesignerWindow.RefreshWindowCatalogAsync(reloadDesignerLayout);
        }
    }

    private async Task<bool> PrepareDesignerForPackageChangeAsync()
    {
        return _frontedDesignerWindow is not { IsLoaded: true }
               || await _frontedDesignerWindow.PrepareForPackageChangeAsync();
    }

    private static string LocalizePackageWarning(string warning)
    {
        var localized = I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, warning);
        return string.Equals(localized, warning, StringComparison.Ordinal) ? warning : localized;
    }

    [ObservableProperty]
    public partial FrontedLayoutPackageInfo? SelectedPackage { get; set; }

    /// <summary>
    /// 获取当前选中布局包的删除确认提示文本。
    /// </summary>
    public string DeletePackageConfirmationText => SelectedPackage?.IsActivePackage == true
        ? I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ConfirmDeleteActivePackage")
        : I18nHelper.GetLocalizedString(AppI18nDictionaries.FrontManage, "ConfirmDeletePackage");

    partial void OnSelectedPackageChanged(FrontedLayoutPackageInfo? value)
    {
        OnPropertyChanged(nameof(DeletePackageConfirmationText));
    }

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

}
