using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using neo_bpsys_wpf.Controls.Modern.Scrolling;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using neo_bpsys_wpf.Views.FrontedDesigner.GraphEditor;
using Wpf.Ui.Controls;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedBehaviorAnimationEditorWindow : FluentWindow
{
    private readonly ITutorialRunner? _tutorialRunner;
    private FrontedBehaviorAnimationHelpWindow? _helpWindow;
    private bool _forceClose;
    private bool _isClosePromptOpen;
    private bool _discardedBeforeClose;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrontedBehaviorAnimationEditorWindow"/> class.
    /// </summary>
    /// <param name="viewModel">Animation editor view model.</param>
    /// <param name="tutorialRunner">Tutorial runner.</param>
    public FrontedBehaviorAnimationEditorWindow(
        FrontedBehaviorAnimationEditorViewModel viewModel,
        ITutorialRunner? tutorialRunner = null)
    {
        _tutorialRunner = tutorialRunner;
        InitializeComponent();
        DataContext = viewModel;
        Title = viewModel.Title;

        Type[] views = [typeof(AnimationStageZeroView), typeof(AnimationStageOneView), typeof(AnimationStageTwoView)];
        for (var index = 0; index < viewModel.Stages.Count; index++)
        {
            AnimationTabs.MenuItems.Add(new NavigationViewItem(
                viewModel.Stages[index].DisplayName,
                SymbolRegular.EditSettings24,
                views[index]));
        }

        Loaded += (_, _) =>
        {
            AnimationTabs.SelectFirstItemIfNoneSelected();
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(async () =>
                {
                    var runner = _tutorialRunner
                        ?? IAppHost.Host?.Services.GetService(typeof(ITutorialRunner)) as ITutorialRunner;
                    if (runner != null)
                    {
                        await runner.RunUntilBlockedAsync(this, TutorialPageKey);
                    }
                }));
        };
        Closed += OnClosed;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_forceClose || DataContext is not FrontedBehaviorAnimationEditorViewModel vm || !vm.HasUnsavedChanges)
        {
            return;
        }

        e.Cancel = true;
        if (_isClosePromptOpen)
        {
            return;
        }

        _ = ConfirmCloseAsync(vm);
    }

    private async Task ConfirmCloseAsync(FrontedBehaviorAnimationEditorViewModel vm)
    {
        _isClosePromptOpen = true;
        try
        {
            var result = await ShowUnsavedChangesPromptAsync();
            switch (result)
            {
                case MessageBoxResult.Primary:
                    if (await vm.SaveAllAsync())
                    {
                        ForceCloseNow();
                    }
                    else
                    {
                        await ShowSaveFailedAsync();
                    }
                    break;
                case MessageBoxResult.Secondary:
                    vm.DiscardAll();
                    _discardedBeforeClose = true;
                    ForceCloseNow();
                    break;
                case MessageBoxResult.None:
                default:
                    break;
            }
        }
        finally
        {
            if (!_forceClose)
            {
                _isClosePromptOpen = false;
            }
        }
    }

    private Task<MessageBoxResult> ShowUnsavedChangesPromptAsync() =>
        MessageBoxHelper.ShowThreeOptionAsync(
            I18nHelper.GetLocalizedString("Designer.AnimationEditor.UnsavedChangesMessage"),
            I18nHelper.GetLocalizedString("Designer.AnimationEditor.Title"),
            I18nHelper.GetLocalizedString("Save"),
            I18nHelper.GetLocalizedString("DiscardChanges"),
            I18nHelper.GetLocalizedString("Cancel"),
            width: 500,
            minWidth: 460,
            primaryButtonIcon: SymbolRegular.Save24,
            secondaryButtonIcon: SymbolRegular.Delete24,
            closeButtonIcon: SymbolRegular.Dismiss24);

    private async Task ShowSaveFailedAsync()
    {
        var errorBox = new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            Title = I18nHelper.GetLocalizedString("Designer.AnimationEditor.SaveFailedTitle"),
            Content = I18nHelper.GetLocalizedString("Designer.AnimationEditor.SaveFailedMessage"),
            PrimaryButtonText = I18nHelper.GetLocalizedString("Confirm"),
            PrimaryButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Checkmark24 },
            CloseButtonText = I18nHelper.GetLocalizedString("Cancel"),
            CloseButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Dismiss24 },
            Width = 400,
            MinWidth = 360
        };
        await errorBox.ShowDialogAsync();
    }

    private void ForceCloseNow()
    {
        _forceClose = true;
        _isClosePromptOpen = false;
        Dispatcher.BeginInvoke(new Action(Close), DispatcherPriority.Background);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (!_discardedBeforeClose && DataContext is FrontedBehaviorAnimationEditorViewModel vm)
        {
            vm.DiscardAll();
        }

        _helpWindow?.Close();
        _helpWindow = null;
    }

    private void OpenHelp_OnClick(object sender, RoutedEventArgs e)
    {
        if (_helpWindow is null || !_helpWindow.IsVisible)
        {
            _helpWindow = new FrontedBehaviorAnimationHelpWindow
            {
                Owner = this
            };
            _helpWindow.Closed += (_, _) => _helpWindow = null;
            _helpWindow.Show();
            return;
        }

        _helpWindow.Activate();
    }
}

public abstract class AnimationStageViewBase : UserControl
{
    protected AnimationStageViewBase(int index)
    {
        ModernScroll.SetOwnership(this, ModernScrollOwnership.Self);

        var editor = new FrontedNodeGraphEditorView();
        editor.SetBinding(DataContextProperty, new Binding($"Stages[{index}].GraphEditor"));
        Content = editor;
    }
}

public sealed class AnimationStageZeroView() : AnimationStageViewBase(0);
public sealed class AnimationStageOneView() : AnimationStageViewBase(1);
public sealed class AnimationStageTwoView() : AnimationStageViewBase(2);
