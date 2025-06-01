using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace neo_bpsys_wpf.Models;
/// <summary>
/// ѡ����, ע���� <see cref="Player"/> �������֣����Ǳ�ʾ�ϳ���ѡ�֣������Ǳ�ʾ�����ڵĳ�Ա, <see cref="Models.Member"/> ���������ݵ� <see cref="Player"/> ����
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
