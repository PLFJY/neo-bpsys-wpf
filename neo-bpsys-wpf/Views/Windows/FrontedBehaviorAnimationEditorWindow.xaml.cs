using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using neo_bpsys_wpf.Controls.Modern.Scrolling;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using neo_bpsys_wpf.Views.FrontedDesigner.GraphEditor;
using Wpf.Ui.Controls;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedBehaviorAnimationEditorWindow : FluentWindow
{
    private FrontedBehaviorAnimationHelpWindow? _helpWindow;
    private bool _forceClose;
    private bool _isClosePromptOpen;

    public FrontedBehaviorAnimationEditorWindow(FrontedBehaviorAnimationEditorViewModel viewModel)
    {
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

        Loaded += (_, _) => AnimationTabs.SelectFirstItemIfNoneSelected();
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
        var messageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = "动画编辑器",
            Content = "有未保存的更改，是否保存？",
            PrimaryButtonText = "保存",
            SecondaryButtonText = "丢弃",
            CloseButtonText = "取消",
            PrimaryButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Save24 },
            SecondaryButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Delete24 },
            CloseButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Dismiss24 },
            Width = 500,
            MinWidth = 460
        };
        var result = await messageBox.ShowDialogAsync();

        switch (result)
        {
            case MessageBoxResult.Primary:
                await vm.SaveAllAsync();
                if (vm.HasUnsavedChanges)
                {
                    var errorBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Title = "保存失败",
                        Content = "保存失败，请重试。",
                        PrimaryButtonText = "确定",
                        CloseButtonText = "取消",
                        Width = 400,
                        MinWidth = 360
                    };
                    await errorBox.ShowDialogAsync();
                    _isClosePromptOpen = false;
                    return;
                }
                break;
            case MessageBoxResult.None:
                _isClosePromptOpen = false;
                return;
        }

        _forceClose = true;
        _isClosePromptOpen = false;
        Close();
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
