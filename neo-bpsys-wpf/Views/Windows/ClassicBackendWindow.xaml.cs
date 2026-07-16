using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Pages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// 经典后台外壳。仅重新编排现有后台操作。
/// </summary>
public partial class ClassicBackendWindow : FluentWindow
{
    private readonly ILogger<ClassicBackendWindow>? _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, ClassicPageHostWindow> _pageHostWindows = [];

    public ClassicBackendWindow(
        IServiceProvider serviceProvider,
        IInfoBarService infoBarService,
        IContentDialogService contentDialogService,
        MainWindowViewModel mainWindowViewModel,
        TeamInfoPageViewModel teamInfoPageViewModel,
        MapBpPageViewModel mapBpPageViewModel,
        BanHunPageViewModel banHunPageViewModel,
        BanSurPageViewModel banSurPageViewModel,
        PickPageViewModel pickPageViewModel,
        TalentPageViewModel talentPageViewModel,
        ScorePageViewModel scorePageViewModel,
        GameDataPageViewModel gameDataPageViewModel,
        ILogger<ClassicBackendWindow>? logger = null)
    {
        _serviceProvider = serviceProvider;
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
        ScorePreviewDrawerRoot.DataContext = scorePageViewModel;
        GameDataRoot.DataContext = gameDataPageViewModel;

        infoBarService.SetInfoBarControl(InfoBar);
        contentDialogService.SetContentDialogHost(ContentDialogHost);

        if (Application.Current.MainWindow is null)
        {
            Application.Current.MainWindow = this;
        }
    }

    private void OpenFrontendManagement_Click(object sender, RoutedEventArgs e)
    {
        OpenPageHost<FrontManagePage>(AppI18nDictionaries.Shell, "FrontendManagement");
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenPageHost<SettingPage>(AppI18nDictionaries.Common, "Settings");
    }

    private void OpenPlugins_Click(object sender, RoutedEventArgs e)
    {
        OpenPageHost<PluginPage>(AppI18nDictionaries.PluginMarket, "Plugins");
    }

    private void OpenSmartBp_Click(object sender, RoutedEventArgs e)
    {
        OpenPageHost<SmartBpPage>(AppI18nDictionaries.Shell, "SmartBp");
    }

    private void OpenTeamInfo_Click(object sender, RoutedEventArgs e)
    {
        OpenPageHost<TeamInfoPage>(AppI18nDictionaries.Team, "TeamInfo");
    }

    private void OpenPageHost<TPage>(string dictionary, string titleKey)
        where TPage : Page
    {
        var pageType = typeof(TPage);
        if (_pageHostWindows.TryGetValue(pageType, out var existingWindow))
        {
            existingWindow.Activate();
            existingWindow.Focus();
            return;
        }

        var page = _serviceProvider.GetRequiredService<TPage>();
        DetachPageFromCurrentParent(page);

        var window = new ClassicPageHostWindow(dictionary, titleKey, page)
        {
            Owner = this
        };
        window.Closed += (_, _) => _pageHostWindows.Remove(pageType);
        _pageHostWindows[pageType] = window;
        window.Show();
        window.Activate();
    }

    private static void DetachPageFromCurrentParent(Page page)
    {
        switch (page.Parent)
        {
            case ContentControl contentControl:
                contentControl.Content = null;
                break;
        }
    }

    private void ToggleScorePreviewDrawer_Click(object sender, RoutedEventArgs e)
    {
        ScorePreviewDrawerRoot.Visibility = ScorePreviewDrawerRoot.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void CloseScorePreviewDrawer_Click(object sender, RoutedEventArgs e)
    {
        ScorePreviewDrawerRoot.Visibility = Visibility.Collapsed;
    }

    private void RestoreClassicScorePreviewDefaultSort_Click(object sender, RoutedEventArgs e)
    {
        var view = CollectionViewSource.GetDefaultView(ClassicScorePreviewDataGrid.ItemsSource);
        view?.SortDescriptions.Clear();

        foreach (var column in ClassicScorePreviewDataGrid.Columns)
        {
            column.SortDirection = null;
        }

        view?.Refresh();
    }

    private void ClassicRoot_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ScorePreviewDrawerRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        if (IsDescendantOf(e.OriginalSource as DependencyObject, ScorePreviewDrawerRoot))
        {
            return;
        }

        ScorePreviewDrawerRoot.Visibility = Visibility.Collapsed;
    }

    private static bool IsDescendantOf(DependencyObject? current, DependencyObject ancestor)
    {
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
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
            Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Warning"),
            Content = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "AreYouSureYouWantToExit"),
            PrimaryButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
            PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowExit20 },
            CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
            CloseButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"),
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
