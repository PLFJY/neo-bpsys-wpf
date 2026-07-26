using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
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
    private readonly ITutorialRunner? _tutorialRunner;
    private readonly global::neo_bpsys_wpf.Services.NavigationService? _navigationService;

    /// <summary>
    /// 初始化 <see cref="TeamInfoPage"/> 类的新实例。
    /// </summary>
    /// <param name="tutorialRunner">教程运行器。</param>
    /// <param name="navigationService">导航服务。</param>
    public TeamInfoPage(
        ITutorialRunner? tutorialRunner = null,
        global::neo_bpsys_wpf.Services.NavigationService? navigationService = null)
    {
        _tutorialRunner = tutorialRunner;
        _navigationService = navigationService;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationTeamInfoOpened);
            if (IsCurrentTeamInfoPage())
            {
                await TryRunTutorialAsync();
            }
        };
    }

    internal async Task TryRunTutorialAsync()
    {
        var runner = _tutorialRunner ?? IAppHost.Host?.Services.GetService<ITutorialRunner>();
        if (runner == null)
        {
            return;
        }

        await runner.RunSequenceAsync(this, TutorialPageKey, TutorialOwnerLifetime.GetToken(this));
    }

    private bool IsCurrentTeamInfoPage()
    {
        var navigationService = _navigationService
            ?? IAppHost.Host?.Services.GetService<global::neo_bpsys_wpf.Services.NavigationService>();
        return navigationService == null
            || ReferenceEquals(navigationService.CurrentPageContent, this);
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
