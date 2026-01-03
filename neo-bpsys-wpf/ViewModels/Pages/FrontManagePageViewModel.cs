using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Views.Windows;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class FrontManagePageViewModel : ViewModelBase
{
    public FrontManagePageViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly IFrontedWindowService _frontedWindowService;
    private readonly ISharedDataService _sharedDataService;

    public FrontManagePageViewModel(IFrontedWindowService frontedWindowService, ISharedDataService sharedDataService)
    {
        _frontedWindowService = frontedWindowService;
        _sharedDataService = sharedDataService;
    }

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
    private void ShowWindow(FrontedWindowType windowType)
    {
        _frontedWindowService.ShowWindow(windowType);
    }

    [RelayCommand]
    private void HideWindow(FrontedWindowType windowType)
    {
        _frontedWindowService.HideWindow(windowType);
    }

    #region 设计者模式

    /// <summary>
    /// 切换前台设计模式
    /// </summary>
    /// <param name="param">[0]参数: 开关信息<br/>[1]参数: 窗口类型</param>
    [RelayCommand]
    private void ChangeDesignMode(object?[] param)
    {
        if (param[0] is not bool isDesignMode || param[1] is not FrontedWindowType frontWindowType) return;
        WeakReferenceMessenger.Default.Send(new DesignModeChangedMessage(this, isDesignMode, frontWindowType));
        if (!isDesignMode)
        {
            _frontedWindowService.SaveWindowElementsPosition(frontWindowType);
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