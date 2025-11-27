using Avalonia.Controls;
using neo_bpsys.Core.Abstractions.Services;
using neo_bpsys_avalonia.ViewModels;

namespace neo_bpsys_avalonia.Views.Windows;

public partial class ScoreHunWindow : Window
{
    public ScoreHunWindow()
    {
        InitializeComponent();
        var shared = App.Services.GetService(typeof(ISharedDataService)) as ISharedDataService;
        var settings = App.Services.GetService(typeof(ISettingsHostService)) as ISettingsHostService;
        DataContext = new ScoreWindowViewModel(shared!, settings!);
    }
}
