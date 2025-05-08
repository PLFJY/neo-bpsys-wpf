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
    public partial class MainWindowViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public MainWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        [ObservableProperty]
        private ApplicationTheme _applicationTheme = ApplicationTheme.Dark;
        private readonly IMessageBoxService _messageBoxService;

        public MainWindowViewModel(ISharedDataService sharedDataService, IMessageBoxService messageBoxService)
        {
            SharedDataService = sharedDataService;
            _messageBoxService = messageBoxService;
        }


        [RelayCommand]
        private async Task ThemeSwitchAsync()
        {
            await Task.Delay(60);
            ApplicationThemeManager.Apply(ApplicationTheme, WindowBackdropType.Mica, true);
        }

        [RelayCommand]
        private async Task NewGameAsync()
        {
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
            SharedDataService.CurrentGame = new(
                surTeam,
                hunTeam,
                SharedDataService.CurrentGameProgress
            );
            await _messageBoxService.ShowInfoAsync($"已成功创建新对局\n{SharedDataService.CurrentGame.GUID}", "创建提示");
        }

        public static event EventHandler<EventArgs>? Swapped;

        [RelayCommand]
        private void Swap()
        {
            (SharedDataService.MainTeam.Camp, SharedDataService.AwayTeam.Camp) = 
                (SharedDataService.AwayTeam.Camp,
                SharedDataService.MainTeam.Camp);
            (SharedDataService.CurrentGame.SurTeam, SharedDataService.CurrentGame.HunTeam) =
                (SharedDataService.CurrentGame.HunTeam,
                SharedDataService.CurrentGame.SurTeam);
            SharedDataService.CurrentGame.RefreshCurrentPlayer();

            SharedDataService.MainTeam.SyncGlobalBanWithRecord();
            SharedDataService.AwayTeam.SyncGlobalBanWithRecord();

            Swapped?.Invoke(this, EventArgs.Empty);

            OnPropertyChanged();
        }

        [RelayCommand]
        private async Task SaveGameInfoAsync()
        {
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
                //Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = JsonSerializer.Serialize(SharedDataService.CurrentGame, options);
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "neo-bpsys-wpf\\GameInfoOutput"
            );
            var fullPath = Path.Combine(path, $"{SharedDataService.CurrentGame.StartTime}.json");
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


        [RelayCommand]
        private void TimerStart()
        {
            if (int.TryParse(TimerTime, out int time))
                SharedDataService.TimerStart(time);
            else
                _messageBoxService.ShowWarningAsync("输入不合法");
        }

        [RelayCommand]
        private void TimerStop()
        {
            SharedDataService.TimerStop();
        }

        public string TimerTime { get; set; } = "30";

        public List<int> RecommendTimmerList { get; } = [30, 45, 60, 90, 120, 150, 180];

        public Dictionary<GameProgress, string> GameList { get; } =
            new Dictionary<GameProgress, string>()
            {
                { GameProgress.Free, "自由对局" },
                { GameProgress.Game1FirstHalf, "BO1上半" },
                { GameProgress.Game1SecondHalf, "BO1下半" },
                { GameProgress.Game2FirstHalf, "BO2上半" },
                { GameProgress.Game2SecondHalf, "BO2下半" },
                { GameProgress.Game3FirstHalf, "BO3上半" },
                { GameProgress.Game3SecondHalf, "BO3下半" },
                { GameProgress.Game3ExtraFirstHalf, "BO3加赛上半" },
                { GameProgress.Game3ExtraSecondHalf, "BO3加赛下半" },
                { GameProgress.Game4FirstHalf, "BO4上半" },
                { GameProgress.Game4SecondHalf, "BO4下半" },
                { GameProgress.Game5FirstHalf, "BO5上半" },
                { GameProgress.Game5SecondHalf, "BO5下半" },
                { GameProgress.Game5ExtraFirstHalf, "BO5加赛上半" },
                { GameProgress.Game5ExtraSecondHalf, "BO5加赛下半" },
            };

        public List<NavigationViewItem> MenuItems { get; } =
            [
                new("启动页", SymbolRegular.Home24, typeof(HomePage)),
                new("队伍信息", SymbolRegular.PeopleTeam24, typeof(TeamInfoPage)),
                new("地图禁选", SymbolRegular.Map24, typeof(MapBpPage)),
                new("禁用监管者", SymbolRegular.PresenterOff24, typeof(BanHunPage)),
                new("禁用求生者", SymbolRegular.PersonProhibited24, typeof(BanSurPage)),
                new("选择角色", SymbolRegular.PersonAdd24, typeof(PickPage)),
                new("天赋特质", SymbolRegular.PersonWalking24, typeof(TalentPage)),
                new("比分控制", SymbolRegular.NumberRow24, typeof(ScorePage)),
                new("赛后数据", SymbolRegular.TextNumberListLtr24, typeof(GameDataPage)),
            ];

        public List<NavigationViewItem> FooterMenuItems { get; } =
            [
                new("前台管理", SymbolRegular.ShareScreenStart24, typeof(FrontManagePage)),
                new("扩展功能", SymbolRegular.AppsAddIn24, typeof(ExtensionPage)),
                new("设置", SymbolRegular.Settings24, typeof(SettingPage)),
            ];
    }
}
