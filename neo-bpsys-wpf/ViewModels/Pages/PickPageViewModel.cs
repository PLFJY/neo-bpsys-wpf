using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Abstractions.ViewModels;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class PickPageViewModel : ObservableRecipient, IRecipient<HighlightMessage>
    {
        private readonly ILogger<PickPageViewModel> _logger;
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public PickPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly ISharedDataService _sharedDataService;
        private readonly IFrontService _frontService;

        public PickPageViewModel(ISharedDataService sharedDataService, IFrontService frontService, ILogger<PickPageViewModel> logger)
        {
            _logger = logger;
            _sharedDataService = sharedDataService;
            _frontService = frontService;
            SurPickViewModelList =
                [.. Enumerable.Range(0, 4).Select(i => new SurPickViewModel(sharedDataService, frontService, logger, i))];
            HunPickVm = new HunPickViewModel(sharedDataService, frontService);
            MainSurGlobalBanRecordViewModelList =
                [.. Enumerable.Range(0, 9).Select(i => new MainSurGlobalBanRecordViewModel(sharedDataService, logger, i))];
            MainHunGlobalBanRecordViewModelList =
                [.. Enumerable.Range(0, 3).Select(i => new MainHunGlobalBanRecordViewModel(sharedDataService, logger, i))];
            AwaySurGlobalBanRecordViewModelList =
                [.. Enumerable.Range(0, 9).Select(i => new AwaySurGlobalBanRecordViewModel(sharedDataService, logger, i))];
            AwayHunGlobalBanRecordViewModelList =
                [.. Enumerable.Range(0, 3).Select(i => new AwayHunGlobalBanRecordViewModel(sharedDataService, logger, i))];
            IsActive = true;
            _logger.LogInformation("PickPageViewModel initialized with {SurPickCount} Sur picks and {HunPickCount} Hun picks",
                SurPickViewModelList.Count, HunPickVm != null ? 1 : 0);
        }

        [RelayCommand]
        private async Task PickingBorderSwitchAsync(string arg)
        {
            var argsMapSur = new Dictionary<string, int[]>
            {
                { "0", [0] },
                { "1", [1] },
                { "2", [2] },
                { "3", [3] },
                { "0and1", [0, 1] }
            };
            if (arg == "Hun")
            {
                if (HunPickingBorder)
                {
                    await _frontService.BreathingStart(FrontWindowType.BpWindow, "HunPickingBorder", -1, string.Empty);
                    _logger.LogInformation("Breathing started for HunPickingBorder");
                }
                else
                {
                    await _frontService.BreathingStop(FrontWindowType.BpWindow, "HunPickingBorder", -1, string.Empty);
                    _logger.LogInformation("Breathing stopped for HunPickingBorder");
                }
                return;
            }


            for (var i = 0; i < argsMapSur[arg].Length; i++)
            {
                var index = argsMapSur[arg][i];
                if (SurPickingBorderList[index])
                {
                    if (index == argsMapSur[arg].Length - 1)
                    {
                        await _frontService.BreathingStart(FrontWindowType.BpWindow, "SurPickingBorder", index,
                            string.Empty);
                    }
                    else
                    {
                        _ = _frontService.BreathingStart(FrontWindowType.BpWindow, "SurPickingBorder", index,
                            string.Empty);
                    }
                    _logger.LogInformation("Breathing started for SurPickingBorder at index {Index}", index);
                }
                else
                {
                    if (index == argsMapSur[arg].Length - 1)
                    {
                        await _frontService.BreathingStop(FrontWindowType.BpWindow, "SurPickingBorder", index,
                            string.Empty);
                    }
                    else
                    {
                        _ = _frontService.BreathingStop(FrontWindowType.BpWindow, "SurPickingBorder", index,
                            string.Empty);
                    }
                    _logger.LogInformation("Breathing stopped for SurPickingBorder at index {Index}", index);
                }
            }
        }

        public void Receive(HighlightMessage message)
        {
            if (message.GameAction == GameAction.PickSur)
            {
                if (message.Index == null) return;
                foreach (var i in message.Index)
                {
                    SurPickingBorderList[i] = true;
                    _ = PickingBorderSwitchAsync(i.ToString());
                }
                _logger.LogInformation("SurPickingBorderList updated with indices: {Indices}", string.Join(", ", message.Index));
            }
            else
            {
                for (var i = 0; i < SurPickingBorderList.Count; i++)
                {
                    if (!SurPickingBorderList[i]) continue;
                    SurPickingBorderList[i] = false;
                    _ = PickingBorderSwitchAsync(i.ToString());
                }
                _logger.LogInformation("SurPickingBorderList reset to false for all indices");
            }

            if (message.GameAction == GameAction.PickHun)
            {
                HunPickingBorder = true;
                _ = PickingBorderSwitchAsync("Hun");
                _logger.LogInformation("HunPickingBorder set to true");
            }
            else
            {
                if (!HunPickingBorder) return;
                HunPickingBorder = false;
                _ = PickingBorderSwitchAsync("Hun");
                _logger.LogInformation("HunPickingBorder set to false");
            }
        }

        public ObservableCollection<bool> SurPickingBorderList { get; set; } =
            [.. Enumerable.Range(0, 4).Select(i => false)];

        [ObservableProperty] private bool _hunPickingBorder = false;

        public ObservableCollection<SurPickViewModel> SurPickViewModelList { get; set; }
        public HunPickViewModel HunPickVm { get; set; }
        public ObservableCollection<MainSurGlobalBanRecordViewModel> MainSurGlobalBanRecordViewModelList { get; set; }
        public ObservableCollection<MainHunGlobalBanRecordViewModel> MainHunGlobalBanRecordViewModelList { get; set; }
        public ObservableCollection<AwaySurGlobalBanRecordViewModel> AwaySurGlobalBanRecordViewModelList { get; set; }
        public ObservableCollection<AwayHunGlobalBanRecordViewModel> AwayHunGlobalBanRecordViewModelList { get; set; }

        //基于模板基类的VM实现
        public partial class SurPickViewModel :
            CharaSelectViewModelBase,
            IRecipient<CharacterSwappedMessage>,
            IRecipient<PlayerSwappedMessage>,
            IRecipient<MemberPropertyChangedMessage>,
            IRecipient<SwapMessage>
        {
            private readonly IFrontService _frontService;
            public string PlayerName => SharedDataService.CurrentGame.SurPlayerList[Index].Member.Name;

            public SurPickViewModel(ISharedDataService sharedDataService, IFrontService frontService, ILogger logger, int index = 0) :
                base(sharedDataService, logger, index)
            {
                _frontService = frontService;
                CharaList = sharedDataService.SurCharaList;
                Logger.LogInformation("SurPickViewModel initialized for player {Index} ({PlayerName})", index, PlayerName);
            }

            public override async void SyncChara()
            {
                _frontService.FadeOutAnimation(FrontWindowType.BpWindow, "SurPick", Index, string.Empty);
                await Task.Delay(250);
                SharedDataService.CurrentGame.SurPlayerList[Index].Character = SelectedChara;
                _frontService.FadeInAnimation(FrontWindowType.BpWindow, "SurPick", Index, string.Empty);
                PreviewImage = SharedDataService.CurrentGame.SurPlayerList[Index].Character?.HeaderImage;
                Logger.LogInformation("Character synced for player {Index} ({PlayerName}): {CharacterName}",
                    Index, PlayerName, SelectedChara?.Name ?? "None");
            }

            private void RevertSyncChara()
            {
                SelectedChara = SharedDataService.CurrentGame.SurPlayerList[Index].Character;
                PreviewImage = SharedDataService.CurrentGame.SurPlayerList[Index].Character?.HeaderImage;
                Logger.LogInformation("Reverted character sync for player {Index} ({PlayerName}): {CharacterName}",
                    Index, PlayerName, SelectedChara?.Name ?? "None");
            }

            [RelayCommand]
            private void SwapCharacterInPlayers(CharacterChangerCommandParameter parameter)
            {
                (SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Character,
                        SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Character) =
                    (SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Character,
                        SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Character);
                WeakReferenceMessenger.Default.Send(new CharacterSwappedMessage(this));
                Logger.LogInformation("Swapped characters between players {Target} and {Source}",
                    parameter.Target, parameter.Source);
                OnPropertyChanged();
            }

            public void Receive(CharacterSwappedMessage message)
            {
                RevertSyncChara();
                Logger.LogInformation("Received CharacterSwappedMessage, reverting character sync for player {Index} ({PlayerName})",
                    Index, PlayerName);
            }

            protected override void SyncIsEnabled()
            {
                Logger.LogError("SyncIsEnabled should not be called in SurPickViewModel, it is not implemented.");
                throw new NotImplementedException();
            }

            protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.PickSur;

            public void Receive(PlayerSwappedMessage message)
            {
                OnPropertyChanged(nameof(PlayerName));
            }

            public void Receive(MemberPropertyChangedMessage message)
            {
                OnPropertyChanged(nameof(PlayerName));
            }

            public void Receive(SwapMessage message)
            {
                if (message.IsSwapped)
                    OnPropertyChanged(nameof(PlayerName));
            }
        }

        public class HunPickViewModel : CharaSelectViewModelBase
        {
            private readonly IFrontService _frontService;

            public HunPickViewModel(ISharedDataService sharedDataService, IFrontService frontService, ILogger logger) : base(
                sharedDataService, logger)
            {
                _frontService = frontService;
                CharaList = sharedDataService.HunCharaList;
            }

            public override async void SyncChara()
            {
                _frontService.FadeOutAnimation(FrontWindowType.BpWindow, "HunPick", -1, string.Empty);
                await Task.Delay(250);
                SharedDataService.CurrentGame.HunPlayer.Character = SelectedChara;
                _frontService.FadeInAnimation(FrontWindowType.BpWindow, "HunPick", -1, string.Empty);
                PreviewImage = SharedDataService.CurrentGame.HunPlayer.Character?.HeaderImage;
            }

            protected override void SyncIsEnabled()
            {
                throw new NotImplementedException();
            }

            protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.PickHun;
        }

        public class MainSurGlobalBanRecordViewModel : CharaSelectViewModelBase
        {
            private Character? _recordedChara;

            public Character? RecordedChara
            {
                get => _recordedChara;
                set
                {
                    _recordedChara = value;
                    SharedDataService.MainTeam.GlobalBannedSurRecordArray[Index] = _recordedChara;
                }
            }

            public MainSurGlobalBanRecordViewModel(ISharedDataService sharedDataService, ILogger logger, int index = 0) : base(
                sharedDataService, logger, index)
            {
                CharaList = sharedDataService.SurCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();

            protected override void SyncIsEnabled()
            {
                throw new NotImplementedException();
            }

            protected override bool IsActionNameCorrect(GameAction? action) => false;
        }

        public class MainHunGlobalBanRecordViewModel : CharaSelectViewModelBase
        {
            private Character? _recordedChara;

            public Character? RecordedChara
            {
                get => _recordedChara;
                set
                {
                    _recordedChara = value;
                    SharedDataService.MainTeam.GlobalBannedHunRecordArray[Index] = _recordedChara;
                }
            }

            public MainHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, ILogger logger, int index = 0) : base(
                sharedDataService, logger, index)
            {
                CharaList = sharedDataService.HunCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();

            protected override void SyncIsEnabled() => throw new NotImplementedException();

            protected override bool IsActionNameCorrect(GameAction? action) => false;
        }

        public class AwaySurGlobalBanRecordViewModel : CharaSelectViewModelBase
        {
            private Character? _recordedChara;

            public Character? RecordedChara
            {
                get => _recordedChara;
                set
                {
                    _recordedChara = value;
                    SharedDataService.AwayTeam.GlobalBannedSurRecordArray[Index] = _recordedChara;
                }
            }

            public AwaySurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
                sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();

            protected override void SyncIsEnabled() => throw new NotImplementedException();

            protected override bool IsActionNameCorrect(GameAction? action) => false;
        }

        public class AwayHunGlobalBanRecordViewModel : CharaSelectViewModelBase
        {
            private Character? _recordedChara;

            public Character? RecordedChara
            {
                get => _recordedChara;
                set
                {
                    _recordedChara = value;
                    SharedDataService.AwayTeam.GlobalBannedHunRecordArray[Index] = _recordedChara;
                }
            }

            public AwayHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, ILogger logger, int index = 0) : base(
                sharedDataService, logger, index)
            {
                CharaList = sharedDataService.HunCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();

            protected override void SyncIsEnabled() => throw new NotImplementedException();

            protected override bool IsActionNameCorrect(GameAction? action) => false;
        }
    }
}