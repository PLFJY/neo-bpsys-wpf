#nullable enable

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.ProductTour.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

/// <summary>
/// Tests overlay host attachment behavior.
/// </summary>
public sealed class OverlayHostTest
{
    [Fact]
    public void WindowPanelContentIsWrappedBeforeOverlayAttachment()
    {
        WpfTestThread.Run(() =>
        {
            var businessGrid = new Grid();
            businessGrid.RowDefinitions.Add(new RowDefinition());
            businessGrid.RowDefinitions.Add(new RowDefinition());
            businessGrid.Children.Add(new Border());

            var window = new Window
            {
                Content = businessGrid,
                Width = 640,
                Height = 360,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Left = -10000,
                Top = -10000
            };

            try
            {
                var host = OverlayHost.GetHostPanel(window);
                var overlay = new ProductTourOverlay();
                host.Children.Add(overlay);

                Assert.NotSame(businessGrid, host);
                Assert.Same(host, window.Content);
                Assert.Same(businessGrid, host.Children[0]);
                Assert.Same(overlay, host.Children[1]);
                Assert.DoesNotContain(overlay, businessGrid.Children.OfType<UIElement>());
                Assert.Same(host, OverlayHost.GetHostPanel(window));
            }
            finally
            {
                window.Close();
            }
        });
    }
}
