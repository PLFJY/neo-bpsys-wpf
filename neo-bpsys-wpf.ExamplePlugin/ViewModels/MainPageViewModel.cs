using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.ExamplePlugin.ViewModels;

public partial class MainPageViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string EditedText { get; set; } = "ExamplePlugin";

    [RelayCommand]
    private void Confirm()
    {
        // The v3 example no longer mutates an injected frontend control.
    }

    [ObservableProperty]
    public partial int Counter { get; set; }

    [RelayCommand]
    private void Plus1()
    {
        Counter++;
    }
}
