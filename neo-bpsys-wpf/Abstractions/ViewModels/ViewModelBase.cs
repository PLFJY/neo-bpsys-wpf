using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Runtime.CompilerServices;

namespace neo_bpsys_wpf.Abstractions.ViewModels;

public abstract partial class ViewModelBase : ObservableRecipient
{
    public ViewModelBase()
    {
        IsActive = true;
    }
    protected bool SetPropertyWithAction<T>(ref T field, T value, Action<T>? onChanged = null,
        [CallerMemberName] string? propertyName = null)
    {
        var oldValue = field;
        if (!SetProperty(ref field, value))
            return false;

        onChanged?.Invoke(oldValue);
        return true;
    }
}