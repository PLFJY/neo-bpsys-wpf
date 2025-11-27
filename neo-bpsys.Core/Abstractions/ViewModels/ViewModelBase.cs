using CommunityToolkit.Mvvm.ComponentModel;

namespace neo_bpsys.Core.Abstractions.ViewModels;

public abstract partial class ViewModelBase : ObservableRecipient
{
    public ViewModelBase()
    {
        IsActive = true;
    }
}
