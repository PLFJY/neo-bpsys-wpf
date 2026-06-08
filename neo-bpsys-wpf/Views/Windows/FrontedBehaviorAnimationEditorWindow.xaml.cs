using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using neo_bpsys_wpf.Controls.Modern.Scrolling;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using neo_bpsys_wpf.Views.FrontedDesigner.GraphEditor;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedBehaviorAnimationEditorWindow : FluentWindow
{
    private FrontedBehaviorAnimationHelpWindow? _helpWindow;

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
                SymbolRegular.TableEdit24,
                views[index]));
        }

        Loaded += (_, _) => AnimationTabs.SelectFirstItemIfNoneSelected();
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
