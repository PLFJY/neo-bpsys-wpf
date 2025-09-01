using System.Windows.Controls;

namespace neo_bpsys_wpf.Controls;

public partial class HyperLinkSnackbarContent : UserControl
{
    public string NavigateUri { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public HyperLinkSnackbarContent()
    {
        InitializeComponent();
        DataContext = this;
    }
}