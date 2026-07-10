using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Pages;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// FrontedWindowsView.xaml 的交互逻辑
/// </summary>
public partial class FrontedWindowsView : UserControl
{
    private CancellationTokenSource _tutorialLifetime = new();

    /// <summary>Gets the lifetime token for this child tutorial owner.</summary>
    internal CancellationToken TutorialLifetimeToken => _tutorialLifetime.Token;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrontedWindowsView"/> class.
    /// </summary>
    public FrontedWindowsView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_tutorialLifetime.IsCancellationRequested)
            {
                _tutorialLifetime.Dispose();
                _tutorialLifetime = new CancellationTokenSource();
            }
        };
        Unloaded += (_, _) => _tutorialLifetime.Cancel();
    }

}
