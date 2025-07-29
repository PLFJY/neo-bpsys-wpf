using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Core.Models;

public partial class MapV2(Map mapName, string mapBorderNormal = "#2B483B", string mapBorderBanned = "#9C3E2F")
    : ViewModelBase, IRecipient<PropertyChangedMessage<bool>>
{
    public Map MapName { get; } = mapName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageSource))]
    [NotifyPropertyChangedFor(nameof(MapBorderBrush))]
    [NotifyPropertyChangedFor(nameof(IsBreathing))]
    private bool _isPicked;

    private bool _isBanned;

    public bool IsBanned
    {
        get => _isBanned;
        set => SetPropertyWithAction(ref _isBanned, value, (oldValue) =>
        {
            if (oldValue && !value)
            {
                WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsBanned), oldValue,
                    value));
            }

            OnPropertyChanged(nameof(ImageSource));
            OnPropertyChanged(nameof(MapBorderBrush));
            OnPropertyChanged(nameof(IsBreathing));
        });
    }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsCampVisible))]
    private Team? _operationTeam;

    private bool _isCampVisible;
    [JsonIgnore]
    public bool IsCampVisible
    {
        get => OperationTeam != null && _isCampVisible;
        set => SetProperty(ref _isCampVisible, value);
    }

    private bool _isBreathing;
    [JsonIgnore]
    public bool IsBreathing
    {
        //如果是Ban就会灭掉
        get => !IsBanned && _isBreathing;
        set => SetProperty(ref _isBreathing, value);
    }

    private ImageSource? _imageSourceBanned;
    private ImageSource? _imageSourceNormal;
    [JsonIgnore]
    public ImageSource? ImageSource
    {
        get
        {
            _imageSourceBanned ??=
                ImageHelper.GetImageSourceFromName(ImageSourceKey.map_singleColor, MapName.ToString());
            _imageSourceNormal ??= ImageHelper.GetImageSourceFromName(ImageSourceKey.map, MapName.ToString());
            return IsBanned ? _imageSourceBanned : _imageSourceNormal;
        }
    }

    private readonly Brush _mapBorderNormalBrush = ColorHelper.HexToBrush(mapBorderNormal);
    private readonly Brush _mapBorderBannedBrush = ColorHelper.HexToBrush(mapBorderBanned);
    [JsonIgnore]
    public Brush MapBorderBrush => IsBanned ? _mapBorderBannedBrush : _mapBorderNormalBrush;


    public void Receive(PropertyChangedMessage<bool> message)
    {
        switch (message.PropertyName)
        {
            case nameof(IsBreathing) when IsBreathing != message.NewValue:
                IsBreathing = message.NewValue;
                break;
            case nameof(IsCampVisible) when IsCampVisible != message.NewValue:
                IsCampVisible = message.NewValue;
                break;
            case nameof(IsBanned) when _isBreathing:
                if (message.Sender != this && !message.NewValue && !_isBanned)
                    OnPropertyChanged(nameof(IsBreathing));
                break;
        }
    }
}