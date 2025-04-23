using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Views.Pages;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        public MainWindowViewModel()
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        [ObservableProperty]
        private ApplicationTheme _applicationTheme = ApplicationTheme.Dark;

        public MainWindowViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
        }

        [RelayCommand]
        private async Task ThemeSwitchAsync()
        {
            await Task.Delay(60);
            ApplicationThemeManager.Apply(ApplicationTheme, WindowBackdropType.Mica, true);
        }

        [RelayCommand]
        private void NewGame()
        {
            SharedDataService.CurrentGame = new(
                SharedDataService.CurrentSurTeam,
                SharedDataService.CurrentHunTeam,
                SharedDataService.CurrentGameProgress
            );
            Wpf.Ui.Controls.MessageBox messageBox = new()
            {
                Title = "创建提示",
                Content = $"已成功创建新对局\n{SharedDataService.CurrentGame.GUID}",
            };
            messageBox.ShowDialogAsync();
        }

        [RelayCommand]
        private void Swap()
        {
            (SharedDataService.CurrentSurTeam, SharedDataService.CurrentHunTeam) = (
                SharedDataService.CurrentHunTeam,
                SharedDataService.CurrentSurTeam
            );
        }

        [RelayCommand]
        private void SaveGameInfo()
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
                Wpf.Ui.Controls.MessageBox messageBox = new()
                {
                    Title = "保存提示",
                    Content = $"已成功保存到\n{fullPath}",
                };
                messageBox.ShowDialogAsync();
            }
            catch (Exception ex)
            {
                Wpf.Ui.Controls.MessageBox messageBox = new()
                {
                    Title = "保存提示",
                    Content = $"保存失败\n{ex.Message}",
                };
                messageBox.ShowDialogAsync();
            }
        }

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
