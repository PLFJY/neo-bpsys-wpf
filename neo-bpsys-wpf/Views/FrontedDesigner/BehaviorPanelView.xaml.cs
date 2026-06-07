using System.Windows.Controls;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using neo_bpsys_wpf.Views.Windows;

namespace neo_bpsys_wpf.Views.FrontedDesigner;

public partial class BehaviorPanelView : UserControl
{
    public BehaviorPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is BehaviorPanelViewModel oldViewModel)
        {
            oldViewModel.AnimationEditorRequested -= OpenAnimationEditor;
        }

        if (e.NewValue is BehaviorPanelViewModel newViewModel)
        {
            newViewModel.AnimationEditorRequested += OpenAnimationEditor;
        }
    }

    private void OpenAnimationEditor(FrontedBehaviorAnimationEditorViewModel viewModel)
    {
        var window = new FrontedBehaviorAnimationEditorWindow(viewModel)
        {
            Owner = System.Windows.Window.GetWindow(this)
        };
        window.ShowDialog();
    }
}
