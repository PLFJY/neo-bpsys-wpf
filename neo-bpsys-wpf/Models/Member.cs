using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Services;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Web;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using static neo_bpsys_wpf.Models.Member;

namespace neo_bpsys_wpf.Models;

public partial class Member : ObservableRecipient, IRecipient<PropertyChangedMessage<CanOnFieldChangedMessageType>>
{
    public Member()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Camp _camp;

    [ObservableProperty]
    private ImageSource? _image;

    [ObservableProperty]
    private bool _isOnField = false;

    [ObservableProperty]
    private bool _canOnFieldChange = true;

    public Member(Camp camp)
    {
        Camp = camp;
        IsActive = true;
    }


    public void Receive(PropertyChangedMessage<CanOnFieldChangedMessageType> message)
    {
        if (message.NewValue.Camp != this.Camp) return;

        if(message.PropertyName == nameof(CanOnFieldChange))
        {
            if (!IsOnField)
                CanOnFieldChange = message.NewValue.CanOthersOnField;
        }
    }


    public class CanOnFieldChangedMessageType
    {
        public Camp Camp { get; set; }
        public bool CanOthersOnField { get; set; }

        public CanOnFieldChangedMessageType(Camp camp, bool canOthersOnField = true)
        {
            Camp = camp;
            CanOthersOnField = canOthersOnField;
        }
    }
}
