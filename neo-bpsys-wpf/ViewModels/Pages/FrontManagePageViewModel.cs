using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.Helpers;
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
        // Decorative constructor for design-time only.
    }

    private readonly IFrontedWindowService _frontedWindowService;
    private readonly ISharedDataService _sharedDataService;
    private readonly IFilePickerService? _filePickerService;
    private readonly IServiceProvider? _serviceProvider;
    private readonly IFrontedLayoutPackageManager? _packageManager;
    private readonly IFrontedLayoutPackageExporter? _packageExporter;
    private readonly IFrontedLayoutPackageImporter? _packageImporter;
    private readonly ILogger<FrontManagePageViewModel>? _logger;
    private FrontedDesignerWindow? _frontedDesignerWindow;

    public FrontManagePageViewModel(
        IFrontedWindowService frontedWindowService,
        ISharedDataService sharedDataService,
        IFilePickerService filePickerService,
        IFrontedLayoutPackageManager packageManager,
        IFrontedLayoutPackageExporter packageExporter,
        IFrontedLayoutPackageImporter packageImporter,
        IServiceProvider serviceProvider,
        ILogger<FrontManagePageViewModel> logger)
    {
        _frontedWindowService = frontedWindowService;
        _sharedDataService = sharedDataService;
        _filePickerService = filePickerService;
        _packageManager = packageManager;
        _packageExporter = packageExporter;
        _packageImporter = packageImporter;
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

    /// <summary>
    /// 打开独立前台编辑器。
    /// </summary>
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
            var result = await _packageImporter.ImportAsync(new FrontedLayoutPackageImportRequest
            {
                PackagePath = path
            });

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
            }

            if (result.IsLegacyPackage)
            {
                PackageManagerStatus = I18nHelper.GetLocalizedString("LegacyPackageConversionNotImplemented");
                await MessageBoxHelper.ShowInfoAsync(PackageManagerStatus);
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

            if (await MessageBoxHelper.ShowConfirmAsync(
                    I18nHelper.GetLocalizedString("ActivateImportedPackage"),
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

    #region 设计者模式

    /// <summary>
    /// 切换前台设计模式
    /// </summary>
    /// <param name="param">[0]参数: 开关信息<br/>[1]参数: 窗口类型</param>
    [RelayCommand]
    private void ChangeDesignerMode(object?[] param)
    {
        if (param[0] is not bool isDesignerMode) return;
        if (param[1] is FrontedWindowType frontWindowType)
        {
            WeakReferenceMessenger.Default.Send(new DesignerModeChangedMessage(this, isDesignerMode,
                FrontedWindowHelper.GetFrontedWindowGuid(frontWindowType)));
            if (!isDesignerMode)
            {
                _frontedWindowService.SaveWindowElementsPosition(frontWindowType);
            }
        }

        if (param[1] is string id)
        {
            WeakReferenceMessenger.Default.Send(new DesignerModeChangedMessage(this, isDesignerMode, id));
            if (!isDesignerMode)
            {
                _frontedWindowService.SaveWindowElementsPosition(id);
            }
        }
    }

    /// <summary>
    /// 重置画布元素位置
    /// </summary>
    /// <param name="parm">[0]是窗口类型枚举或窗口id<br/>[1]是画布名称，如果 1不存在，则直接不传</param>
    [RelayCommand]
    private void ResetCanvasElementsPosition(object?[] parm)
    {
        // 检查数组长度，确保不会越界访问
        if (parm.Length == 0) return;

        switch (parm[0])
        {
            case FrontedWindowType frontWindowType:
                // 检查是否存在[1]，如果存在则传入画布信息，否则只传入窗口信息
                if (parm.Length > 1 && parm[1] is string canvasName1)
                {
                    _frontedWindowService.RestoreInitialPositions(frontWindowType, canvasName1);
                }
                else
                {
                    _frontedWindowService.RestoreInitialPositions(frontWindowType);
                }

                break;
            case string id:
                // 检查是否存在[1]，如果存在则传入画布信息，否则只传入窗口信息
                if (parm.Length > 1 && parm[1] is string canvasName2)
                {
                    _frontedWindowService.RestoreInitialPositions(id, canvasName2);
                }
                else
                {
                    _frontedWindowService.RestoreInitialPositions(id);
                }
                break;
        }
    }

    /// <summary>
    /// 重置<see cref="BpWindow"/>的配置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private void ResetBpWindowElementsPosition()
    {
        _frontedWindowService.RestoreInitialPositions(FrontedWindowType.BpWindow);
    }

    /// <summary>
    /// 重置<see cref="CutSceneWindow"/>的配置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private void ResetCutSceneWindowElementsPosition()
    {
        _frontedWindowService.RestoreInitialPositions(FrontedWindowType.CutSceneWindow);
    }

    /// <summary>
    /// 重置<see cref="ScoreGlobalWindow"/>的配置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private void ResetScoreGlobalWindowElementsPosition()
    {
        _frontedWindowService.RestoreInitialPositions(FrontedWindowType.ScoreGlobalWindow);
    }

    /// <summary>
    /// 重置<see cref="ScoreSurWindow"/>的配置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private void ResetScoreSurWindowElementsPosition()
    {
        _frontedWindowService.RestoreInitialPositions(FrontedWindowType.ScoreSurWindow);
    }

    /// <summary>
    /// 重置<see cref="ScoreHunWindow"/>的配置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private void ResetScoreHunWindowElementsPosition()
    {
        _frontedWindowService.RestoreInitialPositions(FrontedWindowType.ScoreHunWindow);
    }

    /// <summary>
    /// 重置<see cref="WidgetsWindow"/>的配置
    /// </summary>
    /// <param name="canvasName"></param>
    /// <returns></returns>
    [RelayCommand]
    private void ResetWidgetsWindowElementsPosition(string canvasName)
    {
        _frontedWindowService.RestoreInitialPositions(FrontedWindowType.WidgetsWindow, canvasName);
    }

    /// <summary>
    /// 重置<see cref="GameDataWindow"/>的配置
    /// </summary>
    [RelayCommand]
    private void ResetGameDataWindowElementsPosition()
    {
        _frontedWindowService.RestoreInitialPositions(FrontedWindowType.GameDataWindow);
    }

    #endregion
}
