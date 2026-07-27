using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// FrontedLayoutPackagesView.xaml 的交互逻辑
/// </summary>
public partial class FrontedLayoutPackagesView : UserControl
{
    /// <summary>布局包视图教程 Key。</summary>
    public const string TutorialPageKey = "Page.FrontManage.LayoutPackages";

    private CancellationTokenSource _tutorialLifetime = new();
    private Task<TutorialRunResult>? _tutorialTask;

    /// <summary>
    /// 初始化 <see cref="FrontedLayoutPackagesView"/> 类的新实例。
    /// </summary>
    public FrontedLayoutPackagesView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RecreateTutorialLifetimeIfNeeded();
            QueueTutorialRun();
        };
        IsVisibleChanged += (_, e) =>
        {
            if (Equals(e.NewValue, true))
            {
                RecreateTutorialLifetimeIfNeeded();
                QueueTutorialRun();
            }
        };
        Unloaded += (_, _) => _tutorialLifetime.Cancel();
    }

    private void RecreateTutorialLifetimeIfNeeded()
    {
        if (!_tutorialLifetime.IsCancellationRequested)
        {
            return;
        }

        _tutorialLifetime.Dispose();
        _tutorialLifetime = new CancellationTokenSource();
        _tutorialTask = null;
    }

    private void QueueTutorialRun()
    {
        if (_tutorialTask is { IsCompleted: false })
        {
            return;
        }

        _tutorialTask = RunTutorialWhenVisibleAsync();
    }

    private async Task<TutorialRunResult> RunTutorialWhenVisibleAsync()
    {
        // Snapshot the token: _tutorialLifetime may be replaced by
        // RecreateTutorialLifetimeIfNeeded between the InvokeAsync call and the
        // catch filter, which would otherwise let the OperationCanceledException
        // escape and surface as an unobserved task exception.
        var token = _tutorialLifetime.Token;
        try
        {
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle, token);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render, token);
            if (!IsLoaded || !IsVisible || Window.GetWindow(this) is not { IsVisible: true })
            {
                return TutorialRunResult.NotReady;
            }

            var runner = IAppHost.Host?.Services.GetService<ITutorialRunner>();
            return runner == null
                ? TutorialRunResult.NotReady
                : await runner.RunSequenceAsync(this, TutorialPageKey, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return TutorialRunResult.Canceled;
        }
    }

    private void PackageListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FrontManagePageViewModel viewModel
            && viewModel.ActivateSelectedPackageByDoubleClickCommand.CanExecute(null))
        {
            viewModel.ActivateSelectedPackageByDoubleClickCommand.Execute(null);
        }
    }

    private void PackageListBox_OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        if (sender == LayoutPackageList)
        {
            e.Handled = true;
        }
    }

    private void PackageListBox_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox
            || e.OriginalSource is not DependencyObject source
            || ItemsControl.ContainerFromElement(listBox, source) is not ListBoxItem item)
        {
            return;
        }

        listBox.SelectedItem = item.DataContext;
    }
}
