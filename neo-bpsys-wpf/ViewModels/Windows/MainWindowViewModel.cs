using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using neo_bpsys_wpf.Exceptions;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableRecipient,
        IRecipient<NewGameMessage>,
        IRecipient<SwapMessage>,
        IRecipient<ValueChangedMessage<string>>,
        IRecipient<PropertyChangedMessage<bool>>,
        IRecipient<HighlightMessage>
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public MainWindowViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly ISharedDataService _sharedDataService;

        private readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly IMessageBoxService _messageBoxService;
        private readonly IGameGuidanceService _gameGuidanceService;
        private readonly IInfoBarService _infoBarService;
        [ObservableProperty] private ApplicationTheme _applicationTheme = ApplicationTheme.Dark;

        private bool _isGuidanceStarted;

        public bool IsGuidanceStarted
        {
            get => _isGuidanceStarted;
            set
            {
                _isGuidanceStarted = value;
                OnPropertyChanged(nameof(IsGuidanceStarted));
                OnPropertyChanged(nameof(CanGameProgressChange));
            }
        }

        public bool CanGameProgressChange => !IsGuidanceStarted;


        private GameProgress _selectedGameProgress = GameProgress.Free;

        public string SurTeamName => _sharedDataService.CurrentGame.SurTeam.Name;
        public string HunTeamName => _sharedDataService.CurrentGame.HunTeam.Name;
        public string RemainingSeconds => _sharedDataService.RemainingSeconds;

        public GameProgress SelectedGameProgress
        {
            get => _selectedGameProgress;
            set
            {
                _selectedGameProgress = value;
                _sharedDataService.CurrentGame.GameProgress = _selectedGameProgress;
            }
        }

        [ObservableProperty] private string _actionName = string.Empty;

        public MainWindowViewModel(
            ISharedDataService sharedDataService,
            IMessageBoxService messageBoxService,
            IGameGuidanceService gameGuidanceService,
            IInfoBarService infoBarService)
        {
            _sharedDataService = sharedDataService;
            _messageBoxService = messageBoxService;
            _gameGuidanceService = gameGuidanceService;
            _infoBarService = infoBarService;
            _isGuidanceStarted = false;
            jsonSerializerOptions = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
            };
            IsActive = true;
            GameList = GameListBo5;
            IsBo3Mode = _sharedDataService.IsBo3Mode;
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
            Team surTeam;
            Team hunTeam;
            if (_sharedDataService.MainTeam.Camp == Camp.Sur)
            {
                surTeam = _sharedDataService.MainTeam;
                hunTeam = _sharedDataService.AwayTeam;
            }
            else
            {
                surTeam = _sharedDataService.AwayTeam;
                hunTeam = _sharedDataService.MainTeam;
            }

            Map? pickedMap = _sharedDataService.CurrentGame.PickedMap;
            Map? bannedMap = _sharedDataService.CurrentGame.BannedMap;

            _sharedDataService.CurrentGame = new Game(surTeam, hunTeam, SelectedGameProgress)
            {
                PickedMap = pickedMap,
                BannedMap = bannedMap
            };

            //发送新对局已创建的消息
            WeakReferenceMessenger.Default.Send(new NewGameMessage(this, true));

            await _messageBoxService.ShowInfoAsync($"已成功创建新对局\n{_sharedDataService.CurrentGame.GUID}", "创建提示");
            OnPropertyChanged();
        }

        [RelayCommand]
        private void Swap()
        {
            //交换阵营
            (_sharedDataService.MainTeam.Camp, _sharedDataService.AwayTeam.Camp) =
                (_sharedDataService.AwayTeam.Camp, _sharedDataService.MainTeam.Camp);
            //交换队伍
            (_sharedDataService.CurrentGame.SurTeam, _sharedDataService.CurrentGame.HunTeam) =
                (_sharedDataService.CurrentGame.HunTeam, _sharedDataService.CurrentGame.SurTeam);
            _sharedDataService.CurrentGame.RefreshCurrentPlayer();

            WeakReferenceMessenger.Default.Send(new SwapMessage(this, true));
            OnPropertyChanged();
        }

        [RelayCommand]
        private async Task SaveGameInfoAsync()
        {
            var json = JsonSerializer.Serialize(_sharedDataService.CurrentGame, jsonSerializerOptions);
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "neo-bpsys-wpf\\GameInfoOutput"
            );
            var fullPath = Path.Combine(path, $"{_sharedDataService.CurrentGame.StartTime}.json");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            try
            {
                await File.WriteAllTextAsync(fullPath, json);
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
                _sharedDataService.TimerStart(time);
            else
                _messageBoxService.ShowErrorAsync("输入不合法");
        }

        [RelayCommand]
        private void TimerStop()
        {
            _sharedDataService.TimerStop();
        }

        [RelayCommand]
        private async Task StartNavigationAsync()
        {
            var result = await _gameGuidanceService.StartGuidance();
            if(string.IsNullOrEmpty(result)) return;
            ActionName = result;
            IsGuidanceStarted = true;
        }

        [RelayCommand]
        private void StopNavigation()
        {
            _gameGuidanceService.StopGuidance();
            IsGuidanceStarted = false;
            ActionName = string.Empty;
        }

        [RelayCommand]
        private async Task NavigateToNextStepAsync()
        {
            ActionName = await _gameGuidanceService.NextStepAsync();
            await Task.Delay(250);
        }

        [RelayCommand]
        private async Task NavigateToPreviousStepAsync()
        {
            ActionName = await _gameGuidanceService.PrevStepAsync();
            await Task.Delay(250);
        }

        public void Receive(NewGameMessage message)
        {
            if (message.IsNewGameCreated)
            {
                OnPropertyChanged(nameof(SurTeamName));
                OnPropertyChanged(nameof(HunTeamName));
            }
        }

        public void Receive(SwapMessage message)
        {
            OnPropertyChanged(nameof(SurTeamName));
            OnPropertyChanged(nameof(HunTeamName));
        }

        public void Receive(ValueChangedMessage<string> message)
        {
            if (message.Value == nameof(_sharedDataService.RemainingSeconds))
            {
                OnPropertyChanged(nameof(RemainingSeconds));
            }
        }

        public void Receive(PropertyChangedMessage<bool> message)
        {
            if (message.PropertyName == nameof(IGameGuidanceService.IsGuidanceStarted)
                && message.NewValue != IsGuidanceStarted)
            {
                IsGuidanceStarted = message.NewValue;
            }
        }

        private bool _isBo3Mode;

        public bool IsBo3Mode
        {
            get => _isBo3Mode;
            set
            {
                _isBo3Mode = value;
                _sharedDataService.IsBo3Mode = _isBo3Mode;
                if (!IsBo3Mode)
                {
                    GameList = GameListBo5;
                }
                else
                {
                    GameList = GameListBo3;
                }

                OnPropertyChanged(nameof(IsBo3Mode));
            }
        }

        [ObservableProperty] 
        private bool _isSwapHighlighted = false;

        [ObservableProperty]
        private bool _isEndGuidanceHighlighted = false;
        public void Receive(HighlightMessage message)
        {
            IsSwapHighlighted = message.GameAction == GameAction.PickCamp;
            IsEndGuidanceHighlighted = message.GameAction == GameAction.EndGuidance;
        }

        public string TimerTime { get; set; } = "30";

        public List<int> RecommendTimmerList { get; } = [30, 45, 60, 90, 120, 150, 180];

        [ObservableProperty] private Dictionary<GameProgress, string> _gameList;

        public static Dictionary<GameProgress, string> GameListBo5 => new()
        {
            { GameProgress.Free, "自由对局" },
            { GameProgress.Game1FirstHalf, "第1局上半" },
            { GameProgress.Game1SecondHalf, "第1局下半" },
            { GameProgress.Game2FirstHalf, "第2局上半" },
            { GameProgress.Game2SecondHalf, "第2局下半" },
            { GameProgress.Game3FirstHalf, "第3局上半" },
            { GameProgress.Game3SecondHalf, "第3局下半" },
            { GameProgress.Game4FirstHalf, "第4局上半" },
            { GameProgress.Game4SecondHalf, "第4局下半" },
            { GameProgress.Game5FirstHalf, "第5局上半" },
            { GameProgress.Game5SecondHalf, "第5局下半" },
            { GameProgress.Game5ExtraFirstHalf, "第5局加赛上半" },
            { GameProgress.Game5ExtraSecondHalf, "第5局加赛下半" }
        };

        public static Dictionary<GameProgress, string> GameListBo3 => new()
        {
            { GameProgress.Free, "自由对局" },
            { GameProgress.Game1FirstHalf, "第1局上半" },
            { GameProgress.Game1SecondHalf, "第1局下半" },
            { GameProgress.Game2FirstHalf, "第2局上半" },
            { GameProgress.Game2SecondHalf, "第2局下半" },
            { GameProgress.Game3FirstHalf, "第3局上半" },
            { GameProgress.Game3SecondHalf, "第3局下半" },
            { GameProgress.Game3ExtraFirstHalf, "第3局加赛上半" },
            { GameProgress.Game3ExtraSecondHalf, "第3局加赛下半" }
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