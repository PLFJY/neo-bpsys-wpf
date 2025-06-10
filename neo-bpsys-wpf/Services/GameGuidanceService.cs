using System.IO;
using System.Text.Json;
using neo_bpsys_wpf.Enums;
using Wpf.Ui;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.ViewModels;
using System.Security.Cryptography.X509Certificates;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Converters;
using System.Text.Json.Serialization;
using neo_bpsys_wpf.Exceptions;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace neo_bpsys_wpf.Services
{
    public partial class GameGuidanceService(
        ISharedDataService sharedDataService,
        INavigationService navigationService,
        IMessageBoxService messageBoxService,
        IInfoBarService infoBarService) : IGameGuidanceService
    {
        private readonly ISharedDataService _sharedDataService = sharedDataService;
        private readonly INavigationService _navigationService = navigationService;
        private readonly IMessageBoxService _messageBoxService = messageBoxService;
        private readonly IInfoBarService _infoBarService = infoBarService;
        private readonly string guidanceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameRule.json");
        private GameProperty? _currentGameProperty = new();
        private readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };
        private readonly Dictionary<GameAction, Type> ActionToPage = new()
        {
            { GameAction.BanMap, typeof (MapBpPage) },
            { GameAction.PickMap, typeof (MapBpPage) },
            { GameAction.BanSur, typeof (BanSurPage) },
            { GameAction.BanHun, typeof (BanHunPage) },
            { GameAction.PickSur, typeof (PickPage) },
            { GameAction.PickHun, typeof (PickPage) },
            { GameAction.PickTalent, typeof(TalentPage) }
        };
        private Dictionary<GameAction, string> ActionName { get; } = new()
        {
            { GameAction.BanMap, "禁用地图" },
            { GameAction.PickMap, "选择地图" },
            { GameAction.PickCamp,"选择阵营" },
            { GameAction.BanSur, "禁用求生者" },
            { GameAction.BanHun, "禁用监管者" },
            { GameAction.PickSur, "选择求生者" },
            { GameAction.PickHun, "选择监管者" },
            { GameAction.PickTalent, "选择天赋" }
        };

        public int _currentStep = 0;

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
            if (!File.Exists(guidanceFilePath))
            {
                _messageBoxService.ShowErrorAsync("对局规则文件不存在");
                throw new FileNotFoundException();
            }
            var gameRuleFileContent = File.ReadAllText(guidanceFilePath);
            var content = JsonSerializer.Deserialize<Dictionary<GameProgress, GameProperty>>(gameRuleFileContent, jsonSerializerOptions);
            if (content == null || gameProgress == GameProgress.Free)
            {
                throw new GuidanceNotSupportedException();
            }
            return content[gameProgress];
        }

        public string StartGuidance()
        {
            var returnValue = "当前步骤: ";
            if (IsGuidanceStarted)
            {
                _infoBarService.ShowWarningInfoBar("对局已开始");
            }
            _currentGameProperty = ReadGamePropertyFromFileAsync(_sharedDataService.CurrentGame.GameProgress);
            if (_currentGameProperty != null)
            {
                _currentStep = 0;
                IsGuidanceStarted = true;
                returnValue += "无";
            }
            else
            {
                _messageBoxService.ShowErrorAsync("对局文件状态异常");
            }
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
            IsGuidanceStarted = false;
        }

        public string NextStep()
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
                    if (thisStep.Action == GameAction.PickCamp)
                    {
                        //NotImplemented
                    }
                    else
                    {
                        _navigationService.Navigate(ActionToPage[thisStep.Action]);
                    }
                    returnValue += ActionName[thisStep.Action];
                }
                else
                {
                    _infoBarService.ShowInformationalInfoBar("已经是最后一步");
                    returnValue += "无";
                }
            }
            else
            {
                _messageBoxService.ShowErrorAsync("对局信息状态异常");
                returnValue += "无";
            }
            return returnValue;
        }

        public string PrevStep()
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
                    if (thisStep.Action == GameAction.PickCamp)
                    {
                        //NotImplemented
                    }
                    else
                    {
                        _navigationService.Navigate(ActionToPage[thisStep.Action]);
                    }
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
                _messageBoxService.ShowErrorAsync("对局信息状态异常");
                returnValue += "无";
            }
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
            public int Sequence { get; set; }
            public GameAction Action { get; set; }
            public List<int> Index { get; set; } = [];
        }
    }
}