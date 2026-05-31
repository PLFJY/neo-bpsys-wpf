using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
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
    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger<FrontManagePageViewModel>? _logger;
    private FrontedDesignerWindow? _frontedDesignerWindow;

    public FrontManagePageViewModel(
        IFrontedWindowService frontedWindowService,
        ISharedDataService sharedDataService,
        IServiceProvider serviceProvider,
        ILogger<FrontManagePageViewModel> logger)
    {
        _frontedWindowService = frontedWindowService;
        _sharedDataService = sharedDataService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        ExternalFrontedWindows = new ObservableCollection<FrontedWindowInfo>(FrontedWindowRegistryService.RegisteredWindow
            .Where(x => !x.IsBuiltIn)
            .ToList());
    }

    public ObservableCollection<FrontedWindowInfo> ExternalFrontedWindows { get; } 

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
