#nullable enable

using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Linq;
using neo_bpsys_wpf.ProductTour.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

/// <summary>Tests non-invasive overlay host behavior.</summary>
public sealed class OverlayHostTest
{
    [Fact]
    public void OverlayHost_ShouldNotReplaceWindowContent()
    {
        WpfTestThread.Run(() =>
        {
            var businessGrid = new Grid();
            businessGrid.RowDefinitions.Add(new RowDefinition());
            businessGrid.RowDefinitions.Add(new RowDefinition());
            var businessContent = new Border();
            businessGrid.Children.Add(businessContent);
            var window = CreateWindow(businessGrid);

            try
            {
                var host = OverlayHost.GetHostPanel(window);
                var overlay = new ProductTourOverlay();
                host.Children.Add(overlay);

                Assert.Same(businessGrid, window.Content);
                Assert.Contains(businessContent, businessGrid.Children.Cast<UIElement>());
                Assert.Contains(host, businessGrid.Children.Cast<UIElement>());
                Assert.Contains(overlay, host.Children.Cast<UIElement>());
                Assert.Same(host, OverlayHost.GetHostPanel(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Overlay_ShouldNotForceWindowDesiredSize()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "neo-bpsys-wpf.ProductTour",
            "Controls",
            "ProductTourOverlay.cs"));

        Assert.DoesNotContain("Width = width", source);
        Assert.DoesNotContain("Height = height", source);
        Assert.Contains("var width = ActualWidth;", source);
        Assert.Contains("var height = ActualHeight;", source);
    }

    [Fact]
    public void MaximizedWindow_ShouldRemainMaximizedWhenOverlayAppearsAndIsRemoved()
    {
        WpfTestThread.Run(() =>
        {
            var window = CreateWindow(new Grid());
            window.Show();
            window.WindowState = WindowState.Maximized;
            try
            {
                var host = OverlayHost.GetHostPanel(window);
                var overlay = new ProductTourOverlay();
                host.Children.Add(overlay);
                Assert.Equal(WindowState.Maximized, window.WindowState);

                host.Children.Remove(overlay);
                Assert.Equal(WindowState.Maximized, window.WindowState);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Window CreateWindow(object content) => new()
    {
        Content = content,
        Width = 640,
        Height = 360,
        WindowStyle = WindowStyle.None,
        ShowInTaskbar = false,
        Left = -10000,
        Top = -10000
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
