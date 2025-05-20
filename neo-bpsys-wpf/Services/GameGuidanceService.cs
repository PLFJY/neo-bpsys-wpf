using System;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Extensions;
using neo_bpsys_wpf.Models;
using Wpf.Ui;
using System.Windows.Controls;
using System.Diagnostics;
using System.CodeDom;

namespace neo_bpsys_wpf.Services
{
    public partial class GameGuidanceService : IGameGuidanceService
    {
        private readonly ISharedDataService _sharedDataService;
        private readonly INavigationService _navigationService;
        private readonly IMessageBoxService _messageBoxService;
        private readonly string guidanceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameRule.json");
        private GameProperty _currentGameProperty = new();

        public GameGuidanceService(ISharedDataService sharedDataService, INavigationService navigationService, IMessageBoxService messageBoxService)
        {
            _sharedDataService = sharedDataService;
            _navigationService = navigationService;
            _messageBoxService = messageBoxService;
        }

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
            throw new NotImplementedException();
        }

        public void PrevStep()
        {
            throw new NotImplementedException();
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
            public GameAction ThisAction { get; set; }
            public List<int> Index { get; set; } = [];
        }
    }
}