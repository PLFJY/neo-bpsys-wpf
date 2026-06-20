using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// SmartBP module content view.
/// </summary>
public partial class SmartBpModuleContentView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmartBpModuleContentView"/> class.
    /// </summary>
    public SmartBpModuleContentView()
    {
        InitializeComponent();
    }

    private void AiDebugConsoleTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.ScrollToEnd();
    }

    private void DebugTextBox_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var scrollViewer = FindDescendant<ScrollViewer>(textBox);
        if (scrollViewer is null || scrollViewer.ScrollableHeight <= 0)
            return;

        var isWheelUp = e.Delta > 0;
        var canScrollInside = isWheelUp
            ? scrollViewer.VerticalOffset > 0
            : scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;
        if (!canScrollInside)
            return;

        var wheelLines = Math.Max(1, SystemParameters.WheelScrollLines);
        var targetOffset = scrollViewer.VerticalOffset - e.Delta / 120D * wheelLines * 16D;
        scrollViewer.ScrollToVerticalOffset(Math.Clamp(targetOffset, 0D, scrollViewer.ScrollableHeight));
        e.Handled = true;
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
                return typedChild;
            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }
}
