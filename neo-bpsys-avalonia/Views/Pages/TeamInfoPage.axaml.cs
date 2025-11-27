using Avalonia.Controls;
using neo_bpsys_avalonia.ViewModels;
using neo_bpsys.Core.Abstractions.Services;

namespace neo_bpsys_avalonia.Views.Pages;

public partial class TeamInfoPage : UserControl
{
    public TeamInfoPage()
    {
        InitializeComponent();
        var shared = App.Services.GetService(typeof(ISharedDataService)) as ISharedDataService;
        var picker = App.Services.GetService(typeof(IFilePickerService)) as IFilePickerService;
        var msg = App.Services.GetService(typeof(IMessageBoxService)) as IMessageBoxService;
        DataContext = new TeamInfoPageViewModel(shared!, picker!, msg!);
    }
}
