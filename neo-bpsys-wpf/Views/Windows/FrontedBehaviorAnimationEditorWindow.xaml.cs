using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedBehaviorAnimationEditorWindow : FluentWindow
{
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
                SymbolRegular.Flow24,
                views[index]));
        }

        Loaded += (_, _) => AnimationTabs.SelectFirstItemIfNoneSelected();
    }
}

public abstract class AnimationStageViewBase : UserControl
{
    protected AnimationStageViewBase(int index)
    {
        var panel = new StackPanel();
        var placeholder = new System.Windows.Controls.TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        placeholder.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new Binding(nameof(FrontedBehaviorAnimationEditorViewModel.Placeholder)));
        panel.Children.Add(placeholder);

        var stats = new System.Windows.Controls.TextBlock { Margin = new Thickness(0, 16, 0, 0), Opacity = 0.78 };
        stats.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new Binding($"Stages[{index}]")
        {
            Converter = new AnimationStageStatsConverter()
        });
        panel.Children.Add(stats);
        Content = panel;
    }
}

public sealed class AnimationStageZeroView() : AnimationStageViewBase(0);
public sealed class AnimationStageOneView() : AnimationStageViewBase(1);
public sealed class AnimationStageTwoView() : AnimationStageViewBase(2);

public sealed class AnimationStageStatsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is FrontedBehaviorAnimationStageViewModel stage
            ? $"{stage.NodeCount} nodes / {stage.LinkCount} links"
            : "0 nodes / 0 links";

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
