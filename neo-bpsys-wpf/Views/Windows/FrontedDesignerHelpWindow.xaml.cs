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

public sealed class HelpBuiltInControlRow
{
    public string ControlType { get; set; } = string.Empty;
    public string Usage { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public partial class FrontedDesignerHelpWindow : FluentWindow
{
    public ObservableCollection<HelpSection> Sections { get; } = [];
    public ObservableCollection<HelpShortcut> Shortcuts { get; } = [];
    public ObservableCollection<HelpBuiltInControlRow> BuiltInControls { get; } = [];
    public ObservableCollection<HelpSection> PostShortcutSections { get; } = [];

    public string BuiltInControlsTitle { get; private set; } = string.Empty;
    public string BuiltInControlsControlHeader { get; private set; } = string.Empty;
    public string BuiltInControlsUsageHeader { get; private set; } = string.Empty;
    public string BuiltInControlsNotesHeader { get; private set; } = string.Empty;

    public FrontedDesignerHelpWindow()
    {
        InitializeComponent();
        LoadHelpContent();
        DataContext = this;
    }

    private void LoadHelpContent()
    {
        AddSection("Designer.Help.BasicWorkflow.Title", "Designer.Help.BasicWorkflow.Content");
        AddSection("Designer.Help.CanvasNavigation.Title", "Designer.Help.CanvasNavigation.Content");
        AddSection("Designer.Help.Selection.Title", "Designer.Help.Selection.Content");
        AddSection("Designer.Help.Snapping.Title", "Designer.Help.Snapping.Content");
        AddSection("Designer.Help.LayerPanel.Title", "Designer.Help.LayerPanel.Content");
        AddSection("Designer.Help.PropertyGrid.Title", "Designer.Help.PropertyGrid.Content");

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

        AddBuiltInControls();
        AddPostShortcutSection("Designer.Help.AddDeleteCopyPaste.Title", "Designer.Help.AddDeleteCopyPaste.Content");
        AddPostShortcutSection("Designer.Help.CanvasWindowSettings.Title", "Designer.Help.CanvasWindowSettings.Content");
        AddPostShortcutSection("Designer.Help.Validation.Title", "Designer.Help.Validation.Content");
        AddPostShortcutSection("Designer.Help.PluginControls.Title", "Designer.Help.PluginControls.Content");
        AddPostShortcutSection("Designer.Help.LayoutPackages.Title", "Designer.Help.LayoutPackages.Content");
        AddPostShortcutSection("Designer.Help.Troubleshooting.Title", "Designer.Help.Troubleshooting.Content");
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

    private void AddPostShortcutSection(string titleKey, string contentKey)
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

        PostShortcutSections.Add(section);
    }

    private void AddBuiltInControls()
    {
        BuiltInControlsTitle = I18nHelper.GetLocalizedString("Designer.Help.BuiltInControls.Title");
        BuiltInControlsControlHeader = I18nHelper.GetLocalizedString("Designer.Help.BuiltInControls.Column.Control");
        BuiltInControlsUsageHeader = I18nHelper.GetLocalizedString("Designer.Help.BuiltInControls.Column.Usage");
        BuiltInControlsNotesHeader = I18nHelper.GetLocalizedString("Designer.Help.BuiltInControls.Column.Notes");

        var content = I18nHelper.GetLocalizedString("Designer.Help.BuiltInControls.Content");
        foreach (var line in content.Split(new[] { "\\n" }, StringSplitOptions.None))
        {
            var parts = line.Split('|', 3);
            if (parts.Length != 3)
            {
                continue;
            }

            BuiltInControls.Add(new HelpBuiltInControlRow
            {
                ControlType = parts[0].Trim(),
                Usage = parts[1].Trim(),
                Notes = parts[2].Trim()
            });
        }
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
