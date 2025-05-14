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
    /// <summary>
    /// 前台管理页面视图模型，负责管理多窗口显示状态、布局配置及设计模式切换功能
    /// </summary>
    public partial class FrontManagePageViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        /// <summary>
        /// 设计时构造函数（用于XAML设计器）
        /// </summary>
        public FrontManagePageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly IFrontService _frontService;

        /// <summary>
        /// 依赖注入构造函数
        /// </summary>
        /// <param name="frontService">前台服务接口实例</param>
        public FrontManagePageViewModel(IFrontService frontService)
        {
            _frontService = frontService;
            LoadFrontConfig();
        }

        /// <summary>
        /// 显示所有前台窗口
        /// </summary>
        [RelayCommand]
        private void ShowAllWinows()
        {
            _frontService.AllWindowShow();
        }

        /// <summary>
        /// 隐藏所有前台窗口
        /// </summary>
        [RelayCommand]
        private void HideAllWinows()
        {
            _frontService.AllWindowHide();
        }

        /// <summary>
        /// 显示BP窗口
        /// </summary>
        [RelayCommand]
        private void ShowBpWindow()
        {
            _frontService.ShowWindow<BpWindow>();
        }

        /// <summary>
        /// 隐藏BP窗口
        /// </summary>
        [RelayCommand]
        private void HideBpWindow()
        {
            _frontService.HideWindow<BpWindow>();
        }

        /// <summary>
        /// 显示过场窗口
        /// </summary>
        [RelayCommand]
        private void ShowInterludeWindow()
        {
            _frontService.ShowWindow<InterludeWindow>();
        }

        /// <summary>
        /// 隐藏过场窗口
        /// </summary>
        [RelayCommand]
        private void HideInterludeWindow()
        {
            _frontService.HideWindow<InterludeWindow>();
        }

        /// <summary>
        /// 显示得分窗口
        /// </summary>
        [RelayCommand]
        private void ShowScoreWindow()
        {
            _frontService.ShowWindow<ScoreWindow>();
        }

        /// <summary>
        /// 隐藏得分窗口
        /// </summary>
        [RelayCommand]
        private void HideScoreWindow()
        {
            _frontService.HideWindow<ScoreWindow>();
        }

        /// <summary>
        /// 显示游戏数据窗口
        /// </summary>
        [RelayCommand]
        private void ShowGameDataWindow()
        {
            _frontService.ShowWindow<GameDataWindow>();
        }

        /// <summary>
        /// 隐藏游戏数据窗口
        /// </summary>
        [RelayCommand]
        private void HideGameDataWindow()
        {
            _frontService.HideWindow<GameDataWindow>();
        }

        /// <summary>
        /// 显示组件窗口
        /// </summary>
        [RelayCommand]
        private void ShowWidgetsWindow()
        {
            _frontService.ShowWindow<WidgetsWindow>();
        }

        /// <summary>
        /// 隐藏组件窗口
        /// </summary>
        [RelayCommand]
        private void HideWidgetsWindow()
        {
            _frontService.HideWindow<WidgetsWindow>();
        }

        /// <summary>
        /// 支持的窗口分辨率列表
        /// </summary>
        public List<WindowResolution> WindowResolutionsList { get; } =
            [new(1440, 810), new(1920, 1080), new(960, 540)];

        /// <summary>
        /// 设计模式变更事件
        /// </summary>
        public static event EventHandler<DesignModeChangedEventArgs>? DesignModeChanged;

        /// <summary>
        /// 触发设计模式变更事件
        /// </summary>
        /// <param name="isDesignMode">新设计模式状态</param>
        public void ToggleDesignMode(bool isDesignMode) =>
            DesignModeChanged?.Invoke(
                this,
                new DesignModeChangedEventArgs { IsDesignMode = isDesignMode }
            );

        private bool _isEditMode = false;

        /// <summary>
        /// 获取或设置编辑模式状态
        /// 设置为false时会触发配置保存
        /// </summary>
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
        /// 保存所有前台窗口的布局配置
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

        /// <summary>
        /// 异步保存指定窗口的布局配置到本地文件
        /// </summary>
        /// <param name="window">要保存配置的窗口实例</param>
        /// <param name="canvasName">画布名称（默认BaseCanvas）</param>
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
        /// 加载所有前台窗口的布局配置
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

        /// <summary>
        /// 异步从本地文件加载指定窗口的布局配置
        /// </summary>
        /// <param name="window">要加载配置的窗口实例</param>
        /// <param name="canvasName">画布名称（默认BaseCanvas）</param>
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
        /// 重置<see cref="BpWindow"/>的配置到初始状态
        /// </summary>
        /// <returns>异步操作任务</returns>
        [RelayCommand]
        private async Task ResetBpWindowElementsPosition()
        {
            await ResetFrontWindowElementsPosision(App.Services.GetRequiredService<BpWindow>());
        }

        /// <summary>
        /// 重置<see cref="InterludeWindow"/>的配置到初始状态
        /// </summary>
        /// <returns>异步操作任务</returns>
        [RelayCommand]
        private async Task ResetInterludeWindowElementsPosition()
        {
            await ResetFrontWindowElementsPosision(App.Services.GetRequiredService<InterludeWindow>());
        }

        /// <summary>
        /// 重置<see cref="ScoreWindow"/>的指定画布配置到初始状态
        /// </summary>
        /// <param name="canvasName">要重置的画布名称</param>
        /// <returns>异步操作任务</returns>
        [RelayCommand]
        private async Task ResetScoreWindowElementsPosition(string canvasName)
        {
            await ResetFrontWindowElementsPosision(App.Services.GetRequiredService<ScoreWindow>(), canvasName);
        }

        /// <summary>
        /// 重置<see cref="WidgetsWindow"/>的指定画布配置到初始状态
        /// </summary>
        /// <param name="canvasName">要重置的画布名称</param>
        /// <returns>异步操作任务</returns>
        [RelayCommand]
        private async Task ResetWidgetsWindowElementsPosition(string canvasName)
        {
            await ResetFrontWindowElementsPosision(App.Services.GetRequiredService<WidgetsWindow>(), canvasName);
        }

        /// <summary>
        /// 重置指定窗口的界面元素位置配置
        /// 显示确认对话框后执行重置操作并保存配置
        /// </summary>
        /// <param name="window">从IOC容器获取的窗口实例</param>
        /// <param name="canvasName">画布名称（默认BaseCanvas）</param>
        /// <returns>异步操作任务</returns>
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