using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Core.Models;

public partial class BanMapInfo(Map map) : ViewModelBase, IRecipient<PropertyChangedMessage<Map?>>
{
    public Map Map { get; } = map;
    [ObservableProperty] private bool _isBanned;

    public ImageSource? ImageSource { get; } =
        ImageHelper.GetImageSourceFromName(ImageSourceKey.map_singleColor, map.ToString());

    [ObservableProperty] private bool _canBanInvoke = true;

    public void Receive(PropertyChangedMessage<Map?> message)
    {
        switch (message.PropertyName)
        {
            case "PickedMap":
                CanBanInvoke = Map != message.NewValue;
                break;
        }
    }
}