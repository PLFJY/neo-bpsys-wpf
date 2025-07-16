using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class BanSurPageViewModel : ObservableRecipient, IRecipient<SwapMessage>
    {
        private readonly ILogger<BanSurPageViewModel> _logger;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public BanSurPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly ISharedDataService _sharedDataService;

        public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentSurBanned;

        public BanSurPageViewModel(ISharedDataService sharedDataService, ILogger<BanSurPageViewModel> logger)
        {
            _sharedDataService = sharedDataService;
            BanSurCurrentViewModelList = [.. Enumerable.Range(0, 4).Select(i => new BanSurCurrentViewModel(_sharedDataService, i))];
            BanSurGlobalViewModelList = [.. Enumerable.Range(0, 9).Select(i => new BanSurGlobalViewModel(_sharedDataService, i))];
            IsActive = true;
            _logger = logger;
        }

        public void Receive(SwapMessage message)
        {
            if (!message.IsSwapped)
            {
                _logger.LogInformation("BanSurPageViewModel: SwapMessage received, but IsSwapped is false. No action taken.");
                return;
            }
            for (var i = 0; i < _sharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordArray.Length; i++)
            {
                BanSurGlobalViewModelList[i].SelectedChara = _sharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordArray[i];
                BanSurGlobalViewModelList[i].SyncChara();
            }
            _logger.LogInformation("BanSurGlobalViewModelList synced with GlobalBannedSurRecordArray.");
            OnPropertyChanged();
        }

        public ObservableCollection<BanSurCurrentViewModel> BanSurCurrentViewModelList { get; set; }
        public ObservableCollection<BanSurGlobalViewModel> BanSurGlobalViewModelList { get; set; }

        //基于模板基类的VM实现
        public class BanSurCurrentViewModel : Abstractions.ViewModels.CharaSelectViewModelBase
        {
            private readonly ILogger<BanSurCurrentViewModel> _logger;

            public BanSurCurrentViewModel(ISharedDataService sharedDataService, int index = 0, ILogger<BanSurCurrentViewModel> logger = null) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
                IsEnabled = sharedDataService.CanCurrentSurBanned[index];
                _logger = logger;
            }

            public override void Receive(BanCountChangedMessage message)
            {
                if (message.ChangedList == BanListName.CanCurrentSurBanned)
                {
                    IsEnabled = SharedDataService.CanCurrentSurBanned[Index];
                    _logger?.LogInformation($"BanSurCurrentViewModel: IsEnabled set to {IsEnabled} for index {Index}.");
                }
            }

            public override void SyncChara()
            {
                SharedDataService.CurrentGame.CurrentSurBannedList[Index] = SelectedChara;
                PreviewImage = SharedDataService.CurrentGame.CurrentSurBannedList[Index]?.HeaderImageSingleColor;
                _logger?.LogInformation($"BanSurCurrentViewModel: SyncChara called for index {Index}, SelectedChara: {SelectedChara?.Name}");
            }

            protected override void SyncIsEnabled()
            {
                SharedDataService.CanCurrentSurBanned[Index] = IsEnabled;
                _logger?.LogInformation($"BanSurCurrentViewModel: SyncIsEnabled called for index {Index}, IsEnabled: {IsEnabled}");
            }

            protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.BanSur;
        }

        public class BanSurGlobalViewModel : Abstractions.ViewModels.CharaSelectViewModelBase
        {
            private readonly ILogger<BanSurGlobalViewModel> _logger;
            public BanSurGlobalViewModel(ISharedDataService sharedDataService, int index = 0, ILogger<BanSurGlobalViewModel> logger = null) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
                IsEnabled = sharedDataService.CanGlobalSurBanned[index];
                PreviewImage = sharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[index]?.HeaderImageSingleColor;
                _logger?.LogInformation($"BanSurGlobalViewModel initialized for index {index}, IsEnabled: {IsEnabled}");
                _logger = logger;
            }

            public override void Receive(BanCountChangedMessage message)
            {
                if (message.ChangedList == BanListName.CanGlobalSurBanned)
                {
                    IsEnabled = SharedDataService.CanGlobalSurBanned[Index];
                    _logger?.LogInformation($"BanSurGlobalViewModel: IsEnabled set to {IsEnabled} for index {Index}.");
                }
            }

            public override void SyncChara()
            {
                SharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index] = SelectedChara;
                PreviewImage = SharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index]?.HeaderImageSingleColor;
                _logger?.LogInformation($"BanSurGlobalViewModel: SyncChara called for index {Index}, SelectedChara: {SelectedChara?.Name}");
            }

            protected override void SyncIsEnabled()
            {
                SharedDataService.CanGlobalSurBanned[Index] = IsEnabled;
                _logger?.LogInformation($"BanSurGlobalViewModel: SyncIsEnabled called for index {Index}, IsEnabled: {IsEnabled}");
            }

            protected override bool IsActionNameCorrect(GameAction? action) => false;
        }
    }
}
