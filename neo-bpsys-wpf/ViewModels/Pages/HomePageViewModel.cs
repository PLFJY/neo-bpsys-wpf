using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class HomePageViewModel: ViewModelBase, IRecipient<SnackbarInitializedMessage>
{
    private readonly SnackbarService _snbService;
    
    public HomePageViewModel(SnackbarService snbService)
    {
        _snbService = snbService;
        // WeakReferenceMessenger.Default.Register<SnackbarInitializedMessage>(this);
    }
    
    public async void Receive(SnackbarInitializedMessage message)
    {
        await Task.Delay(3000);
        if (message.Value)
        {
            _snbService.Show(
                "提示", 
                "软件安装目录下有无作者名字版本的前台UI", 
                ControlAppearance.Secondary, 
                new SymbolIcon(SymbolRegular.Info28), 
                TimeSpan.FromSeconds(5));
        }
    }
    
    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<SnackbarInitializedMessage>(this);
    }
}