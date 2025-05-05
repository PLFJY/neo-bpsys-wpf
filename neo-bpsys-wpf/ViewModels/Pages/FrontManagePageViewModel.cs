using System.IO;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Views.Windows;
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

        public FrontManagePageViewModel(IFrontService frontService)
        {
            _frontService = frontService;
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

        public static event EventHandler<DesignModeChangedEventArgs>? DesignModeChanged;

        public void ToggleDesignMode(bool isDesignMode) =>
            DesignModeChanged?.Invoke(
                this,
                new DesignModeChangedEventArgs { IsDesignMode = isDesignMode }
            );

        private bool _isEditMode = false;

        public bool IsEditMode
        {
            get { return _isEditMode; }
            set
            {
                _isEditMode = value;
                ToggleDesignMode(value);
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
                MessageBox messageBox = new()
                {
                    Title = "保存提示",
                    Content = $"保存前台配置文件失败\n{ex.Message}",
                };
                await messageBox.ShowDialogAsync();
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
                    MessageBox messageBox = new()
                    {
                        Title = "加载提示",
                        Content = $"加载前台配置文件失败\n{ex.Message}",
                    };
                    await messageBox.ShowDialogAsync();
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

        [RelayCommand]
        private async Task ResetScoreWindowElementsPosition(string canvasName)
        {
            await ResetFrontWindowElementsPosision(App.Services.GetRequiredService<ScoreWindow>(), canvasName);
        }


        /// <summary>
        /// 重置指定窗口的界面配置
        /// </summary>
        /// <param name="window">从IOC里拿</param>
        /// <returns></returns>
        private async Task ResetFrontWindowElementsPosision(Window window, string canvasName = "BaseCanvas")
        {
            var messageBox = new MessageBox()
            {
                Title = "重置提示",
                Content = $"是否重置{window.GetType().Name}-{canvasName}的配置",
                PrimaryButtonText = "确认",
                PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Checkmark24 },
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
                CloseButtonText = "取消",
            };
            var result = await messageBox.ShowDialogAsync();

            if (result == MessageBoxResult.Primary)
            {
                _frontService.RestoreInitialPositions(window, canvasName);
                SaveWindowConfigAsync(window, canvasName);
            }
        }
    }
}
