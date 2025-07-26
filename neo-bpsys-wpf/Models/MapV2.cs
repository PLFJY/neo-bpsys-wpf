using neo_bpsys_wpf.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Abstractions.ViewModels;

namespace neo_bpsys_wpf.Models;

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
        set => SetPropertyWithAction(ref _isBanned, value, (oldValue, newValue) =>
        {
            if (oldValue && !newValue)
            {
                WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsBanned), oldValue,
                    newValue));
            }

            OnPropertyChanged(nameof(ImageSource));
            OnPropertyChanged(nameof(MapBorderBrush));
            OnPropertyChanged(nameof(IsBreathing));
        });
    }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsCampVisible))]
    private Team? _operationTeam;

    private bool _isCampVisible;

    public bool IsCampVisible
    {
        get => OperationTeam != null && _isCampVisible;
        set => SetProperty(ref _isCampVisible, value);
    }

    private bool _isBreathing;

    public bool IsBreathing
    {
        //如果是Ban就会灭掉
        get => !IsBanned && _isBreathing;
        set => SetProperty(ref _isBreathing, value);
    }

    private ImageSource? _imageSourceBanned;
    private ImageSource? _imageSourceNormal;

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