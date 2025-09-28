using System.Windows.Controls;

namespace neo_bpsys_wpf.Controls;

public partial class HyperLinkSnackbarMessage : UserControl
{
    public string HyperLinkUri { get; set; }
    public HyperLinkSnackbarMessage()
    {
        InitializeComponent();
        DataContext = this;
    }
}