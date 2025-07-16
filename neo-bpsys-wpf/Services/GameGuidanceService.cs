using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Exceptions;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Views.Pages;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wpf.Ui;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 前台窗口服务, 实现了 <see cref="IGameGuidanceService"/> 接口，负责对局引导功能
    /// </summary>
    /// <param name="sharedDataService"></param>
    /// <param name="navigationService"></param>
    /// <param name="messageBoxService"></param>
    /// <param name="infoBarService"></param>
    public class GameGuidanceService(
        ISharedDataService sharedDataService,
        INavigationService navigationService,
        IMessageBoxService messageBoxService,
        IInfoBarService infoBarService) : IGameGuidanceService
    {
        private readonly ISharedDataService _sharedDataService = sharedDataService;
        private readonly INavigationService _navigationService = navigationService;
        private readonly IMessageBoxService _messageBoxService = messageBoxService;
        private readonly IInfoBarService _infoBarService = infoBarService;
        private readonly ILogger<GameGuidanceService> _logger;

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

        private Dictionary<GameAction, string> ActionName { get; } = new()
        {
            { GameAction.BanMap, "禁用地图" },
            { GameAction.PickMap, "选择地图" },
            { GameAction.PickCamp, "选择阵营" },
            { GameAction.BanSur, "禁用求生者" },
            { GameAction.BanHun, "禁用监管者" },
            { GameAction.PickSur, "选择求生者" },
            { GameAction.DistributeChara, "分配角色" },
            { GameAction.PickHun, "选择监管者" },
            { GameAction.PickSurTalent, "选择求生者天赋" },
            { GameAction.PickHunTalent, "选择监管者天赋" }
        };

        private int _currentStep = -1;

        private bool _isGuidanceStarted = false;

        public bool IsGuidanceStarted
        {
            get => _isGuidanceStarted;
            set
            {
                WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsGuidanceStarted), _isGuidanceStarted, value));
                _isGuidanceStarted = value;
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
            try
            {
                if (!File.Exists(_guidanceFilePath))
                {
                    _logger.LogError("Game rule file not found at path: {Path}", _guidanceFilePath);
                    throw new FileNotFoundException();
                }

                var gameRuleFileContent = File.ReadAllText(_guidanceFilePath);
                var content = JsonSerializer.Deserialize<Dictionary<GameProgress, GameProperty>>(gameRuleFileContent, _jsonSerializerOptions);
                if (content == null || gameProgress == GameProgress.Free)
                {
                    _logger.LogWarning("Game progress type not supported: {GameProgress}", gameProgress);
                    throw new GuidanceNotSupportedException();
                }

                _logger.LogInformation("Successfully loaded game rules for {GameProgress}", gameProgress);
                return content[gameProgress];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while reading game properties");
                throw;
            }
        }

        public GameGuidanceService(
            ISharedDataService sharedDataService,
            INavigationService navigationService,
            IMessageBoxService messageBoxService,
            IInfoBarService infoBarService,
            ILogger<GameGuidanceService> logger) : this(sharedDataService, navigationService, messageBoxService, infoBarService)
        {
            _logger = logger;
            _logger.LogInformation("GameGuidanceService initialized");
        }

        public async Task<string?> StartGuidance()
        {
            _logger.LogInformation("StartGuidance initiated. Current game progress: {GameProgress}", _sharedDataService.CurrentGame?.GameProgress);
            var returnValue = "当前步骤: ";
            if (IsGuidanceStarted)
            {
                _logger.LogWarning("Attempted to start guidance while already started");
                _infoBarService.ShowWarningInfoBar("对局已开始");
            }

            try
            {
                _currentGameProperty = ReadGamePropertyFromFileAsync(_sharedDataService.CurrentGame.GameProgress);
            }
            catch (GuidanceNotSupportedException)
            {
                _logger.LogWarning("Free game mode does not support guidance");
                _infoBarService.ShowWarningInfoBar("自由对局不支持引导");
                return null;
            }
            catch (FileNotFoundException)
            {
                _logger.LogError("Game rule file not found");
                await _messageBoxService.ShowErrorAsync($"对局规则文件状态异常\n{new FileNotFoundException().Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during game rule loading");
                await _messageBoxService.ShowErrorAsync($"对局规则文件状态异常\n{ex}");
                return null;
            }

            if (_currentGameProperty != null)
            {
                _logger.LogInformation("Game properties loaded: {@Properties}", _currentGameProperty);
                _currentStep = -1;
                _sharedDataService.SetBanCount(BanListName.CanCurrentSurBanned, _currentGameProperty.SurCurrentBan);
                _sharedDataService.SetBanCount(BanListName.CanCurrentHunBanned, _currentGameProperty.HunCurrentBan);
                _sharedDataService.SetBanCount(BanListName.CanGlobalSurBanned, _currentGameProperty.SurGlobalBan);
                _sharedDataService.SetBanCount(BanListName.CanGlobalHunBanned, _currentGameProperty.HunGlobalBan);
                IsGuidanceStarted = true;
                _logger.LogInformation("Guidance started successfully");
                returnValue = await NextStepAsync();
            }
            else
            {
                _logger.LogError("Game properties are null after attempted loading");
                await _messageBoxService.ShowErrorAsync("对局规则文件状态异常");
            }

            _logger.LogDebug("StartGuidance returning: {ReturnValue}", returnValue);
            return returnValue;
        }

        public void StopGuidance()
        {
            _logger.LogInformation("StopGuidance initiated. Current step: {Step}", _currentStep);
            if (!IsGuidanceStarted)
            {
                _logger.LogWarning("StopGuidance called when guidance not started");
                _infoBarService.ShowWarningInfoBar("请先开始对局");
                return;
            }

            _currentStep = 0;
            _infoBarService.CloseInfoBar();
            WeakReferenceMessenger.Default.Send(new HighlightMessage(null, null));
            IsGuidanceStarted = false;
            _logger.LogInformation("Guidance stopped successfully");
        }

        public async Task<string> NextStepAsync()
        {
            _logger.LogInformation("NextStepAsync initiated. Current step: {Step}", _currentStep);
            var returnValue = "当前步骤: ";
            if (!IsGuidanceStarted)
            {
                _logger.LogWarning("NextStepAsync called when guidance not started");
                _infoBarService.ShowWarningInfoBar("请先开始对局");
                returnValue += "无";
                return returnValue;
            }

            if (_currentGameProperty != null)
            {
                if (_currentStep + 1 < _currentGameProperty.WorkFlow.Count)
                {
                    var thisStep = _currentGameProperty.WorkFlow[++_currentStep];
                    _logger.LogDebug("Navigating to step {StepIndex}: {Action}", _currentStep, thisStep.Action);

                    if (thisStep.Action != GameAction.PickCamp)
                        _navigationService.Navigate(_actionToPage[thisStep.Action]);

                    _sharedDataService.TimerStart(thisStep.Time);
                    await Task.Delay(250);
                    WeakReferenceMessenger.Default.Send(new HighlightMessage(thisStep.Action, thisStep.Index));
                    returnValue += ActionName[thisStep.Action];
                    _logger.LogInformation("Advanced to step {Step}: {Action}", _currentStep, thisStep.Action);
                }
                else
                {
                    _logger.LogInformation("Reached final step");
                    _infoBarService.ShowInformationalInfoBar("已经是最后一步");
                    WeakReferenceMessenger.Default.Send(new HighlightMessage(GameAction.EndGuidance, null));
                    returnValue += "无";
                }
            }
            else
            {
                _logger.LogError("CurrentGameProperty is null during navigation");
                await _messageBoxService.ShowErrorAsync("对局信息状态异常");
                returnValue += "无";
            }

            _logger.LogDebug("NextStepAsync returning: {ReturnValue}", returnValue);
            return returnValue;
        }

        public async Task<string> PrevStepAsync()
        {
            _logger.LogInformation("PrevStepAsync initiated. Current step: {Step}", _currentStep);
            var returnValue = "当前步骤: ";
            if (!IsGuidanceStarted)
            {
                _logger.LogWarning("PrevStepAsync called when guidance not started");
                _infoBarService.ShowWarningInfoBar("请先开始对局");
                returnValue += "无";
                return returnValue;
            }

            if (_currentGameProperty != null)
            {
                if (_currentStep > 0)
                {
                    var thisStep = _currentGameProperty.WorkFlow[--_currentStep];
                    _logger.LogDebug("Navigating to previous step {StepIndex}: {Action}", _currentStep, thisStep.Action);

                    if (thisStep.Action != GameAction.PickCamp)
                        _navigationService.Navigate(_actionToPage[thisStep.Action]);

                    _sharedDataService.TimerStart(thisStep.Time);
                    await Task.Delay(250);
                    WeakReferenceMessenger.Default.Send(new HighlightMessage(thisStep.Action, thisStep.Index));
                    returnValue += ActionName[thisStep.Action];
                    _logger.LogInformation("Returned to step {Step}: {Action}", _currentStep, thisStep.Action);
                }
                else
                {
                    _logger.LogInformation("Already at first step");
                    _infoBarService.ShowInformationalInfoBar("已经是第一步");
                    returnValue += "无";
                }
            }
            else
            {
                _logger.LogError("CurrentGameProperty is null during navigation");
                await _messageBoxService.ShowErrorAsync("对局信息状态异常");
                returnValue += "无";
            }

            _logger.LogDebug("PrevStepAsync returning: {ReturnValue}", returnValue);
            return returnValue;
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
}