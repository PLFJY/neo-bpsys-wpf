using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// FrontedDesignerWindow 的TutorialRuntime业务交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow
{
    private void QueueDesignerTutorial()
    {
        if (_designerTutorialTask is { IsCompleted: false })
        {
            return;
        }

        _logger?.LogInformation("Designer sequence queued.");
        _designerTutorialTask = RunDesignerTutorialAsync();
    }

    private async Task<TutorialRunResult> RunDesignerTutorialAsync()
    {
        var token = _tutorialLifetime.Token;
        try
        {
            await _initialPreviewReady.Task.WaitAsync(token);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle, token);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render, token);
            if (!IsLoaded || !IsVisible)
            {
                return TutorialRunResult.NotReady;
            }

            var runner = _tutorialRunner
                ?? IAppHost.Host?.Services.GetService(typeof(ITutorialRunner)) as ITutorialRunner;
            if (runner == null)
            {
                return TutorialRunResult.NotReady;
            }

            _logger?.LogInformation("Designer sequence started.");
            var result = await runner.RunSequenceAsync(this, TutorialPageKeys.DesignerV3, token);
            _logger?.LogInformation("Designer sequence result. Result={Result}", result);
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return TutorialRunResult.Canceled;
        }
    }

    private void TryQueuePropertyPanelTutorial()
    {
        if (_propertyPanelTutorialTriggered)
        {
            return;
        }

        if (_propertyPanelTutorialTask is { IsCompleted: false })
        {
            return;
        }

        _propertyPanelTutorialTriggered = true;
        _propertyPanelTutorialTask = RunPropertyPanelTutorialAsync();
    }

    private async Task<TutorialRunResult> RunPropertyPanelTutorialAsync()
    {
        var token = _tutorialLifetime.Token;
        try
        {
            await WaitForPropertyGridReadyAsync(token);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle, token);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render, token);
            if (_viewModel?.SelectedDesignItem == null || !IsVisible)
            {
                return TutorialRunResult.NotReady;
            }

            var runner = _tutorialRunner
                ?? IAppHost.Host?.Services.GetService(typeof(ITutorialRunner)) as ITutorialRunner;
            if (runner == null)
            {
                return TutorialRunResult.NotReady;
            }

            return await runner.RunPackageAsync(this, Tours.PropertyPanelBasic, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return TutorialRunResult.Canceled;
        }
    }

    private async Task LoadInitialLayoutAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.ReloadLayoutCoreAsync();
        _lastAcceptedWindow = _viewModel.SelectedWindow;
        _initialLayoutLoaded = true;
    }

    private async Task WaitForPropertyGridReadyAsync(CancellationToken cancellationToken)
    {
        if (_viewModel?.IsRebuildingPropertyGrid != true)
        {
            return;
        }

        _propertyGridReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_viewModel.IsRebuildingPropertyGrid)
        {
            await _propertyGridReady.Task.WaitAsync(cancellationToken);
        }

        _propertyGridReady = null;
    }

    private bool CanRunBehaviorPanelTutorial() =>
        _viewModel?.SelectedDesignItem != null
        && BehaviorExpander.IsExpanded
        && BehaviorPanelHost.IsVisible
        && BehaviorPanelHost.DataContext is neo_bpsys_wpf.ViewModels.FrontedDesigner.BehaviorPanelViewModel
        {
            HasSelectedControl: true
        };

    private async Task<TutorialRunResult> RunBehaviorPanelTutorialAsync()
    {
        var token = _tutorialLifetime.Token;
        try
        {
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle, token);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render, token);
            if (!CanRunBehaviorPanelTutorial())
            {
                return TutorialRunResult.NotReady;
            }

            var runner = _tutorialRunner
                ?? IAppHost.Host?.Services.GetService(typeof(ITutorialRunner)) as ITutorialRunner;
            return runner == null
                ? TutorialRunResult.NotReady
                : await runner.RunSequenceAsync(
                    BehaviorPanelHost,
                    neo_bpsys_wpf.Views.FrontedDesigner.BehaviorPanelView.TutorialPageKey,
                    token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return TutorialRunResult.Canceled;
        }
    }

}
