using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Messages;
using Player = neo_bpsys_wpf.Core.Models.Player;

namespace neo_bpsys_wpf.ViewModels.Pages;

public class GameDataPageViewModel : ViewModelBase, IRecipient<NewGameMessage>
{

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public GameDataPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;

    public GameDataPageViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;
        IsActive = true;
    }

    public ObservableCollection<Player> SurPlayerList => _sharedDataService.CurrentGame.SurPlayerList;

    public Player HunPlayer => _sharedDataService.CurrentGame.HunPlayer;

    public void Receive(NewGameMessage message)
    {
        OnPropertyChanged(nameof(SurPlayerList));
        OnPropertyChanged(nameof(HunPlayer));
    }
}