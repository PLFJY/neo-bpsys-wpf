using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;
/// <summary>
/// 选手类, 注意与 <see cref="Player"/> 类做区分，这是表示上场的选手，本类是表示队伍内的成员, <see cref="Models.Member"/> 被它所操纵的 <see cref="Player"/> 包含
/// </summary>
public partial class Member : ObservableObject
{
    public Member()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            WeakReferenceMessenger.Default.Send(new MemberStateChangedMessage(this));
            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }


    [ObservableProperty]
    private Camp _camp;

    [ObservableProperty]
    private ImageSource? _image;

    [ObservableProperty]
    private bool _isOnField = false;

    [ObservableProperty]
    [JsonIgnore]
    private bool _canOnFieldChange = true;

    public Member(Camp camp)
    {
        Camp = camp;
    }
}
