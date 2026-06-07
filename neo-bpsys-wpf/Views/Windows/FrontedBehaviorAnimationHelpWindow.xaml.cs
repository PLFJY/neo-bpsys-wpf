using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedBehaviorAnimationHelpWindow : FluentWindow
{
    public FrontedBehaviorAnimationHelpWindow()
    {
        InitializeComponent();
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
