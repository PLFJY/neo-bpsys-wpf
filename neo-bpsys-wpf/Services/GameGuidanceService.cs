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

namespace neo_bpsys_wpf.Services
{
    public partial class GameGuidanceService : IGameGuidanceService
    {
        public ISharedDataService SharedDataService { get; }
        public INavigationService NavigationService { get; }
        public GameProgress CurrentGameProgress { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public GameGuidanceService(GameProgress gameProgress, Enums.Action step, ISharedDataService sharedDataService, INavigationService navigationService)
        {
            //var nextStep = ReadNextStepFromFile("GameRule.json", gameProgress, step);

        }

        private static Enums.Action ReadNextStepFromFile(string gameRuleFilePath, GameProgress gameProgress, Enums.Action step)
        {
            if (!File.Exists(gameRuleFilePath))
                return Enums.Action.None;
            var gameRuleFileContent = File.ReadAllText(gameRuleFilePath);
            var deserializedFileContent = JsonSerializer.Deserialize<Dictionary<string, GameProperty>>(gameRuleFileContent);
            var thisGameHalf = deserializedFileContent[gameProgress.ToString()];
            var thisWorkFlow = thisGameHalf.StepArray;
            var firstStepIndex = 0;
            for (int i = 0; i < thisWorkFlow.Length; i++)
            {
                var thisStep = thisWorkFlow[i];
                if (thisStep.ThisAction == step)
                {
                    firstStepIndex = i;
                    break;
                }
            }
            if (firstStepIndex < thisWorkFlow.Length)
            {
                return thisWorkFlow[firstStepIndex + 1].ThisAction;
            }
            return Enums.Action.None;
        }
    }
}