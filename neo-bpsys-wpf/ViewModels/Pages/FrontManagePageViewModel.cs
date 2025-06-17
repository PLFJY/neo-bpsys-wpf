using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Views.Windows;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;
using static neo_bpsys_wpf.Services.FrontService;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class FrontManagePageViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public FrontManagePageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly IFrontService _frontService;
        private readonly IMessageBoxService _messageBoxService;
        private readonly ISharedDataService _sharedDataService;

        public FrontManagePageViewModel(IFrontService frontService, IMessageBoxService messageBoxService, ISharedDataService sharedDataService)
        {
            _frontService = frontService;
            _messageBoxService = messageBoxService;
            _sharedDataService = sharedDataService;
            _globalScoreTotalMargin = _sharedDataService.GlobalScoreTotalMargin;
            LoadFrontConfig();
        }

        [RelayCommand]
        private void ShowAllWinows()
        {
            _frontService.AllWindowShow();
        }

        [RelayCommand]
        private void HideAllWinows()
        {
            _frontService.AllWindowHide();
        }

        [RelayCommand]
        private void ShowBpWindow()
        {
            _frontService.ShowWindow<BpWindow>();
        }

        [RelayCommand]
        private void HideBpWindow()
        {
            _frontService.HideWindow<BpWindow>();
        }

        [RelayCommand]
        private void ShowInterludeWindow()
        {
            _frontService.ShowWindow<InterludeWindow>();
        }

        [RelayCommand]
        private void HideInterludeWindow()
        {
            _frontService.HideWindow<InterludeWindow>();
        }

        [RelayCommand]
        private void ShowScoreWindow()
        {
            _frontService.ShowWindow<ScoreWindow>();
        }

        [RelayCommand]
        private void HideScoreWindow()
        {
            _frontService.HideWindow<ScoreWindow>();
        }

        [RelayCommand]
        private void ShowGameDataWindow()
        {
            _frontService.ShowWindow<GameDataWindow>();
        }

        [RelayCommand]
        private void HideGameDataWindow()
        {
            _frontService.HideWindow<GameDataWindow>();
        }

        [RelayCommand]
        private void ShowWidgetsWindow()
        {
            _frontService.ShowWindow<WidgetsWindow>();
        }

        [RelayCommand]
        private void HideWidgetsWindow()
        {
            _frontService.HideWindow<WidgetsWindow>();
        }

        private double _globalScoreTotalMargin = 390;

        public double GlobalScoreTotalMargin
        {
            get => _globalScoreTotalMargin;
            set
            {
                _globalScoreTotalMargin = value;
                _sharedDataService.GlobalScoreTotalMargin = _globalScoreTotalMargin;
            }
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
                    SaveFrontConfig(); //保存前台窗口配置
                }
            }
        }

        /// <summary>
        /// 保存前台窗口配置
        /// </summary>
        private void SaveFrontConfig()
        {
            _frontService.SaveWindowElementsPosition<BpWindow>();
            _frontService.SaveWindowElementsPosition<InterludeWindow>();
            _frontService.SaveWindowElementsPosition<ScoreWindow>("ScoreSurCanvas");
            _frontService.SaveWindowElementsPosition<ScoreWindow>("ScoreHunCanvas");
            _frontService.SaveWindowElementsPosition<ScoreWindow>("ScoreGlobalCanvas");
            _frontService.SaveWindowElementsPosition<WidgetsWindow>("MapBpCanvas");
        }

        /// <summary>
        /// 加载前台窗口配置
        /// </summary>
        private async void LoadFrontConfig()
        {
            await _frontService.LoadWindowElementsPositionOnStartupAsync<BpWindow>();
            await _frontService.LoadWindowElementsPositionOnStartupAsync<InterludeWindow>();
            await _frontService.LoadWindowElementsPositionOnStartupAsync<ScoreWindow>("ScoreSurCanvas");
            await _frontService.LoadWindowElementsPositionOnStartupAsync<ScoreWindow>("ScoreHunCanvas");
            await _frontService.LoadWindowElementsPositionOnStartupAsync<ScoreWindow>("ScoreGlobalCanvas");
            await _frontService.LoadWindowElementsPositionOnStartupAsync<WidgetsWindow>("MapBpCanvas");
        }

        /// <summary>
        /// 重置<see cref="BpWindow"/>的配置
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private void ResetBpWindowElementsPosition()
        {
            _frontService.RestoreInitialPositions<BpWindow>();
        }

        /// <summary>
        /// 重置<see cref="InterludeWindow"/>的配置
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private void ResetInterludeWindowElementsPosition()
        {
            _frontService.RestoreInitialPositions<InterludeWindow>();
        }

        /// <summary>
        /// 重置<see cref="ScoreWindow"/>的配置
        /// </summary>
        /// <param name="canvasName"></param>
        /// <returns></returns>
        [RelayCommand]
        private void ResetScoreWindowElementsPosition(string canvasName)
        {
            _frontService.RestoreInitialPositions<ScoreWindow>(canvasName);
        }

        /// <summary>
        /// 重置<see cref="WidgetsWindow"/>的配置
        /// </summary>
        /// <param name="canvasName"></param>
        /// <returns></returns>
        [RelayCommand]
        private void ResetWidgetsWindowElementsPosition(string canvasName)
        {
            _frontService.RestoreInitialPositions<WidgetsWindow>(canvasName);
        }

        [RelayCommand]
        private void ResetGameDataWindowElementsPosition()
        {
            _frontService.RestoreInitialPositions<GameDataWindow>();
        }
    }
}
