using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.ExamplePlugin.ViewModels;

public partial class MainPageViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _editedText = "ExamplePlugin";

    [RelayCommand]
    private void Confirm()
    {
        // The v3 example no longer mutates an injected frontend control.
    }

    [ObservableProperty]
    private int _counter;

    [RelayCommand]
    private void Plus1()
    {
        Counter++;
    }
}
