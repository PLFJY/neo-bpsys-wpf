using System.IO;
using System.Text.Json;
using neo_bpsys_wpf.Enums;
using Wpf.Ui;
using neo_bpsys_wpf.Views.Pages;
using System.Text.Json.Serialization;
using neo_bpsys_wpf.Exceptions;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Messages;
using System.Threading.Tasks;

namespace neo_bpsys_wpf.Services
{
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
            if (!File.Exists(_guidanceFilePath))
            {
                _messageBoxService.ShowErrorAsync("对局规则文件不存在");
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
            var returnValue = "当前步骤: ";
            if (IsGuidanceStarted)
            {
                _infoBarService.ShowWarningInfoBar("对局已开始");
            }

            try
            {
                _currentGameProperty = ReadGamePropertyFromFileAsync(_sharedDataService.CurrentGame.GameProgress);
            }
            catch (GuidanceNotSupportedException)
            {
                _infoBarService.ShowWarningInfoBar("自由对局不支持引导");
                return null;
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync($"对局规则文件状态异常\n{ex}");
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
                returnValue = await NextStepAsync();
            }
            else
                await _messageBoxService.ShowErrorAsync("对局规则文件状态异常");

            return returnValue;
        }

        public void StopGuidance()
        {
            if (!IsGuidanceStarted)
            {
                _infoBarService.ShowWarningInfoBar("请先开始对局");
                return;
            }

            _currentStep = 0;
            _infoBarService.CloseInfoBar();
            WeakReferenceMessenger.Default.Send(new HighlightMessage(null, null));
            IsGuidanceStarted = false;
        }

        public async Task<string> NextStepAsync()
        {
            var returnValue = "当前步骤: ";
            if (!IsGuidanceStarted)
            {
                _infoBarService.ShowWarningInfoBar("请先开始对局");
                returnValue += "无";
            }

            if (_currentGameProperty != null)
            {
                if (_currentStep + 1 < _currentGameProperty.WorkFlow.Count)
                {
                    var thisStep = _currentGameProperty.WorkFlow[++_currentStep];
                    if (thisStep.Action != GameAction.PickCamp)
                        _navigationService.Navigate(_actionToPage[thisStep.Action]);

                    _sharedDataService.TimerStart(thisStep.Time);
                    await Task.Delay(250);
                    WeakReferenceMessenger.Default.Send(new HighlightMessage(thisStep.Action, thisStep.Index));
                    returnValue += ActionName[thisStep.Action];
                }
                else
                {
                    _infoBarService.ShowInformationalInfoBar("已经是最后一步");
                    WeakReferenceMessenger.Default.Send(new HighlightMessage(GameAction.EndGuidance, null));
                    returnValue += "无";
                }
            }
            else
            {
                await _messageBoxService.ShowErrorAsync("对局信息状态异常");
                returnValue += "无";
            }

            return returnValue;
        }

        public async Task<string> PrevStepAsync()
        {
            var returnValue = "当前步骤: ";
            if (!IsGuidanceStarted)
            {
                _infoBarService.ShowWarningInfoBar("请先开始对局");
                returnValue += "无";
            }

            if (_currentGameProperty != null)
            {
                if (_currentStep > 0)
                {
                    var thisStep = _currentGameProperty.WorkFlow[--_currentStep];
                    if (thisStep.Action != GameAction.PickCamp)
                        _navigationService.Navigate(_actionToPage[thisStep.Action]);

                    _sharedDataService.TimerStart(thisStep.Time);
                    await Task.Delay(250);
                    WeakReferenceMessenger.Default.Send(new HighlightMessage(thisStep.Action, thisStep.Index));
                    returnValue += ActionName[thisStep.Action];
                }
                else
                {
                    _infoBarService.ShowInformationalInfoBar("已经是第一步");
                    returnValue += "无";
                }
            }
            else
            {
                await _messageBoxService.ShowErrorAsync("对局信息状态异常");
                returnValue += "无";
            }

            return returnValue;
        }

        private class GameProperty
        {
            public int SurCurrentBan { get; set; } = 4;
            public int HunCurrentBan { get; set; } = 2;
            public int SurGlobalBan { get; set; } = 9;
            public int HunGlobalBan { get; set; } = 3;
            public List<Step> WorkFlow { get; set; } = [];
        }

        private class Step
        {
            public GameAction Action { get; set; }
            public List<int> Index { get; set; } = [];
            public int? Time { get; set; }
        }
    }
}