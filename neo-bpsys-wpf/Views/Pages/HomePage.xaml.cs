using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// HomePage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("76538B42-BA3A-4A86-B696-902D3AF9E777",
    "HomePage",
    SymbolRegular.Home24,
    BackendPageCategory.Internal)]
public partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
    }

    private void ReleaseNotesMarkdownViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var parentScrollViewer = FindAncestor<ScrollViewer>((DependencyObject)sender);
        if (parentScrollViewer == null)
        {
            return;
        }

        e.Handled = true;
        var forwardedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = sender
        };
        parentScrollViewer.RaiseEvent(forwardedEvent);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
