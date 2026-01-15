using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.ExamplePlugin.Views;

namespace neo_bpsys_wpf.ExamplePlugin.ViewModels;

public partial class MainPageViewModel : ViewModelBase
{
    private readonly IFrontedWindowService _frontedWindowService;
    private ExampleInjectedControl _injectedControl;

    public MainPageViewModel()
    {

    }

    public MainPageViewModel(IFrontedWindowService frontedWindowService)
    {
        _frontedWindowService = frontedWindowService;
        //利用 GUID 获取注入的控件实例
        _injectedControl =
            frontedWindowService.GetInjectedControl("D9AFD731-DB3C-408B-8368-D70E688CE7CB") as ExampleInjectedControl;

        EditedText = _injectedControl.TextBlock1.Text;
    }

    [ObservableProperty] private string _editedText = string.Empty;

    [RelayCommand]
    private void Confirm()
    {
        _injectedControl.TextBlock1.Text = EditedText;
    }

    [ObservableProperty] private int _counter;

    [RelayCommand]
    private void Plus1()
    {
        Counter++;
    }
}