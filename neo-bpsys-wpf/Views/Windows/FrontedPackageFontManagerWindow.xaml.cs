using neo_bpsys_wpf.ViewModels.Windows;
using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Interaction logic for FrontedPackageFontManagerWindow.xaml.
/// </summary>
public partial class FrontedPackageFontManagerWindow : FluentWindow
{
    /// <summary>
    /// Initializes a new package font manager window.
    /// </summary>
    public FrontedPackageFontManagerWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new package font manager window with its view model.
    /// </summary>
    /// <param name="viewModel">Window view model.</param>
    public FrontedPackageFontManagerWindow(FrontedPackageFontManagerWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private FrontedPackageFontManagerWindowViewModel? ViewModel =>
        DataContext as FrontedPackageFontManagerWindowViewModel;

    private async void FrontedPackageFontManagerWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.LoadAsync();
        }
    }

    private async void DeleteSelectedFont_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.DeleteSelectedFontAsync();
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
