using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Views.Pages;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ISharedDataService _sharedDataService;

        [ObservableProperty] 
        private ApplicationTheme _applicationTheme = ApplicationTheme.Dark;

        public MainWindowViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
        }

        [ObservableProperty] private bool _isTopmost = false;

        [RelayCommand]
        private void ThemeSwitch()
        {
            ApplicationThemeManager.Apply(ApplicationTheme, WindowBackdropType.Mica, true);
        }

        [RelayCommand]
        private static void Maximize()
        {
            App.Current.MainWindow.WindowState = App.Current.MainWindow.WindowState ==
                WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        [RelayCommand]
        private static void Minimize()
        {
            App.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        [RelayCommand]
        private static void Exit()
        {
            ExitConfirm();
        }

        [RelayCommand]
        private void WindowClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            ExitConfirm();
        }

        [RelayCommand]
        private void TitleBarMouseDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            App.Current.MainWindow.DragMove();
        }

        private static async void ExitConfirm()
        {
            var messageBox = new Wpf.Ui.Controls.MessageBox()
            {
                Title = "退出确认",
                Content = "是否退出程序",
                PrimaryButtonText = "退出",
                PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowExit20 },
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
                CloseButtonText = "取消",
            };
            var result = await messageBox.ShowDialogAsync();

            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary) Application.Current.Shutdown();
        }
        public List<int> RecommendTimmerList { get; } =
        [
            30,
            45,
            60,
            90,
            120,
            150,
            180,
            ];

        public List<string> GameList { get; } =
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

        public List<object> MenuItems { get; } =
        [
            new NavigationViewItem("启动页", SymbolRegular.Home24, typeof(HomePage)),
            new NavigationViewItem("队伍信息", SymbolRegular.PeopleTeam24, typeof(TeamInfoPage)),
            new NavigationViewItem("地图禁选", SymbolRegular.Map24, typeof(MapBpPage)),
            new NavigationViewItem("禁用监管者", SymbolRegular.PresenterOff24, typeof(BanHunPage)),
            new NavigationViewItem("禁用求生者", SymbolRegular.PersonProhibited24, typeof(BanSurPage)),
            new NavigationViewItem("选择角色", SymbolRegular.PersonAdd24, typeof(PickPage)),
            new NavigationViewItem("天赋特质", SymbolRegular.PersonWalking24, typeof(TalentPage)),
            new NavigationViewItem("比分控制", SymbolRegular.NumberRow24, typeof(ScorePage)),
            new NavigationViewItem("赛后数据", SymbolRegular.TextNumberListLtr24, typeof(GameDataPage))
            ];


        public List<object> FooterMenuItems { get; } =
        [
            new NavigationViewItem("前台管理", SymbolRegular.ShareScreenStart24, typeof(FrontManagePage)),
            new NavigationViewItem("扩展功能", SymbolRegular.AppsAddIn24, typeof(ExtensionPage)),
            new NavigationViewItem("设置", SymbolRegular.Settings24, typeof(SettingPage)),
            ];
    }
}
