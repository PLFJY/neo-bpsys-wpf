using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.ViewModels.Pages;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// TeamInfoPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("54B0068C-7DF1-408A-997C-B16F6E099471",
    "TeamInfo",
    SymbolRegular.PeopleTeam24,
    BackendPageCategory.Internal)]
public partial class TeamInfoPage : Page
{
    public TeamInfoPage()
    {
        InitializeComponent();
    }

    private void TeamColorTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter
            || sender is not FrameworkElement
            {
                DataContext: TeamInfoPageViewModel.TeamInfoViewModel viewModel
            })
        {
            return;
        }

        viewModel.ApplyTeamColorCommand.Execute(null);
        e.Handled = true;
    }
}
