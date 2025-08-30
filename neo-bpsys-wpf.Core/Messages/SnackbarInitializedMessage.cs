using CommunityToolkit.Mvvm.Messaging.Messages;

namespace neo_bpsys_wpf.Core.Messages;

public class SnackbarInitializedMessage : ValueChangedMessage<bool>
{
    public SnackbarInitializedMessage(bool value) : base(value)
    {
    }
}