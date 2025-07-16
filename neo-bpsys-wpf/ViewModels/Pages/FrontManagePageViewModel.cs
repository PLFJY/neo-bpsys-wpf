using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Views.Windows;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class FrontManagePageViewModel : ObservableObject
    {
        private readonly ILogger _logger;
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public FrontManagePageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly IFrontService _frontService;
        private readonly IMessageBoxService _messageBoxService;
        private readonly ISharedDataService _sharedDataService;

        public FrontManagePageViewModel(ILogger<FrontManagePageViewModel> logger, IFrontService frontService, IMessageBoxService messageBoxService, ISharedDataService sharedDataService)
        {
            _frontService = frontService;
            _messageBoxService = messageBoxService;
            _sharedDataService = sharedDataService;
            _logger = logger;
        }

        [RelayCommand]
        private void ShowAllWindows()
        {
            _frontService.AllWindowShow();
            _logger.LogInformation("All windows shown.");
        }

        [RelayCommand]
        private void HideAllWindows()
        {
            _frontService.AllWindowHide();
            _logger.LogInformation("All windows hidden.");
        }

        [RelayCommand]
        private void ShowBpWindow()
        {
            _frontService.ShowWindow(FrontWindowType.BpWindow);
            _logger.LogInformation("BpWindow shown.");
        }

        [RelayCommand]
        private void HideBpWindow()
        {
            _frontService.HideWindow(FrontWindowType.BpWindow);
            _logger.LogInformation("BpWindow hidden.");
        }

        [RelayCommand]
        private void ShowCutSceneWindow()
        {
            _frontService.ShowWindow(FrontWindowType.CutSceneWindow);
            _logger.LogInformation("CutSceneWindow shown.");
        }

        [RelayCommand]
        private void HideCutSceneWindow()
        {
            _frontService.HideWindow(FrontWindowType.CutSceneWindow);
            _logger.LogInformation("CutSceneWindow hidden.");
        }

        [RelayCommand]
        private void ShowScoreWindow()
        {
            _frontService.ShowWindow(FrontWindowType.ScoreWindow);
            _logger.LogInformation("ScoreWindow shown.");
        }

        [RelayCommand]
        private void HideScoreWindow()
        {
            _frontService.HideWindow(FrontWindowType.ScoreWindow);
            _logger.LogInformation("ScoreWindow hidden.");
        }

        [RelayCommand]
        private void ShowGameDataWindow()
        {
            _frontService.ShowWindow(FrontWindowType.GameDataWindow);
            _logger.LogInformation("GameDataWindow shown.");
        }

        [RelayCommand]
        private void HideGameDataWindow()
        {
            _frontService.HideWindow(FrontWindowType.GameDataWindow);
            _logger.LogInformation("GameDataWindow hidden.");
        }

        [RelayCommand]
        private void ShowWidgetsWindow()
        {
            _frontService.ShowWindow(FrontWindowType.WidgetsWindow);
            _logger.LogInformation("WidgetsWindow shown.");
        }

        [RelayCommand]
        private void HideWidgetsWindow()
        {
            _frontService.HideWindow(FrontWindowType.WidgetsWindow);
            _logger.LogInformation("WidgetsWindow hidden.");
        }

        //前台设计器模式
        private bool _isDesignMode = false;

        public bool IsDesignMode
        {
            get => _isDesignMode;
            set
            {
                _isDesignMode = value;
                WeakReferenceMessenger.Default.Send(new DesignModeChangedMessage(this, _isDesignMode));
                if (!value)
                {
                    _frontService.SaveAllWindowElementsPosition();
                    _logger.LogInformation("Design mode disabled, all window positions saved.");
                }
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
            _logger.LogInformation("BpWindow elements position reset to initial state.");
        }

        /// <summary>
        /// 重置<see cref="CutSceneWindow"/>的配置
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private void ResetCutSceneWindowElementsPosition()
        {
            _frontService.RestoreInitialPositions(FrontWindowType.CutSceneWindow);
            _logger.LogInformation("CutSceneWindow elements position reset to initial state.");
        }

        /// <summary>
        /// 重置<see cref="ScoreWindow"/>的配置
        /// </summary>
        /// <param name="canvasName"></param>
        /// <returns></returns>
        [RelayCommand]
        private void ResetScoreWindowElementsPosition(string canvasName)
        {
            _frontService.RestoreInitialPositions(FrontWindowType.ScoreWindow, canvasName);
            _logger.LogInformation($"ScoreWindow elements position reset to initial state for canvas: {canvasName}.");
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
            _logger.LogInformation($"WidgetsWindow elements position reset to initial state for canvas: {canvasName}.");
        }

        [RelayCommand]
        private void ResetGameDataWindowElementsPosition()
        {
            _frontService.RestoreInitialPositions(FrontWindowType.GameDataWindow);
            _logger.LogInformation("GameDataWindow elements position reset to initial state.");
        }
    }
}
