using System.Security.AccessControl;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.String;

namespace neo_bpsys_wpf.Models;
/// <summary>
/// 选手类, 注意与 <see cref="Player"/> 类做区分，这是表示上场的选手，本类是表示队伍内的成员, <see cref="Models.Member"/> 被它所操纵的 <see cref="Player"/> 包含
/// </summary>
public partial class Member : ObservableObject
{
    public Member(Camp camp)
    {
        Camp = camp;
    }

    private string _name = Empty;
    public string Name
    {
        get => _name;
        set
        {
            SetProperty(ref _name, value);
            OnPropertyChanged();
            WeakReferenceMessenger.Default.Send(new MemberPropertyChangedMessage(this));
        }
    }


    [ObservableProperty]
    private Camp _camp;

    private ImageSource? _image;
    [JsonIgnore]
    public ImageSource? Image
    {
        get
        {
            if (_image != null || ImageUri == null) return _image;
            _image = new BitmapImage(new Uri(ImageUri));
            return _image;
        }
        set
        {
            SetProperty(ref _image, value);
            ImageUri = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsImageValid));
            WeakReferenceMessenger.Default.Send(new MemberPropertyChangedMessage(this));
        }
    }

    public string? ImageUri { get; set; }

    private bool _isOnField = false;
    public bool IsOnField
    {
        get => _isOnField;
        set
        {
            SetProperty(ref _isOnField, value);
            OnPropertyChanged();
            WeakReferenceMessenger.Default.Send(new MemberPropertyChangedMessage(this));
        }
    }

    [ObservableProperty]
    [property: JsonIgnore]
    private bool _canOnFieldChange = true;

    public bool IsImageValid => Image != null;
}
