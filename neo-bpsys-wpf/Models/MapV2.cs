using neo_bpsys_wpf.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using  neo_bpsys_wpf.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Abstractions.Services;

namespace neo_bpsys_wpf.Models;

public partial class MapV2 : ObservableRecipient, IRecipient<PropertyChangedMessage<bool>>
{
    public Map MapName { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageSource))]
    [NotifyPropertyChangedFor(nameof(MapBorderBrush))]
    [NotifyPropertyChangedFor(nameof(IsBreathing))]
    private bool _isPicked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageSource))]
    [NotifyPropertyChangedFor(nameof(MapBorderBrush))]
    [NotifyPropertyChangedFor(nameof(IsBreathing))]
    private bool _isBanned;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCampVisible))]
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
        get => !IsBanned && _isBreathing;
        set => SetProperty(ref _isBreathing, value);
    }

    private ImageSource? _imageSourceBanned;
    private ImageSource? _imageSourceNormal;

    public ImageSource? ImageSource
    {
        get
        {
            _imageSourceBanned ??= ImageHelper.GetImageSourceFromName(ImageSourceKey.map_singleColor, MapName.ToString());
            _imageSourceNormal ??= ImageHelper.GetImageSourceFromName(ImageSourceKey.map, MapName.ToString());
            return IsBanned ? _imageSourceBanned : _imageSourceNormal;
        }
    }

    private readonly Brush _mapBorderNormalBrush;
    private readonly Brush _mapBorderBannedBrush;

    /// <inheritdoc/>
    public MapV2(Map mapName, string mapBorderNormal = "#2B483B", string mapBorderBanned = "#9C3E2F")
    {
        MapName = mapName;
        _mapBorderNormalBrush = ColorHelper.HexToBrush(mapBorderNormal);
        _mapBorderBannedBrush = ColorHelper.HexToBrush(mapBorderBanned);
        IsActive = true;
    }

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
        }
    }
}