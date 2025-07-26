using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Views.Pages;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Abstractions.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class MainWindowViewModel : 
    ViewModelBase,
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

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
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
        set
        {
            _selectedGameProgress = value;
            CurrentGame.GameProgress = _selectedGameProgress;
        }
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
        _logger.LogInformation($"Theme changed to {ApplicationTheme}");
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

        _sharedDataService.CurrentGame = new Game(surTeam, hunTeam, SelectedGameProgress, pickedMap, bannedMap, mapV2Dictionary);

        //发送新对局已创建的消息
        WeakReferenceMessenger.Default.Send(new NewGameMessage(this, true));
            
        OnPropertyChanged(nameof(CurrentGame));
        await _messageBoxService.ShowInfoAsync($"已成功创建新对局\n{CurrentGame.Guid}", "创建提示");
        _logger.LogInformation($"New Game Created{CurrentGame.Guid}");
    }

    [RelayCommand]
    private void Swap()
    {
        //交换阵营
        (CurrentGame.SurTeam.Camp, CurrentGame.HunTeam.Camp) =
            (CurrentGame.HunTeam.Camp, CurrentGame.SurTeam.Camp);
        //交换队伍
        (CurrentGame.SurTeam, _sharedDataService.CurrentGame.HunTeam) =
            (CurrentGame.HunTeam, _sharedDataService.CurrentGame.SurTeam);

        WeakReferenceMessenger.Default.Send(new MemberOnFieldChangedMessage(this));
        WeakReferenceMessenger.Default.Send(new SwapMessage(this, true));
        _logger.LogInformation("Team swapped");
        OnPropertyChanged();
    }

    [RelayCommand]
    private async Task SaveGameInfoAsync()
    {
        var json = JsonSerializer.Serialize(CurrentGame, _jsonSerializerOptions);
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "neo-bpsys-wpf", 
            "GameInfoOutput"
        );
        var fullPath = Path.Combine(path, $"{CurrentGame.StartTime:yyyy-MM-dd-HH-mm-ss}.json");
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        try
        {
            await File.WriteAllTextAsync(fullPath, json);
            await _messageBoxService.ShowInfoAsync($"已成功保存到\n{fullPath}", "保存提示");
            _logger.LogInformation($"Save game {CurrentGame.Guid} info successfully");
        }
        catch (Exception ex)
        {
            await _messageBoxService.ShowInfoAsync($"保存失败\n{ex.Message}", "保存提示");
            _logger.LogError($"Save game {CurrentGame.Guid} info failed\n{ex.Message}");
        }
    }


    [RelayCommand]
    private void TimerStart()
    {
        if (int.TryParse(TimerTime, out var time))
        {
            _logger.LogInformation($"Calling timer started with {time} seconds");
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
        _logger.LogInformation($"Accepted current step: {result}");
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
        _logger.LogInformation($"Accepted current step: {ActionName}");
        await Task.Delay(250);
    }

    [RelayCommand]
    private async Task NavigateToPreviousStepAsync()
    {
        _logger.LogInformation("Calling navigating to the previous step");
        ActionName = await _gameGuidanceService.PrevStepAsync();
        _logger.LogInformation($"Accepted current step: {ActionName}");
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
                _logger.LogInformation($"Accepted IsGuidanceStarted value: {message.NewValue}");
                break;
        }
    }

    private bool _isBo3Mode;

    public bool IsBo3Mode
    {
        get => _isBo3Mode;
        set
        {
            SetProperty(ref _isBo3Mode, value);
            _sharedDataService.IsBo3Mode = _isBo3Mode;
            GameList = !IsBo3Mode ? GameListBo5 : GameListBo3;
            _logger.LogInformation($"Accepted IsBo3Mode value: {value}");
        }
    }

    [ObservableProperty] private bool _isSwapHighlighted;

    [ObservableProperty] private bool _isEndGuidanceHighlighted;

    public void Receive(HighlightMessage message)
    {
        IsSwapHighlighted = message.GameAction == GameAction.PickCamp;
        IsEndGuidanceHighlighted = message.GameAction == GameAction.EndGuidance;
        _logger.LogInformation($"Accepted highlight message: {message.GameAction}");
    }

    public string TimerTime { get; set; } = "30";

    public List<int> RecommendTimerList { get; } = [30, 45, 60, 90, 120, 150, 180];

    [ObservableProperty] private Dictionary<GameProgress, string> _gameList;

    private static Dictionary<GameProgress, string> GameListBo5 => new()
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

    private static Dictionary<GameProgress, string> GameListBo3 => new()
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