using System.Text.Json.Serialization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.Core.Models;

public partial class MapV2(Map? mapName, string mapBorderNormal = "#2B483B", string mapBorderBanned = "#9C3E2F")
    : ViewModelBase, IRecipient<PropertyChangedMessage<bool>>
{
    /// <summary>
    /// 地图名称
    /// </summary>
    public Map? MapName { get; } = mapName;

    /// <summary>
    /// 地图是否被选中
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageSource))]
    [NotifyPropertyChangedFor(nameof(MapBorderBrush))]
    [NotifyPropertyChangedFor(nameof(IsBreathing))]
    [NotifyPropertyChangedFor(nameof(CanBePicked))]
    [NotifyPropertyChangedFor(nameof(CanBeBanned))]
    private bool _isPicked;

    private bool _isBanned;

    /// <summary>
    /// 地图是否被禁用
    /// </summary>
    public bool IsBanned
    {
        get => _isBanned;
        set => SetPropertyWithAction(ref _isBanned, value, oldValue =>
        {
            OnPropertyChanged(nameof(ImageSource));
            OnPropertyChanged(nameof(MapBorderBrush));
            OnPropertyChanged(nameof(IsBreathing));
            OnPropertyChanged(nameof(CanBePicked));
            OnPropertyChanged(nameof(CanBeBanned));
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsBanned), oldValue,
                value));
        });
    }

    /// <summary>
    /// 执行地图操作的队伍
    /// </summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsCampVisible))]
    private Team? _operationTeam;

    /// <summary>
    /// 地图是否可被选
    /// </summary>
    public bool CanBePicked => !IsBanned;

    /// <summary>
    /// 地图是否可被Ban
    /// </summary>
    public bool CanBeBanned => !IsPicked;

    private bool _isCampVisible;

    /// <summary>
    /// 阵营选择是否可见
    /// </summary>
    [JsonIgnore]
    public bool IsCampVisible
    {
        get => OperationTeam != null && _isCampVisible;
        set => SetProperty(ref _isCampVisible, value);
    }

    private bool _isBreathing;

    /// <summary>
    /// 呼吸灯是否开启
    /// </summary>
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

    [JsonIgnore] public Brush MapBorderBrush => IsBanned ? _mapBorderBannedBrush : _mapBorderNormalBrush;
    public void Receive(PropertyChangedMessage<bool> message)
    {
        switch (message.PropertyName)
        {
            case nameof(IsBanned):
                if (message is { OldValue: true, NewValue: false })
                {
                    OnPropertyChanged(nameof(IsBreathing));
                }
                break;
        }
    }
}