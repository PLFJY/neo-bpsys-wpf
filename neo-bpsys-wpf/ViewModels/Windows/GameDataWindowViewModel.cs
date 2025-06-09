using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Windows.Input;

namespace neo_bpsys_wpf.ViewModels.Windows;

public partial class GameDataWindowViewModel : 
    ObservableRecipient, 
    IRecipient<NewGameMessage>, 
    IRecipient<DesignModeChangedMessage>, 
    IRecipient<PropertyChangedMessage<bool>>
{
#pragma warning disable CS8618 // ���˳����캯��ʱ������Ϊ null ���ֶα�������� null ֵ���뿼����� "required" ���η�������Ϊ��Ϊ null��
    public GameDataWindowViewModel()
#pragma warning restore CS8618 // ���˳����캯��ʱ������Ϊ null ���ֶα�������� null ֵ���뿼����� "required" ���η�������Ϊ��Ϊ null��
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;

    [ObservableProperty]
    private bool _isDesignMode = false;

    public GameDataWindowViewModel(ISharedDataService sharedDataService)
    {
        IsActive = true;
        _sharedDataService = sharedDataService;
    }

    public Game CurrentGame => _sharedDataService.CurrentGame;

    public void Receive(NewGameMessage message)
    {
        OnPropertyChanged(nameof(CurrentGame));
    }

    public void Receive(DesignModeChangedMessage message)
    {
        if (IsDesignMode != message.IsDesignMode)
            IsDesignMode = message.IsDesignMode;
    }

    [ObservableProperty]
    private bool _isBo3Mode = false;
    public void Receive(PropertyChangedMessage<bool> message)
    {
        if (message.PropertyName == nameof(ISharedDataService.IsBo3Mode))
        {
            IsBo3Mode = message.NewValue;
        }
    }
}
