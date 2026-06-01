#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedDesignerHelpTest
{
    private static string GetRepositoryPath(
        string first,
        string second,
        string third,
        string? fourth = null,
        [CallerFilePath] string sourceFilePath = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", ".."));
        return fourth is null
            ? Path.Combine(repositoryRoot, first, second, third)
            : Path.Combine(repositoryRoot, first, second, third, fourth);
    }

    [Fact]
    public void FrontedDesignerBottomBarContainsHelpButton()
    {
        var xaml = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml"));

        Assert.Contains("OpenDesignerHelp_OnClick", xaml, StringComparison.Ordinal);
        Assert.Contains("QuestionCircle24", xaml, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Open", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontedDesignerCodeBehindContainsHelpClickHandler()
    {
        var code = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml.cs"));

        Assert.Contains("OpenDesignerHelp_OnClick", code, StringComparison.Ordinal);
        Assert.Contains("FrontedDesignerHelpWindow", code, StringComparison.Ordinal);
        Assert.Contains("_helpWindow", code, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontedDesignerHelpWindowXamlExists()
    {
        var xamlPath = GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerHelpWindow.xaml");

        Assert.True(File.Exists(xamlPath), "Help window XAML file should exist.");
    }

    [Fact]
    public void FrontedDesignerHelpWindowCodeBehindExists()
    {
        var csPath = GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerHelpWindow.xaml.cs");

        Assert.True(File.Exists(csPath), "Help window code-behind file should exist.");
    }

    [Fact]
    public void FrontedDesignerHelpWindowContainsScrollViewer()
    {
        var xaml = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerHelpWindow.xaml"));

        Assert.Contains("<ScrollViewer", xaml, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontedDesignerHelpWindowContainsShortcutSection()
    {
        var xaml = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerHelpWindow.xaml"));

        Assert.Contains("Designer.Help.Shortcuts.Title", xaml, StringComparison.Ordinal);
        Assert.Contains("HelpShortcut", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontedDesignerHelpWindowContainsLayerPanelSection()
    {
        var code = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerHelpWindow.xaml.cs"));

        Assert.Contains("Designer.Help.LayerPanel.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.LayerPanel.Content", code, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontedDesignerHelpWindowContainsPluginControlsSection()
    {
        var code = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerHelpWindow.xaml.cs"));

        Assert.Contains("Designer.Help.PluginControls.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.PluginControls.Content", code, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalizationKeysExistInAllResxFiles()
    {
        var requiredKeys = new[]
        {
            "Designer.Help.Title",
            "Designer.Help.Open",
            "Designer.Help.Close",
            "Designer.Help.BasicWorkflow.Title",
            "Designer.Help.CanvasNavigation.Title",
            "Designer.Help.Selection.Title",
            "Designer.Help.Shortcuts.Title",
            "Designer.Help.Snapping.Title",
            "Designer.Help.LayerPanel.Title",
            "Designer.Help.PropertyGrid.Title",
            "Designer.Help.AddDeleteCopyPaste.Title",
            "Designer.Help.CanvasWindowSettings.Title",
            "Designer.Help.Validation.Title",
            "Designer.Help.PluginControls.Title",
            "Designer.Help.LayoutPackages.Title",
            "Designer.Help.Troubleshooting.Title",
            "Designer.Help.Shortcut.Save",
            "Designer.Help.Shortcut.Copy",
            "Designer.Help.Shortcut.Paste",
            "Designer.Help.Shortcut.Delete",
            "Designer.Help.Shortcut.Undo",
            "Designer.Help.Shortcut.Redo",
            "Designer.Help.Shortcut.Zoom",
            "Designer.Help.Shortcut.PanSpace",
            "Designer.Help.Shortcut.PanRight",
            "Designer.Help.Shortcut.SnapShift",
            "Designer.Help.BasicWorkflow.Content",
            "Designer.Help.CanvasNavigation.Content",
            "Designer.Help.Selection.Content",
            "Designer.Help.Snapping.Content",
            "Designer.Help.LayerPanel.Content",
            "Designer.Help.PropertyGrid.Content",
            "Designer.Help.AddDeleteCopyPaste.Content",
            "Designer.Help.CanvasWindowSettings.Content",
            "Designer.Help.Validation.Content",
            "Designer.Help.PluginControls.Content",
            "Designer.Help.LayoutPackages.Content",
            "Designer.Help.Troubleshooting.Content"
        };

        var resxFiles = new[]
        {
            GetRepositoryPath("neo-bpsys-wpf", "Locales", "Lang.resx"),
            GetRepositoryPath("neo-bpsys-wpf", "Locales", "Lang.en-us.resx"),
            GetRepositoryPath("neo-bpsys-wpf", "Locales", "Lang.ja-jp.resx")
        };

        foreach (var resxPath in resxFiles)
        {
            Assert.True(File.Exists(resxPath), $"RESX file should exist: {resxPath}");
            var xml = XDocument.Load(resxPath);
            var allKeys = xml.Descendants("data")
                .Select(data => data.Attribute("name")?.Value)
                .Where(name => name is not null)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var key in requiredKeys)
            {
                Assert.True(
                    allKeys.Contains(key),
                    $"Missing key '{key}' in {Path.GetFileName(resxPath)}");
            }
        }
    }

    [Fact]
    public void FrontedDesignerCodeBehindOpensHelpWindowWithoutDuplicates()
    {
        var code = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml.cs"));

        Assert.Contains("_helpWindow is null || !_helpWindow.IsVisible", code, StringComparison.Ordinal);
        Assert.Contains("_helpWindow.Activate", code, StringComparison.Ordinal);
        Assert.Contains("_helpWindow.Show", code, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontedDesignerBottomZoomControlsRemain()
    {
        var xaml = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerWindow.xaml"));

        Assert.Contains("ZoomComboBox", xaml, StringComparison.Ordinal);
        Assert.Contains("ZoomPercent", xaml, StringComparison.Ordinal);
        Assert.Contains("FitToWindowCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("CurrentWindowCanvasDisplay", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpWindowContainsCloseButton()
    {
        var xaml = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerHelpWindow.xaml"));

        Assert.Contains("Close_OnClick", xaml, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Close", xaml, StringComparison.Ordinal);
        Assert.Contains("Dismiss24", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpWindowUsesConsistentWindowChrome()
    {
        var xaml = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerHelpWindow.xaml"));

        Assert.Contains("<WindowChrome", xaml, StringComparison.Ordinal);
        Assert.Contains("CaptionHeight=\"35\"", xaml, StringComparison.Ordinal);
        Assert.Contains("UseAeroCaptionButtons=\"False\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpWindowContainsAllMajorSections()
    {
        var code = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerHelpWindow.xaml.cs"));

        Assert.Contains("Designer.Help.BasicWorkflow.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.CanvasNavigation.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Selection.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Snapping.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.LayerPanel.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.PropertyGrid.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.AddDeleteCopyPaste.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.CanvasWindowSettings.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Validation.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.PluginControls.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.LayoutPackages.Title", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Troubleshooting.Title", code, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpWindowContainsAllShortcutRows()
    {
        var code = File.ReadAllText(GetRepositoryPath(
            "neo-bpsys-wpf",
            "Views",
            "Windows",
            "FrontedDesignerHelpWindow.xaml.cs"));

        Assert.Contains("Designer.Help.Shortcut.Save", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Shortcut.Copy", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Shortcut.Paste", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Shortcut.Delete", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Shortcut.Undo", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Shortcut.Redo", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Shortcut.Zoom", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Shortcut.PanSpace", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Shortcut.PanRight", code, StringComparison.Ordinal);
        Assert.Contains("Designer.Help.Shortcut.SnapShift", code, StringComparison.Ordinal);
    }
}
