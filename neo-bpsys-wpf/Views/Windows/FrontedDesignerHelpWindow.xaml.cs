using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

public sealed class HelpSection
{
    public string Title { get; set; } = string.Empty;
    public ObservableCollection<string> Items { get; } = [];
}

public sealed class HelpShortcut
{
    public string Shortcut { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public partial class FrontedDesignerHelpWindow : FluentWindow
{
    public ObservableCollection<HelpSection> Sections { get; } = [];
    public ObservableCollection<HelpShortcut> Shortcuts { get; } = [];

    public FrontedDesignerHelpWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadHelpContent();
    }

    private void LoadHelpContent()
    {
        AddSection("Designer.Help.BasicWorkflow.Title", "Designer.Help.BasicWorkflow.Content");
        AddSection("Designer.Help.CanvasNavigation.Title", "Designer.Help.CanvasNavigation.Content");
        AddSection("Designer.Help.Selection.Title", "Designer.Help.Selection.Content");
        AddSection("Designer.Help.Snapping.Title", "Designer.Help.Snapping.Content");
        AddSection("Designer.Help.LayerPanel.Title", "Designer.Help.LayerPanel.Content");
        AddSection("Designer.Help.PropertyGrid.Title", "Designer.Help.PropertyGrid.Content");
        AddSection("Designer.Help.AddDeleteCopyPaste.Title", "Designer.Help.AddDeleteCopyPaste.Content");
        AddSection("Designer.Help.CanvasWindowSettings.Title", "Designer.Help.CanvasWindowSettings.Content");
        AddSection("Designer.Help.Validation.Title", "Designer.Help.Validation.Content");
        AddSection("Designer.Help.PluginControls.Title", "Designer.Help.PluginControls.Content");
        AddSection("Designer.Help.LayoutPackages.Title", "Designer.Help.LayoutPackages.Content");
        AddSection("Designer.Help.Troubleshooting.Title", "Designer.Help.Troubleshooting.Content");

        AddShortcut("Designer.Help.Shortcut.Save");
        AddShortcut("Designer.Help.Shortcut.Copy");
        AddShortcut("Designer.Help.Shortcut.Paste");
        AddShortcut("Designer.Help.Shortcut.Delete");
        AddShortcut("Designer.Help.Shortcut.Undo");
        AddShortcut("Designer.Help.Shortcut.Redo");
        AddShortcut("Designer.Help.Shortcut.Zoom");
        AddShortcut("Designer.Help.Shortcut.PanSpace");
        AddShortcut("Designer.Help.Shortcut.PanRight");
        AddShortcut("Designer.Help.Shortcut.SnapShift");
    }

    private void AddSection(string titleKey, string contentKey)
    {
        var title = I18nHelper.GetLocalizedString(titleKey);
        var content = I18nHelper.GetLocalizedString(contentKey);
        var section = new HelpSection { Title = title };

        foreach (var line in content.Split(new[] { "\\n" }, StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                section.Items.Add($"• {trimmed}");
            }
        }

        Sections.Add(section);
    }

    private void AddShortcut(string key)
    {
        var text = I18nHelper.GetLocalizedString(key);
        var colonIndex = text.IndexOf(':');
        if (colonIndex > 0 && colonIndex < text.Length - 1)
        {
            Shortcuts.Add(new HelpShortcut
            {
                Shortcut = text[..colonIndex].Trim(),
                Description = text[(colonIndex + 1)..].Trim()
            });
        }
        else
        {
            Shortcuts.Add(new HelpShortcut
            {
                Shortcut = text,
                Description = string.Empty
            });
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
