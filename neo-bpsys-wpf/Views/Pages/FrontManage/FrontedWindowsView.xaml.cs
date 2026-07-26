using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
    private Task<TutorialRunResult>? _tutorialTask;

    /// <summary>
    /// 初始化 <see cref="FrontedWindowsView"/> 类的新实例。
    /// </summary>
    public FrontedWindowsView()
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
}
