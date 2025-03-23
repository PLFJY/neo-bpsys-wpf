using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Views.Pages;
using System.Collections.ObjectModel;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ISharedDataService _sharedDataService;

        public MainWindowViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
        }

        [ObservableProperty] private bool _isTopmost = false;

        public ObservableCollection<int> RecommendTimmerList { get; } =
        [
            30,
            45,
            60,
            90,
            120,
            150,
            180,
            ];

        public ObservableCollection<string> GameList { get; } =
        [
            "Free",
            "Game 1 First Half",
            "Game 1 Second Half",
            "Game 2 First Half",
            "Game 2 Second Half",
            "Game 3 First Half",
            "Game 3 Second Half",
            "Game 3 Extra First Half",
            "Game 3 Extra Second Half",
            "Game 4 First Half",
            "Game 4 Second Half",
            "Game 5 First Half",
            "Game 5 Second Half",
            "Game 5 Extra First Half",
            "Game 5 Extra Second Half"
            ];

        public ObservableCollection<object> MenuItems { get; } =
        [
            new NavigationViewItem("启动页", SymbolRegular.Home24, typeof(HomePage)),
            new NavigationViewItem("队伍信息", SymbolRegular.PeopleTeam24, typeof(TeamInfoPage)),
            new NavigationViewItem("地图BP", SymbolRegular.Map24, typeof(MapBpPage)),
            new NavigationViewItem("Ban监管", SymbolRegular.PresenterOff24, typeof(BanHunPage)),
            new NavigationViewItem("Ban求生者", SymbolRegular.PersonProhibited24, typeof(BanSurPage)),
            new NavigationViewItem("角色选择", SymbolRegular.PersonAdd24, typeof(PickPage)),
            new NavigationViewItem("天赋特质", SymbolRegular.PersonWalking24, typeof(TalentPage)),
            new NavigationViewItem("比分控制", SymbolRegular.NumberRow24, typeof(ScorePage)),
            new NavigationViewItem("赛后数据", SymbolRegular.TextNumberListLtr24, typeof(GameDataPage))
            ];


        public ObservableCollection<object> FooterMenuItems { get; } =
        [
            new NavigationViewItem("前台管理", SymbolRegular.ShareScreenStart24, typeof(FrontManagePage)),
            new NavigationViewItem("扩展功能", SymbolRegular.AppsAddIn24, typeof(ExtensionPage)),
            new NavigationViewItem("设置", SymbolRegular.Settings24, typeof(SettingPage)),
            ];
    }
}
