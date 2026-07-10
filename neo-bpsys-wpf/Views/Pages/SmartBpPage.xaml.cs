using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// SmartBpPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("6E5AB941-A4A0-4D43-B9CB-381364414C1B",
    "SmartBp",
    Wpf.Ui.Controls.SymbolRegular.ScanText24,
    Core.Enums.BackendPageCategory.External)]
public partial class SmartBpPage : Page
{
    private readonly ITutorialRunner? _tutorialRunner;
    private readonly global::neo_bpsys_wpf.Services.NavigationService? _navigationService;
    private CancellationTokenSource _tutorialLifetime = new();
    private Task<TutorialRunResult>? _tutorialRun;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartBpPage"/> class.
    /// </summary>
    /// <param name="tutorialRunner">Tutorial runner.</param>
    /// <param name="navigationService">Navigation service.</param>
    public SmartBpPage(
        ITutorialRunner? tutorialRunner = null,
        global::neo_bpsys_wpf.Services.NavigationService? navigationService = null)
    {
        _tutorialRunner = tutorialRunner;
        _navigationService = navigationService;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (_tutorialLifetime.IsCancellationRequested)
            {
                _tutorialLifetime.Dispose();
                _tutorialLifetime = new CancellationTokenSource();
            }

            TutorialSignalPublisher.Publish(TutorialSignalIds.NavigationSmartBpOpened);
            //if (IsCurrentSmartBpPage())
            //{
            //    await TryRunTutorialAsync();
            //}
        };
        //IsVisibleChanged += async (_, e) =>
        //{
        //    if (Equals(e.NewValue, true) && IsCurrentSmartBpPage())
        //    {
        //        await TryRunTutorialAsync();
        //    }
        //};
        Unloaded += (_, _) =>
        {
            _tutorialLifetime.Cancel();
        };
    }

    //internal async Task TryRunTutorialAsync()
    //{
    //    var runner = _tutorialRunner ?? IAppHost.Host?.Services.GetService<ITutorialRunner>();
    //    if (runner == null)
    //    {
    //        return;
    //    }

    //    if (_tutorialRun is not { IsCompleted: false })
    //    {
    //        _tutorialRun = runner.RunSequenceAsync(this, TutorialPageKey, _tutorialLifetime.Token);
    //    }

    //    await _tutorialRun;
    //}

    private bool IsCurrentSmartBpPage()
    {
        var navigationService = _navigationService
            ?? IAppHost.Host?.Services.GetService<global::neo_bpsys_wpf.Services.NavigationService>();
        return navigationService == null
            || ReferenceEquals(navigationService.CurrentPageContent, this);
    }
}
