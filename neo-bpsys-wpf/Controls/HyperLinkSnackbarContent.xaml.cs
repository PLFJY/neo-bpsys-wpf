using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.Controls;

public partial class HyperLinkSnackbarContent : UserControl
{
    public string Text { get; }
    public string NoLongerDisplayText { get; }
    public ICommand NoLogerDisplayedCommand { get; }

    public HyperLinkSnackbarContent(string text, string noLongerDisplayText, Action noLogerDisplayedAction)
    {
        InitializeComponent();
        Text = text;
        NoLongerDisplayText = noLongerDisplayText;
        NoLogerDisplayedCommand = new RelayCommand(noLogerDisplayedAction, () => true);
        DataContext = this;
    }
}