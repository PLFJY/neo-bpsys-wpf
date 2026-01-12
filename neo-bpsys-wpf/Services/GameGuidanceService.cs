using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Exceptions;
using neo_bpsys_wpf.Views.Pages;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wpf.Ui;
using I18nHelper = neo_bpsys_wpf.Helpers.I18nHelper;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 对局引导服务, 实现了 <see cref="IGameGuidanceService"/> 接口，负责对局引导功能
/// </summary>
/// <param name="sharedDataService"></param>
/// <param name="navigationService"></param>
/// <param name="infoBarService"></param>
public class GameGuidanceService(
    ISharedDataService sharedDataService,
    INavigationService navigationService,
    IInfoBarService infoBarService) : IGameGuidanceService
{
    private readonly ISharedDataService _sharedDataService = sharedDataService;
    private readonly INavigationService _navigationService = navigationService;
    private readonly IInfoBarService _infoBarService = infoBarService;

    private readonly string _guidanceFilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameRule.json");

    private GameProperty? _currentGameProperty = new();

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Dictionary<GameAction, Type> _actionToPage = new()
    {
        { GameAction.BanMap, typeof(MapBpPage) },
        { GameAction.PickMap, typeof(MapBpPage) },
        { GameAction.BanSur, typeof(BanSurPage) },
        { GameAction.BanHun, typeof(BanHunPage) },
        { GameAction.PickSur, typeof(PickPage) },
        { GameAction.DistributeChara, typeof(PickPage) },
        { GameAction.PickHun, typeof(PickPage) },
        { GameAction.PickSurTalent, typeof(TalentPage) },
        { GameAction.PickHunTalent, typeof(TalentPage) }
    };

    private Dictionary<GameAction, Func<string>> ActionName { get; } = new()
    {
        { GameAction.BanMap, () => I18nHelper.GetLocalizedString("BanMap") },
        { GameAction.PickMap, () => I18nHelper.GetLocalizedString("PickMap") },
        { GameAction.PickCamp, () => I18nHelper.GetLocalizedString("PickCamp") },
        { GameAction.BanSur, () => I18nHelper.GetLocalizedString("BanSurvivor") },
        { GameAction.BanHun, () => I18nHelper.GetLocalizedString("BanHunter") },
        { GameAction.PickSur, () => I18nHelper.GetLocalizedString("PickSurvivor") },
        { GameAction.DistributeChara, () => I18nHelper.GetLocalizedString("DistributeCharacters") },
        { GameAction.PickHun, () => I18nHelper.GetLocalizedString("PickHunter") },
        { GameAction.PickSurTalent, () => I18nHelper.GetLocalizedString("PickSurTalent") },
        { GameAction.PickHunTalent, () => I18nHelper.GetLocalizedString("PickHunTalent") }
    };

    private int _currentStep = -1;

    private bool _isGuidanceStarted;

    public bool IsGuidanceStarted
    {
        get => _isGuidanceStarted;
        set
        {
            var oldValue = _isGuidanceStarted;
            _isGuidanceStarted = value;
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsGuidanceStarted),
                oldValue, value));
        }
    }

    /// <summary>
    /// 读取对局规则文件
    /// </summary>
    /// <param name="gameProgress"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="GuidanceNotSupportedException"></exception>
    private GameProperty? ReadGamePropertyFromFileAsync(GameProgress gameProgress)
    {
        if (!File.Exists(_guidanceFilePath))
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("GameRuleFileNotFound"));
            throw new FileNotFoundException();
        }

        var gameRuleFileContent = File.ReadAllText(_guidanceFilePath);
        var content =
            JsonSerializer.Deserialize<Dictionary<GameProgress, GameProperty>>(gameRuleFileContent,
                _jsonSerializerOptions);
        if (content == null || gameProgress == GameProgress.Free)
        {
            throw new GuidanceNotSupportedException();
        }

        return content[gameProgress];
    }

    public async Task<string?> StartGuidance()
    {
        if (IsGuidanceStarted)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("GameRuleFileNotFound"));
        }

        try
        {
            _currentGameProperty = ReadGamePropertyFromFileAsync(_sharedDataService.CurrentGame.GameProgress);
        }
        catch (GuidanceNotSupportedException)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("GuidanceNotAvailableInFree"));
            return null;
        }
        catch (Exception ex)
        {
            await MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("GameRuleFileError")}\n{ex}");
            return null;
        }

        if (_currentGameProperty != null)
        {
            _currentStep = -1;
            _sharedDataService.SetBanCount(BanListName.CanCurrentSurBanned, _currentGameProperty.SurCurrentBan);
            _sharedDataService.SetBanCount(BanListName.CanCurrentHunBanned, _currentGameProperty.HunCurrentBan);
            _sharedDataService.SetBanCount(BanListName.CanGlobalSurBanned, _currentGameProperty.SurGlobalBan);
            _sharedDataService.SetBanCount(BanListName.CanGlobalHunBanned, _currentGameProperty.HunGlobalBan);
            IsGuidanceStarted = true;
            var nextStepResult = await NextStepAsync();
            return nextStepResult;
        }

        await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("GameRuleFileError"));

        return null;
    }

    public void StopGuidance()
    {
        if (!IsGuidanceStarted)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("PleaseStartGameFirst"));
            return;
        }

        _currentStep = 0;
        _infoBarService.CloseInfoBar();
        WeakReferenceMessenger.Default.Send(new HighlightMessage(null, null));
        IsGuidanceStarted = false;
    }

    public async Task<string?> NextStepAsync()
    {
        if (!IsGuidanceStarted)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("PleaseStartGameFirst"));
            return null;
        }

        if (_currentGameProperty != null)
        {
            if (_currentStep + 1 < _currentGameProperty.WorkFlow.Count)
            {
                return await HandleStepChange(_currentStep + 1);
            }

            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("AlreadyLastStep"));
            WeakReferenceMessenger.Default.Send(new HighlightMessage(GameAction.EndGuidance, null));
        }
        else
        {
            await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("GameInfoError"));
        }

        return null;
    }

    public async Task<string?> PrevStepAsync()
    {
        if (!IsGuidanceStarted)
        {
            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("PleaseStartGameFirst"));
            return null;
        }

        if (_currentGameProperty != null)
        {
            if (_currentStep > 0)
            {
                return await HandleStepChange(_currentStep - 1);
            }

            _infoBarService.ShowWarningInfoBar(I18nHelper.GetLocalizedString("AlreadyFirstStep"));
        }
        else
        {
            await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("GameInfoError"));
        }

        return null;
    }

    private async Task<string> HandleStepChange(int newStepIndex)
    {
        if (_currentGameProperty == null) return "N/A";
        var thisStep = _currentGameProperty.WorkFlow[newStepIndex];
        _currentStep = newStepIndex;

        //切换页面
        if (thisStep.Action != GameAction.PickCamp)
            _navigationService.Navigate(_actionToPage[thisStep.Action]);
        //设置计时器
        _sharedDataService.TimerStart(thisStep.Time);
        //等待待选框动画就位
        await Task.Delay(250);
        //广播高亮消息
        WeakReferenceMessenger.Default.Send(new HighlightMessage(thisStep.Action, thisStep.Index));

        return ActionName[thisStep.Action].Invoke();
    }

    public class GameProperty
    {
        public int SurCurrentBan { get; set; } = 4;
        public int HunCurrentBan { get; set; } = 2;
        public int SurGlobalBan { get; set; } = 9;
        public int HunGlobalBan { get; set; } = 3;
        public List<Step> WorkFlow { get; set; } = [];
    }

    public class Step
    {
        public GameAction Action { get; set; }
        public List<int> Index { get; set; } = [];
        public int? Time { get; set; }
    }
}