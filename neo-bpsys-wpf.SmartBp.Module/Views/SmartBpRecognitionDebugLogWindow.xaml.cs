using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.ViewModels.Pages;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>Displays complete SmartBP recognition diagnostics outside the main page scroll view.</summary>
public partial class SmartBpRecognitionDebugLogWindow : FluentWindow
{
    /// <summary>Initializes a SmartBP recognition debug log window.</summary>
    /// <param name="viewModel">SmartBP page view model that owns the live diagnostic text.</param>
    public SmartBpRecognitionDebugLogWindow(SmartBpModuleContentViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void LogTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is SmartBpModuleContentViewModel { IsRecognitionDebugLogAutoScrollEnabled: true } &&
            sender is TextBox textBox)
            textBox.ScrollToEnd();
    }
}
