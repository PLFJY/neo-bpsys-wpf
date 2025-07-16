using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using System.Collections.ObjectModel;
// 添加日志命名空间
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public class GameDataPageViewModel : ObservableRecipient, IRecipient<NewGameMessage>
    {
        private readonly ILogger<GameDataPageViewModel> _logger;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public GameDataPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            // 设计时构造函数保持不变
        }

        private readonly ISharedDataService _sharedDataService;

        public GameDataPageViewModel(ISharedDataService sharedDataService, ILogger<GameDataPageViewModel> logger)
        {
            _sharedDataService = sharedDataService;
            _logger = logger;

            _logger.LogInformation("Initializing GameDataPageViewModel");

            IsActive = true;
            _logger.LogDebug("Message receiver activated");
        }

        public ObservableCollection<Player> SurPlayerList => _sharedDataService.CurrentGame.SurPlayerList;

        public Player HunPlayer => _sharedDataService.CurrentGame.HunPlayer;

        public void Receive(NewGameMessage message)
        {
            _logger.LogInformation("Received NewGameMessage, updating player data");
            _logger.LogDebug("Refreshing SurPlayerList ({Count} players) and HunPlayer",
                _sharedDataService.CurrentGame.SurPlayerList.Count);

            OnPropertyChanged(nameof(SurPlayerList));
            OnPropertyChanged(nameof(HunPlayer));

            _logger.LogDebug("Player properties refreshed");
        }
    }
}