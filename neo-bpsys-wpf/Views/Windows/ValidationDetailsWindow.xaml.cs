using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Non-modal validation detail table for Designer v3.
/// </summary>
public partial class ValidationDetailsWindow : FluentWindow
{
    public ValidationDetailsWindow()
    {
        InitializeComponent();
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
