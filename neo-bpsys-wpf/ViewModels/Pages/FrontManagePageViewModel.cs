using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
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

    private readonly IFrontService _frontService;
    private readonly IMessageBoxService _messageBoxService;
    private readonly ISharedDataService _sharedDataService;

    public FrontManagePageViewModel(IFrontService frontService, IMessageBoxService messageBoxService,
        ISharedDataService sharedDataService)
    {
        _frontService = frontService;
        _messageBoxService = messageBoxService;
        _sharedDataService = sharedDataService;
    }

    [RelayCommand]
    private void ShowAllWindows()
    {
        _frontService.AllWindowShow();
    }

    [RelayCommand]
    private void HideAllWindows()
    {
        _frontService.AllWindowHide();
    }

    [RelayCommand]
    private void ShowWindow(FrontWindowType windowType)
    {
        _frontService.ShowWindow(windowType);
    }

    [RelayCommand]
    private void HideWindow(FrontWindowType windowType)
    {
        _frontService.HideWindow(windowType);
    }

    #region 设计者模式

    /// <summary>
    /// 切换前台设计模式
    /// </summary>
    /// <param name="param">[0]参数: 开关信息<br/>[1]参数: 窗口类型</param>
    [RelayCommand]
    private void ChangeDesignMode(object?[] param)
    {
        if (param[0] is not bool isDesignMode || param[1] is not FrontWindowType frontWindowType) return;
        WeakReferenceMessenger.Default.Send(new DesignModeChangedMessage(this, isDesignMode, frontWindowType));
        if (!isDesignMode)
        {
            _frontService.SaveWindowElementsPosition(frontWindowType);
        }
    }

    /// <summary>
    /// 重置<see cref="BpWindow"/>的配置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private void ResetBpWindowElementsPosition()
    {
        _frontService.RestoreInitialPositions(FrontWindowType.BpWindow);
    }

    /// <summary>
    /// 重置<see cref="CutSceneWindow"/>的配置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private void ResetCutSceneWindowElementsPosition()
    {
        _frontService.RestoreInitialPositions(FrontWindowType.CutSceneWindow);
    }

    /// <summary>
    /// 重置<see cref="ScoreGlobalWindow"/>的配置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private void ResetScoreGlobalWindowElementsPosition()
    {
        _frontService.RestoreInitialPositions(FrontWindowType.ScoreGlobalWindow);
    }

    /// <summary>
    /// 重置<see cref="ScoreSurWindow"/>的配置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private void ResetScoreSurWindowElementsPosition()
    {
        _frontService.RestoreInitialPositions(FrontWindowType.ScoreSurWindow);
    }

    /// <summary>
    /// 重置<see cref="ScoreHunWindow"/>的配置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private void ResetScoreHunWindowElementsPosition()
    {
        _frontService.RestoreInitialPositions(FrontWindowType.ScoreHunWindow);
    }

    /// <summary>
    /// 重置<see cref="WidgetsWindow"/>的配置
    /// </summary>
    /// <param name="canvasName"></param>
    /// <returns></returns>
    [RelayCommand]
    private void ResetWidgetsWindowElementsPosition(string canvasName)
    {
        _frontService.RestoreInitialPositions(FrontWindowType.WidgetsWindow, canvasName);
    }

    /// <summary>
    /// 重置<see cref="GameDataWindow"/>的配置
    /// </summary>
    [RelayCommand]
    private void ResetGameDataWindowElementsPosition()
    {
        _frontService.RestoreInitialPositions(FrontWindowType.GameDataWindow);
    }

    #endregion
}