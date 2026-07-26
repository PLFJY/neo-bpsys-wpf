using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Extensions;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 地图BP v2
/// </summary>
[FrontedBindingObject]
public partial class MapV2 : ObservableObjectBase, IRecipient<PropertyChangedMessage<bool>>
{
    /// <summary>
    /// 地图名称
    /// </summary>
    public Map? MapName { get; }

    /// <summary>
    /// 地图是否被选中
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageSource))]
    [NotifyPropertyChangedFor(nameof(CanBePicked))]
    [NotifyPropertyChangedFor(nameof(CanBeBanned))]
    [NotifyPropertyChangedFor(nameof(CanBeGloballyDisabled))]
    public partial bool IsPicked { get; set; }

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
            OnPropertyChanged(nameof(IsBreathing));
            OnPropertyChanged(nameof(CanBePicked));
            OnPropertyChanged(nameof(CanBeBanned));
            OnPropertyChanged(nameof(IsVisuallyBanned));
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsBanned), oldValue,
                value));
        });
    }

    private bool _isGloballyDisabled;

    /// <summary>
    /// 地图是否被全局禁用（无操作方，禁止Ban/Pick）
    /// </summary>
    public bool IsGloballyDisabled
    {
        get => _isGloballyDisabled;
        set => SetPropertyWithAction(ref _isGloballyDisabled, value, oldValue =>
        {
            OnPropertyChanged(nameof(ImageSource));
            OnPropertyChanged(nameof(IsBreathing));
            OnPropertyChanged(nameof(CanBePicked));
            OnPropertyChanged(nameof(CanBeBanned));
            OnPropertyChanged(nameof(IsVisuallyBanned));
            WeakReferenceMessenger.Default.Send(new PropertyChangedMessage<bool>(this, nameof(IsGloballyDisabled),
                oldValue, value));
        });
    }

    /// <summary>
    /// 执行地图操作的队伍
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCampVisible))]
    public partial Team? OperationTeam { get; set; }

    /// <summary>
    /// 地图是否可被选
    /// </summary>
    public bool CanBePicked => !IsBanned && !IsGloballyDisabled;

    /// <summary>
    /// 地图是否可被Ban
    /// </summary>
    public bool CanBeBanned => !IsPicked && !IsGloballyDisabled;

    /// <summary>
    /// 地图是否可被全局禁用
    /// </summary>
    public bool CanBeGloballyDisabled => !IsPicked;

    /// <summary>
    /// 地图是否在视觉上表现为禁用状态（被Ban或被全局禁用）
    /// </summary>
    [JsonIgnore]
    public bool IsVisuallyBanned => IsBanned || IsGloballyDisabled;

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
        //如果是Ban或全局禁用就会灭掉
        get => !IsBanned && !IsGloballyDisabled && _isBreathing;
        set => SetProperty(ref _isBreathing, value);
    }

    /// <summary>
    /// 地图图片（ban）
    /// </summary>
    private ImageSource? _imageSourceBanned;
    /// <summary>
    /// 地图图片（正常）
    /// </summary>
    private ImageSource? _imageSourceNormal;

    /// <summary>
    /// 地图图片（最终输出）
    /// </summary>
    [JsonIgnore]
    public ImageSource? ImageSource
    {
        get
        {
            _imageSourceNormal ??= ImageHelper.GetImageSourceFromName(ImageSourceKey.map, MapName.ToString());
            if(_imageSourceBanned == null)
            {
                _imageSourceBanned ??= _imageSourceNormal?.ToGrayKeepAlpha();

                var banMark = ImageHelper.GetImageSourceFromName(ImageSourceKey.map, "BanMark");
                if (banMark != null)
                    _imageSourceBanned = _imageSourceBanned?.Overlay(banMark);
            }
            return IsVisuallyBanned ? _imageSourceBanned : _imageSourceNormal;
        }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="mapName">地图名称</param>
    [JsonConstructor]
    public MapV2(Map? mapName)
    {
        MapName = mapName;
        IsActive = true;
    }

    /// <summary>
    /// 从Ban/全局禁用中恢复刷新呼吸灯动画
    /// </summary>
    /// <param name="message"></param>
    public void Receive(PropertyChangedMessage<bool> message)
    {
        switch (message.PropertyName)
        {
            case nameof(IsBanned):
            case nameof(IsGloballyDisabled):
                if (message is { OldValue: true, NewValue: false })
                {
                    OnPropertyChanged(nameof(IsBreathing));
                }
                break;
        }
    }
}
