using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

public sealed class AnimationHelpSection
{
    public string Title { get; set; } = string.Empty;
    public ObservableCollection<string> Items { get; } = [];
}

public sealed class AnimationHelpShortcut
{
    public string Shortcut { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public partial class FrontedBehaviorAnimationHelpWindow : FluentWindow
{
    public ObservableCollection<AnimationHelpSection> Sections { get; } = [];
    public ObservableCollection<AnimationHelpShortcut> Shortcuts { get; } = [];

    public FrontedBehaviorAnimationHelpWindow()
    {
        InitializeComponent();
        LoadHelpContent();
        DataContext = this;
    }

    private void LoadHelpContent()
    {
        AddSection("Designer.Graph.Help.Concepts.Title", "Designer.Graph.Help.Concepts.Content");
        AddSection("Designer.Graph.Help.Workflow.Title", "Designer.Graph.Help.Workflow.Content");
        AddSection("Designer.Graph.Help.NodeTypes.Title", "Designer.Graph.Help.NodeTypes.Content");
        AddSection("Designer.Graph.Help.Preview.Title", "Designer.Graph.Help.Preview.Content");
        AddSection("Designer.Graph.Help.LoopPreview.Title", "Designer.Graph.Help.LoopPreview.Content");
        AddSection("Designer.Graph.Help.Troubleshooting.Title", "Designer.Graph.Help.Troubleshooting.Content");

        AddShortcut("Designer.Graph.Help.Shortcut.Delete");
        AddShortcut("Designer.Graph.Help.Shortcut.Duplicate");
        AddShortcut("Designer.Graph.Help.Shortcut.Connect");
    }

    private void AddSection(string titleKey, string contentKey)
    {
        var title = I18nHelper.GetLocalizedString(titleKey);
        var content = I18nHelper.GetLocalizedString(contentKey);
        var section = new AnimationHelpSection { Title = title };

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
            Shortcuts.Add(new AnimationHelpShortcut
            {
                Shortcut = text[..colonIndex].Trim(),
                Description = text[(colonIndex + 1)..].Trim()
            });
        }
        else
        {
            Shortcuts.Add(new AnimationHelpShortcut
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
