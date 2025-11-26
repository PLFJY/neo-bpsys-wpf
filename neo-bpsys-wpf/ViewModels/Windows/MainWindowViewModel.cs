using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Views.Pages;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Game = neo_bpsys_wpf.Core.Models.Game;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class MainWindowViewModel :
    ViewModelBase,
    IRecipient<ValueChangedMessage<string>>,
    IRecipient<PropertyChangedMessage<bool>>,
    IRecipient<HighlightMessage>
{
    public MainWindowViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly IMessageBoxService _messageBoxService;
    private readonly IGameGuidanceService _gameGuidanceService;
    private readonly IInfoBarService _infoBarService;
    private readonly ILogger<MainWindowViewModel> _logger;
    [ObservableProperty] private ApplicationTheme _applicationTheme = ApplicationTheme.Dark;

    private bool _isGuidanceStarted;

    public bool IsGuidanceStarted
    {
        get => _isGuidanceStarted;
        set
        {
            _isGuidanceStarted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGameProgressChange));
        }
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public bool CanGameProgressChange => !IsGuidanceStarted;

    private GameProgress _selectedGameProgress = GameProgress.Free;
    public string RemainingSeconds => _sharedDataService.RemainingSeconds;

    public GameProgress SelectedGameProgress
    {
        get => _selectedGameProgress;
        set => SetPropertyWithAction(ref _selectedGameProgress, value, _ =>
        {
            _selectedGameProgress = value;
            CurrentGame.GameProgress = _selectedGameProgress;
        });
    }

    [ObservableProperty] private string _actionName = string.Empty;

    public MainWindowViewModel(
        ISharedDataService sharedDataService,
        IMessageBoxService messageBoxService,
        IGameGuidanceService gameGuidanceService,
        IInfoBarService infoBarService,
        ILogger<MainWindowViewModel> logger)
    {
        _sharedDataService = sharedDataService;
        _messageBoxService = messageBoxService;
        _gameGuidanceService = gameGuidanceService;
        _infoBarService = infoBarService;
        _logger = logger;
        _isGuidanceStarted = false;
        _jsonSerializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };
        GameList = GameListBo5;
        IsBo3Mode = _sharedDataService.IsBo3Mode;
    }

    [RelayCommand]
    private async Task ThemeSwitchAsync()
    {
        await Task.Delay(60);
        ApplicationThemeManager.Apply(ApplicationTheme);
        _logger.LogInformation("Theme changed to {ApplicationTheme1}", ApplicationTheme);
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

        var pickedMap = _sharedDataService.CurrentGame.PickedMap;
        var bannedMap = _sharedDataService.CurrentGame.BannedMap;
        var mapV2Dictionary = _sharedDataService.CurrentGame.MapV2Dictionary;

        _sharedDataService.CurrentGame =
            new Game(surTeam, hunTeam, SelectedGameProgress, pickedMap, bannedMap, mapV2Dictionary);

        OnPropertyChanged(nameof(CurrentGame));
        await _messageBoxService.ShowInfoAsync($"已成功创建新对局\n{CurrentGame.Guid}", "创建提示");
        _logger.LogInformation("New Game Created{CurrentGameGuid}", CurrentGame.Guid);
    }

    [RelayCommand]
    private void Swap()
    {
        _sharedDataService.CurrentGame.Swap();
        _logger.LogInformation("Team swapped");
        OnPropertyChanged();
    }

    [RelayCommand]
    private async Task SaveGameInfoAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(CurrentGame, _jsonSerializerOptions);
            var path = Path.Combine(AppConstants.AppOutputPath, "GameInfoOutput");
            var fullPath = Path.Combine(path, $"{CurrentGame.StartTime:yyyy-MM-dd-HH-mm-ss}.json");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            await File.WriteAllTextAsync(fullPath, json);
            await _messageBoxService.ShowInfoAsync($"已成功保存到\n{fullPath}", "保存提示");
            _logger.LogInformation("Save game {CurrentGameGuid} info successfully", CurrentGame.Guid);
        }
        catch (Exception ex)
        {
            await _messageBoxService.ShowInfoAsync($"保存失败\n{ex.Message}", "保存提示");
            _logger.LogError("Save game {CurrentGameGuid} info failed\n{ExMessage}", CurrentGame.Guid, ex.Message);
        }
    }


    [RelayCommand]
    private void TimerStart()
    {
        if (int.TryParse(TimerTime, out var time))
        {
            _logger.LogInformation("Calling timer started with {Result} seconds", time);
            _sharedDataService.TimerStart(time);
        }
        else
        {
            _messageBoxService.ShowErrorAsync("输入不合法");
            _logger.LogError("Timer input is not valid");
        }
    }

    [RelayCommand]
    private void TimerStop()
    {
        _logger.LogInformation("Calling Time to stop");
        _sharedDataService.TimerStop();
    }

    [RelayCommand]
    private async Task StartNavigationAsync()
    {
        _logger.LogInformation("Calling GameGuidance to start");
        var result = await _gameGuidanceService.StartGuidance();
        if (string.IsNullOrEmpty(result)) return;
        ActionName = result;
        IsGuidanceStarted = true;
        _logger.LogInformation("Accepted current step: {Result}", result);
    }

    [RelayCommand]
    private void StopNavigation()
    {
        _logger.LogInformation("Calling GameGuidance to stop");
        _gameGuidanceService.StopGuidance();
        IsGuidanceStarted = false;
        ActionName = string.Empty;
        _logger.LogInformation("Accepted Current step: None");
    }

    [RelayCommand]
    private async Task NavigateToNextStepAsync()
    {
        _logger.LogInformation("Calling navigating to the next step");
        ActionName = await _gameGuidanceService.NextStepAsync();
        _logger.LogInformation("Accepted current step: {S}", ActionName);
        await Task.Delay(250);
    }

    [RelayCommand]
    private async Task NavigateToPreviousStepAsync()
    {
        _logger.LogInformation("Calling navigating to the previous step");
        ActionName = await _gameGuidanceService.PrevStepAsync();
        _logger.LogInformation("Accepted current step: {S}", ActionName);
        await Task.Delay(250);
    }

    public void Receive(ValueChangedMessage<string> message)
    {
        switch (message.Value)
        {
            case nameof(_sharedDataService.RemainingSeconds):
                OnPropertyChanged(nameof(RemainingSeconds));
                break;
        }
    }

    public void Receive(PropertyChangedMessage<bool> message)
    {
        switch (message.PropertyName)
        {
            case nameof(IGameGuidanceService.IsGuidanceStarted)
                when message.NewValue != IsGuidanceStarted:
                IsGuidanceStarted = message.NewValue;
                _logger.LogInformation("Accepted IsGuidanceStarted value: {MessageNewValue}", message.NewValue);
                break;
        }
    }

    private bool _isBo3Mode;

    public bool IsBo3Mode
    {
        get => _isBo3Mode;
        set => SetPropertyWithAction(ref _isBo3Mode, value, _ =>
        {
            _sharedDataService.IsBo3Mode = _isBo3Mode;
            if (SelectedGameProgress > GameProgress.Game3SecondHalf)
            {
                SelectedGameProgress = GameProgress.Free;
            }
            GameList = !IsBo3Mode ? GameListBo5 : GameListBo3;
            _logger.LogInformation("Accepted IsBo3Mode value: {Value}", value);
        });
    }

    [ObservableProperty] private bool _isSwapHighlighted;

    [ObservableProperty] private bool _isEndGuidanceHighlighted;

    public void Receive(HighlightMessage message)
    {
        IsSwapHighlighted = message.GameAction == GameAction.PickCamp;
        IsEndGuidanceHighlighted = message.GameAction == GameAction.EndGuidance;
        _logger.LogInformation("Accepted highlight message: {MessageGameAction}", message.GameAction);
    }

    public string TimerTime { get; set; } = "30";

    public List<int> RecommendTimerList { get; } = [30, 45, 60, 90, 120, 150, 180];

    [ObservableProperty] private Dictionary<GameProgress, string> _gameList;

    private static Dictionary<GameProgress, string> GameListBo5 => new()
    {
        { GameProgress.Free, "Free" },
        { GameProgress.Game1FirstHalf, "Game1FirstHalf" },
        { GameProgress.Game1SecondHalf, "Game1SecondHalf" },
        { GameProgress.Game2FirstHalf, "Game2FirstHalf" },
        { GameProgress.Game2SecondHalf, "Game2SecondHalf" },
        { GameProgress.Game3FirstHalf, "Game3FirstHalf" },
        { GameProgress.Game3SecondHalf, "Game3SecondHalf" },
        { GameProgress.Game4FirstHalf, "Game4FirstHalf" },
        { GameProgress.Game4SecondHalf, "Game4SecondHalf" },
        { GameProgress.Game5FirstHalf, "Game5FirstHalf" },
        { GameProgress.Game5SecondHalf, "Game5SecondHalf" },
        { GameProgress.Game5ExtraFirstHalf, "Game5ExtraFirstHalf" },
        { GameProgress.Game5ExtraSecondHalf, "Game5ExtraSecondHalf" }
    };

    private static Dictionary<GameProgress, string> GameListBo3 => new()
    {
        { GameProgress.Free, "Free" },
        { GameProgress.Game1FirstHalf, "Game1FirstHalf" },
        { GameProgress.Game1SecondHalf, "Game1SecondHalf" },
        { GameProgress.Game2FirstHalf, "Game2FirstHalf" },
        { GameProgress.Game2SecondHalf, "Game2SecondHalf" },
        { GameProgress.Game3FirstHalf, "Game3FirstHalf" },
        { GameProgress.Game3SecondHalf, "Game3SecondHalf" },
        { GameProgress.Game3ExtraFirstHalf, "Game3ExtraFirstHalf" },
        { GameProgress.Game3ExtraSecondHalf, "Game3ExtraSecondHalf" }
    };

    public List<NavigationViewItem> MenuItems { get; } =
    [
        new("HomePage", SymbolRegular.Home24, typeof(HomePage)),
        new("TeamInfo", SymbolRegular.PeopleTeam24, typeof(TeamInfoPage)),
        new("MapBP", SymbolRegular.Map24, typeof(MapBpPage)),
        new("BanHunter", SymbolRegular.PresenterOff24, typeof(BanHunPage)),
        new("BanSurvivor", SymbolRegular.PersonProhibited24, typeof(BanSurPage)),
        new("PickCharacter", SymbolRegular.PersonAdd24, typeof(PickPage)),
        new("TalentAndTrait", SymbolRegular.PersonWalking24, typeof(TalentPage)),
        new("ScoreControl", SymbolRegular.NumberRow24, typeof(ScorePage)),
        new("GameData", SymbolRegular.TextNumberListLtr24, typeof(GameDataPage)),
    ];

    public List<NavigationViewItem> FooterMenuItems { get; } =
    [
        new("FrontendManagement", SymbolRegular.ShareScreenStart24, typeof(FrontManagePage)),
        new("Extensions", SymbolRegular.AppsAddIn24, typeof(ExtensionPage)),
        new("Settings", SymbolRegular.Settings24, typeof(SettingPage)),
    ];
}