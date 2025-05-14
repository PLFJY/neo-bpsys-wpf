using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    /// <summary>
    /// 主窗口视图模型，负责管理应用程序主题、对局数据、计时器及导航菜单
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        // 设计时使用的构造函数（通过#pragma禁用空字段警告）
#pragma warning disable CS8618
        public MainWindowViewModel()
#pragma warning restore CS8618
        {
            // 设计器专用构造函数，与IsDesignTimeCreatable=True配合使用
        }

        /// <summary>
        /// 共享数据服务实例，用于跨视图模型数据交互
        /// </summary>
        public ISharedDataService SharedDataService { get; }

        /// <summary>
        /// 当前应用程序主题（默认为暗色主题）
        /// </summary>
        [ObservableProperty]
        private ApplicationTheme _applicationTheme = ApplicationTheme.Dark;

        private readonly IMessageBoxService _messageBoxService;

        /// <summary>
        /// 主窗口视图模型构造函数
        /// </summary>
        /// <param name="sharedDataService">共享数据服务依赖注入</param>
        /// <param name="messageBoxService">消息框服务依赖注入</param>
        public MainWindowViewModel(ISharedDataService sharedDataService, IMessageBoxService messageBoxService)
        {
            SharedDataService = sharedDataService;
            _messageBoxService = messageBoxService;
        }

        /// <summary>
        /// 切换应用程序主题命令（延迟60ms后应用主题变化）
        /// </summary>
        [RelayCommand]
        private async Task ThemeSwitchAsync()
        {
            await Task.Delay(60);
            ApplicationThemeManager.Apply(ApplicationTheme, WindowBackdropType.Mica, true);
        }

        /// <summary>
        /// 创建新对局命令，初始化双方队伍并生成新游戏实例
        /// </summary>
        [RelayCommand]
        private async Task NewGameAsync()
        {
            // 根据主队阵营分配求生者/监管者队伍
            Team surTeam = new(Camp.Sur);
            Team hunTeam = new(Camp.Hun);
            if (SharedDataService.MainTeam.Camp == Camp.Sur)
            {
                surTeam = SharedDataService.MainTeam;
                hunTeam = SharedDataService.AwayTeam;
            }
            else
            {
                surTeam = SharedDataService.AwayTeam;
                hunTeam = SharedDataService.MainTeam;
            }

            // 创建包含当前游戏进度的新对局
            SharedDataService.CurrentGame = new(
                surTeam,
                hunTeam,
                SharedDataService.CurrentGameProgress
            );
            await _messageBoxService.ShowInfoAsync($"已成功创建新对局\n{SharedDataService.CurrentGame.GUID}", "创建提示");
        }

        /// <summary>
        /// 交换双方阵营事件
        /// </summary>
        public static event EventHandler<EventArgs>? Swapped;

        /// <summary>
        /// 交换主客队阵营命令，同步更新相关数据
        /// </summary>
        [RelayCommand]
        private void Swap()
        {
            // 交换主客队阵营标识
            (SharedDataService.MainTeam.Camp, SharedDataService.AwayTeam.Camp) =
                (SharedDataService.AwayTeam.Camp,
                SharedDataService.MainTeam.Camp);

            // 交换游戏实例中的队伍引用
            (SharedDataService.CurrentGame.SurTeam, SharedDataService.CurrentGame.HunTeam) =
                (SharedDataService.CurrentGame.HunTeam,
                SharedDataService.CurrentGame.SurTeam);

            // 刷新当前玩家状态并同步全局Ban位数据
            SharedDataService.CurrentGame.RefreshCurrentPlayer();
            SharedDataService.MainTeam.SyncGlobalBanWithRecord();
            SharedDataService.AwayTeam.SyncGlobalBanWithRecord();

            // 触发交换事件并通知属性变更
            Swapped?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged();
        }

        /// <summary>
        /// 保存游戏信息到JSON文件命令
        /// </summary>
        [RelayCommand]
        private async Task SaveGameInfoAsync()
        {
            // 配置JSON序列化选项（美化输出/枚举转字符串）
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };

            // 构建保存路径（我的文档/neo-bpsys-wpf/GameInfoOutput）
            var json = JsonSerializer.Serialize(SharedDataService.CurrentGame, options);
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "neo-bpsys-wpf\\GameInfoOutput"
            );
            var fullPath = Path.Combine(path, $"{SharedDataService.CurrentGame.StartTime}.json");

            // 确保目录存在并执行文件写入
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            try
            {
                File.WriteAllText(fullPath, json);
                await _messageBoxService.ShowInfoAsync($"已成功保存到\n{fullPath}", "保存提示");
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowInfoAsync($"保存失败\n{ex.Message}", "保存提示");
            }
        }

        /// <summary>
        /// 启动计时器命令（需验证输入有效性）
        /// </summary>
        [RelayCommand]
        private void TimerStart()
        {
            if (int.TryParse(TimerTime, out int time))
                SharedDataService.TimerStart(time);
            else
                _messageBoxService.ShowWarningAsync("输入不合法");
        }

        /// <summary>
        /// 停止计时器命令
        /// </summary>
        [RelayCommand]
        private void TimerStop()
        {
            SharedDataService.TimerStop();
        }

        /// <summary>
        /// 计时器时间输入（默认30秒）
        /// </summary>
        public string TimerTime { get; set; } = "30";

        /// <summary>
        /// 预设计时器时长列表（单位：秒）
        /// </summary>
        public List<int> RecommendTimmerList { get; } = [30, 45, 60, 90, 120, 150, 180];

        /// <summary>
        /// 游戏进度中文字典（映射枚举值到中文描述）
        /// </summary>
        public Dictionary<GameProgress, string> GameList { get; } =
            new Dictionary<GameProgress, string>()
            {
                { GameProgress.Free, "自由对局" },
                // ...（其他映射项保持原有结构）
            };

        /// <summary>
        /// 主导航菜单项配置（包含页面类型和图标）
        /// </summary>
        public List<NavigationViewItem> MenuItems { get; } =
            [
                new("启动页", SymbolRegular.Home24, typeof(HomePage)),
                // ...（其他菜单项保持原有结构）
            ];

        /// <summary>
        /// 底部导航菜单项配置
        /// </summary>
        public List<NavigationViewItem> FooterMenuItems { get; } =
            [
                new("前台管理", SymbolRegular.ShareScreenStart24, typeof(FrontManagePage)),
                // ...（其他菜单项保持原有结构）
            ];
    }
}