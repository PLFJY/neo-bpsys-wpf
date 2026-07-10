using System;
using System.IO;
using System.Windows;
using neo_bpsys_wpf.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

/// <summary>Tests custom title-bar event subscription behavior.</summary>
public sealed class CustomTitleBarTest
{
    [Fact]
    public void CustomTitleBar_RepeatedLoaded_ShouldNotDuplicateMaximizeHandler()
    {
        var source = ReadSource();
        var onLoadedStart = source.IndexOf("private void OnLoaded", StringComparison.Ordinal);
        var onUnloadedStart = source.IndexOf("private void OnUnloaded", onLoadedStart, StringComparison.Ordinal);
        var onLoaded = source[onLoadedStart..onUnloadedStart];

        Assert.Equal(1, Count(source, "MaximizeButton.Click += MaximizeButton_OnClick"));
        Assert.Equal(1, Count(source, "TitleBar.MouseDown += TitleBar_MouseDown"));
        Assert.DoesNotContain("MaximizeButton.Click +=", onLoaded);
        Assert.DoesNotContain("TitleBar.MouseDown +=", onLoaded);
        Assert.Contains("_hostWindow.StateChanged -= HostWindow_OnStateChanged", source);
        Assert.Contains("_hostWindow.StateChanged += HostWindow_OnStateChanged", source);
    }

    [Fact]
    public void CustomTitleBar_SingleClick_ShouldToggleWindowStateOnce()
    {
        Assert.Equal(WindowState.Maximized, CustomTitleBar.ToggleWindowStateOnce(WindowState.Normal));
        Assert.Equal(WindowState.Normal, CustomTitleBar.ToggleWindowStateOnce(WindowState.Maximized));
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ReadSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "neo-bpsys-wpf.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "neo-bpsys-wpf",
            "Controls",
            "CustomTitleBar.xaml.cs"));
    }
}
