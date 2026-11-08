using System.ComponentModel;
using System.Diagnostics;
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

/// <summary>
/// 动画编辑器窗口，承载行为图编辑、预览与帮助。
/// </summary>
public partial class FrontedBehaviorAnimationEditorWindow : FluentWindow
{
    private readonly ITutorialRunner? _tutorialRunner;
    private FrontedBehaviorAnimationHelpWindow? _helpWindow;
    private bool _forceClose;
    private bool _isClosePromptOpen;
    private bool _discardedBeforeClose;
    private readonly CancellationTokenSource _tutorialLifetime = new();

    /// <summary>
    /// 初始化 <see cref="FrontedBehaviorAnimationEditorWindow"/> 类的新实例。
    /// </summary>
    /// <param name="viewModel">动画编辑器视图模型。</param>
    /// <param name="tutorialRunner">教程运行器。</param>
    public FrontedBehaviorAnimationEditorWindow(
        FrontedBehaviorAnimationEditorViewModel viewModel,
        ITutorialRunner? tutorialRunner = null)
    {
        _tutorialRunner = tutorialRunner;
        InitializeComponent();
        WindowBackdropThemeHelper.Attach(this);
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

        Loaded += async (_, _) =>
        {
            var token = _tutorialLifetime.Token;
            try
            {
                AnimationTabs.SelectFirstItemIfNoneSelected();
                await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle, token);
                await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render, token);
                if (!IsVisible || AnimationTabs.SelectedItem == null)
                {
                    return;
                }

                var runner = _tutorialRunner
                    ?? IAppHost.Host?.Services.GetService(typeof(ITutorialRunner)) as ITutorialRunner;
                if (runner != null)
                {
                    await runner.RunSequenceAsync(this, TutorialPageKey, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                // Loaded 是 async void 的边界，未捕获异常会直接结束进程。
                Debug.WriteLine($"[AnimationEditor] 教程初始化失败: {ex.Message}");
            }
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
            I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationEditor.UnsavedChangesMessage"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationEditor.Title"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Save"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "DiscardChanges"),
            I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"),
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
            Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationEditor.SaveFailedTitle"),
            Content = I18nHelper.GetLocalizedString(AppI18nDictionaries.AnimationEditor, "Designer.AnimationEditor.SaveFailedMessage"),
            PrimaryButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
            PrimaryButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Checkmark24 },
            CloseButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"),
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
        _tutorialLifetime.Cancel();
        _tutorialLifetime.Dispose();
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

/// <summary>
/// 动画编辑器中承载图编辑器的阶段视图基类。
/// </summary>
public abstract class AnimationStageViewBase : UserControl
{
    /// <summary>
    /// 初始化阶段视图。
    /// </summary>
    /// <param name="index">阶段索引，对应动画编辑器的阶段集合。</param>
    protected AnimationStageViewBase(int index)
    {
        ModernScroll.SetOwnership(this, ModernScrollOwnership.Self);

        var editor = new FrontedNodeGraphEditorView();
        editor.SetBinding(DataContextProperty, new Binding($"Stages[{index}].GraphEditor"));
        Content = editor;
    }
}

/// <summary>第 1 个动画阶段的视图。</summary>
public sealed class AnimationStageZeroView() : AnimationStageViewBase(0);

/// <summary>第 2 个动画阶段的视图。</summary>
public sealed class AnimationStageOneView() : AnimationStageViewBase(1);

/// <summary>第 3 个动画阶段的视图。</summary>
public sealed class AnimationStageTwoView() : AnimationStageViewBase(2);
