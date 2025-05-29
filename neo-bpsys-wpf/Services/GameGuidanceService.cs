using System.IO;
using System.Text.Json;
using neo_bpsys_wpf.Enums;
using Wpf.Ui;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.ViewModels;
using System.Security.Cryptography.X509Certificates;
using neo_bpsys_wpf.ViewModels.Pages;

namespace neo_bpsys_wpf.Services
{
//<<<<<<< HEAD
    public partial class GameGuidanceService(ISharedDataService sharedDataService, INavigationService navigationService, IMessageBoxService messageBoxService) : IGameGuidanceService
    {
        private readonly ISharedDataService _sharedDataService = sharedDataService;
        private readonly INavigationService _navigationService = navigationService;
        private readonly IMessageBoxService _messageBoxService = messageBoxService;
        private readonly string guidanceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameRule.json");
        private GameProperty _currentGameProperty = new();
        private object? _charaSelectViewModelBase;
//=======
//    public partial class GameGuidanceService : IGameGuidanceService
//    {
//        private readonly ISharedDataService _sharedDataService;
//        private readonly INavigationService _navigationService;
//        private readonly IMessageBoxService _messageBoxService;
//        private readonly string guidanceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameRule.json");
//        private GameProperty _currentGameProperty = new();
//        //private CharaSelectViewModelBase _charaSelectViewModelBase { get; set; }
//>>>>>>> 5d440765c9e649bac2938734e7e3ffcf86064342
        private readonly Dictionary<GameAction, Type> ActionToPage = new()
        {
            { GameAction.BanMap, typeof (MapBpPage) },
            { GameAction.PickMap, typeof (MapBpPage) },
            { GameAction.BanSur, typeof (BanSurPage) },
            { GameAction.BanHun, typeof (BanHunPage) },
            { GameAction.PickSur, typeof (PickPage) },
            { GameAction.PickHun, typeof (PickPage) },
        };

        public Step CurrentStep { get; set; } = new();

        
        private GameProperty ReadGamePropertyFromFileAsync(GameProgress gameProgress) 
        {
            if (!File.Exists(guidanceFilePath))
            {
                _messageBoxService.ShowErrorAsync("对局规则文件不存在");
                throw new FileNotFoundException();
            }
            var gameRuleFileContent = File.ReadAllText(guidanceFilePath);
            var content = JsonSerializer.Deserialize<Dictionary<GameProgress, GameProperty>>(gameRuleFileContent);
            if (content == null) return new();
            return content[gameProgress];
        }

        public void StartGuidance()
        {
            _currentGameProperty = ReadGamePropertyFromFileAsync(_sharedDataService.CurrentGame.GameProgress);
            CurrentStep = _currentGameProperty.WorkFlow[0];
        }

        public void StopGuidance()
        {
            throw new NotImplementedException();
        }

        public void NextStep()
        {
            
            if (CurrentStep.Sequence < _currentGameProperty.WorkFlow.Count)
            {
                if (CurrentStep.ThisAction == GameAction.PickCamp)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    _navigationService.Navigate(ActionToPage[CurrentStep.ThisAction]);
                    CurrentStep = _currentGameProperty.WorkFlow[CurrentStep.Sequence + 1];
                }
                
            }
            else
            {
                _messageBoxService.ShowInfoAsync("已经是最后一步");

            }
            
        }

        public void PrevStep()
        {
            //throw new NotImplementedException();


            if (CurrentStep.Sequence > 0)
            {
                var lastStep = _currentGameProperty.WorkFlow[CurrentStep.Sequence - 1];
                if (lastStep.ThisAction == GameAction.PickCamp)
                {

                }
                else
                {
                    _navigationService.Navigate(ActionToPage[CurrentStep.ThisAction]);

                }
                CurrentStep = lastStep;
            }
            else
            {
                _messageBoxService.ShowInfoAsync("已经是第一步");
            }
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
            public GameAction ThisAction { get; set; }
            public List<int> Index { get; set; } = [];
        }
    }
}