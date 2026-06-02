using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.ViewModels.Windows;
using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Classic backend shell. It only rearranges existing backend operations.
/// </summary>
public partial class ClassicBackWindow : FluentWindow
{
    private readonly ILogger<ClassicBackWindow>? _logger;

    public ClassicBackWindow(
        IInfoBarService infoBarService,
        MainWindowViewModel mainWindowViewModel,
        TeamInfoPageViewModel teamInfoPageViewModel,
        MapBpPageViewModel mapBpPageViewModel,
        BanHunPageViewModel banHunPageViewModel,
        BanSurPageViewModel banSurPageViewModel,
        PickPageViewModel pickPageViewModel,
        TalentPageViewModel talentPageViewModel,
        ScorePageViewModel scorePageViewModel,
        GameDataPageViewModel gameDataPageViewModel,
        FrontManagePageViewModel frontManagePageViewModel,
        ILogger<ClassicBackWindow>? logger = null)
    {
        _logger = logger;
        InitializeComponent();

        DataContext = mainWindowViewModel;
        TopBarRoot.DataContext = mainWindowViewModel;
        TeamRoot.DataContext = teamInfoPageViewModel;
        MapRoot.DataContext = mapBpPageViewModel;
        BanHunRoot.DataContext = banHunPageViewModel;
        BanHunNumberRoot.DataContext = banHunPageViewModel;
        BanSurRoot.DataContext = banSurPageViewModel;
        BanSurNumberRoot.DataContext = banSurPageViewModel;
        PickRoot.DataContext = pickPageViewModel;
        GlobalBanRecordRoot.DataContext = pickPageViewModel;
        TalentRoot.DataContext = talentPageViewModel;
        ScoreRoot.DataContext = scorePageViewModel;
        GameDataRoot.DataContext = gameDataPageViewModel;
        FrontWindowRoot.DataContext = frontManagePageViewModel;

        infoBarService.SetInfoBarControl(InfoBar);

        if (Application.Current.MainWindow is null)
        {
            Application.Current.MainWindow = this;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        e.Cancel = true;
        _ = ConfirmToExitAsync();
    }

    private async Task ConfirmToExitAsync()
    {
        var messageBox = new MessageBox()
        {
            Title = I18nHelper.GetLocalizedString("Warning"),
            Content = I18nHelper.GetLocalizedString("AreYouSureYouWantToExit"),
            PrimaryButtonText = I18nHelper.GetLocalizedString("Confirm"),
            PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowExit20 },
            CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
            CloseButtonText = I18nHelper.GetLocalizedString("Cancel"),
            Owner = this,
        };
        var result = await messageBox.ShowDialogAsync();

        if (result == MessageBoxResult.Primary)
        {
            _logger?.LogInformation("Application Closing from classic backend shell");
            Application.Current.Shutdown();
        }
    }
}
