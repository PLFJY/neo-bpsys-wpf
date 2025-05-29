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

        public FrontManagePageViewModel(IFrontService frontService, IMessageBoxService messageBoxService)
        {
            _frontService = frontService;
            _messageBoxService = messageBoxService;
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

        //前台窗口大小修改

        public List<WindowResolution> WindowResolutionsList { get; } =
            [new(1440, 810), new(1920, 1080), new(960, 540)];

        //前台设计器模式
        private bool _isDesignMode = false;

        public bool IsDesignMode
        {
            get { return _isDesignMode; }
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
            SaveWindowConfigAsync(App.Services.GetRequiredService<BpWindow>());
            SaveWindowConfigAsync(App.Services.GetRequiredService<InterludeWindow>());
            SaveWindowConfigAsync(App.Services.GetRequiredService<ScoreWindow>(), "ScoreSurCanvas");
            SaveWindowConfigAsync(App.Services.GetRequiredService<ScoreWindow>(), "ScoreHunCanvas");
            SaveWindowConfigAsync(App.Services.GetRequiredService<ScoreWindow>(), "ScoreGlobalCanvas");
            SaveWindowConfigAsync(App.Services.GetRequiredService<WidgetsWindow>(), "MapBpCanvas");
        }

        private async void SaveWindowConfigAsync(Window window, string canvasName = "BaseCanvas")
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf"
            );

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var windowElementsPosition = _frontService.GetWindowElementsPosition(window, canvasName);

            try
            {
                File.WriteAllText(
                    Path.Combine(path, $"{window.GetType().Name}Config-{canvasName}.json"),
                    windowElementsPosition
                );
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync("加载提示", $"保存前台配置文件失败\n{ex.Message}");
            }
        }

        /// <summary>
        /// 加载前台窗口配置
        /// </summary>
        private async void LoadFrontConfig()
        {
            await LoadWindowConfigAsync(App.Services.GetRequiredService<BpWindow>());
            await LoadWindowConfigAsync(App.Services.GetRequiredService<InterludeWindow>());
            await LoadWindowConfigAsync(App.Services.GetRequiredService<ScoreWindow>(), "ScoreSurCanvas");
            await LoadWindowConfigAsync(App.Services.GetRequiredService<ScoreWindow>(), "ScoreHunCanvas");
            await LoadWindowConfigAsync(App.Services.GetRequiredService<ScoreWindow>(), "ScoreGlobalCanvas");
            await LoadWindowConfigAsync(App.Services.GetRequiredService<WidgetsWindow>(), "MapBpCanvas");
        }

        private async Task LoadWindowConfigAsync(Window window, string canvasName = "BaseCanvas")
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf"
            );

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            if (File.Exists(Path.Combine(path, $"{window.GetType().Name}Config-{canvasName}.json")))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(Path.Combine(path, $"{window.GetType().Name}Config-{canvasName}.json"));
                    _frontService.LoadWindowElementsPosition(window, json, canvasName);
                }
                catch (Exception ex)
                {
                    await _messageBoxService.ShowErrorAsync("加载提示", $"加载前台配置文件失败\n{ex.Message}");
                }
            }
        }

        /// <summary>
        /// 重置<see cref="BpWindow"/>的配置
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task ResetBpWindowElementsPosition()
        {
            await ResetFrontWindowElementsPosision(App.Services.GetRequiredService<BpWindow>());
        }

        /// <summary>
        /// 重置<see cref="InterludeWindow"/>的配置
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task ResetInterludeWindowElementsPosition()
        {
            await ResetFrontWindowElementsPosision(App.Services.GetRequiredService<InterludeWindow>());
        }

        /// <summary>
        /// 重置<see cref="ScoreWindow"/>的配置
        /// </summary>
        /// <param name="canvasName"></param>
        /// <returns></returns>
        [RelayCommand]
        private async Task ResetScoreWindowElementsPosition(string canvasName)
        {
            await ResetFrontWindowElementsPosision(App.Services.GetRequiredService<ScoreWindow>(), canvasName);
        }

        /// <summary>
        /// 重置<see cref="WidgetsWindow"/>的配置
        /// </summary>
        /// <param name="canvasName"></param>
        /// <returns></returns>
        [RelayCommand]
        private async Task ResetWidgetsWindowElementsPosition(string canvasName)
        {
            await ResetFrontWindowElementsPosision(App.Services.GetRequiredService<WidgetsWindow>(), canvasName);
        }


        /// <summary>
        /// 重置指定窗口的界面配置
        /// </summary>
        /// <param name="window">从IOC里拿</param>
        /// <returns></returns>
        private async Task ResetFrontWindowElementsPosision(Window window, string canvasName = "BaseCanvas")
        {
            var result = await _messageBoxService.ShowConfirmAsync("重置提示", $"是否重置{window.GetType().Name}-{canvasName}的配置");

            if (result)
            {
                _frontService.RestoreInitialPositions(window, canvasName);
                SaveWindowConfigAsync(window, canvasName);
            }
        }
    }
}
