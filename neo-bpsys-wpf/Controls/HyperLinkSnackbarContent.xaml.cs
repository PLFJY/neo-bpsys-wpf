using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.Controls;

public partial class HyperLinkSnackbarContent : UserControl
{
    public string NavigateUri { get; }
    public string Text { get; }
    public ICommand NoLogerDisplayedCommand { get; }

    public HyperLinkSnackbarContent(string navigateUri, string text, Action noLogerDisplayedAction)
    {
        InitializeComponent();
        NavigateUri = navigateUri;
        Text = text;
        NoLogerDisplayedCommand = new RelayCommand(noLogerDisplayedAction, () => true);
        DataContext = this;
    }
}