using neo_bpsys_wpf.Helpers;
using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// 动画编辑器帮助窗口中的一个分组。
/// </summary>
public sealed class AnimationHelpSection
{
    /// <summary>分组标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>分组条目。</summary>
    public ObservableCollection<string> Items { get; } = [];
}

/// <summary>
/// 动画编辑器帮助窗口中的快捷键条目。
/// </summary>
public sealed class AnimationHelpShortcut
{
    /// <summary>快捷键文本。</summary>
    public string Shortcut { get; set; } = string.Empty;

    /// <summary>快捷键说明。</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 动画编辑器帮助窗口。
/// </summary>
public partial class FrontedBehaviorAnimationHelpWindow : FluentWindow
{
    /// <summary>帮助分组集合。</summary>
    public ObservableCollection<AnimationHelpSection> Sections { get; } = [];

    /// <summary>快捷键说明集合。</summary>
    public ObservableCollection<AnimationHelpShortcut> Shortcuts { get; } = [];

    /// <summary>
    /// 初始化帮助窗口。
    /// </summary>
    public FrontedBehaviorAnimationHelpWindow()
    {
        InitializeComponent();
        WindowBackdropThemeHelper.Attach(this);
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
        var title = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, titleKey);
        var content = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, contentKey);
        var section = new AnimationHelpSection { Title = title };

        var normalized = content
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal);
        foreach (var line in normalized.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                section.Items.Add($"• {trimmed}");
            }
            else if (section.Items.Count > 0 && section.Items[^1].Length > 0)
            {
                // 保留 resx 中的分段空行，避免帮助正文挤成一整段。
                section.Items.Add(string.Empty);
            }
        }

        Sections.Add(section);
    }

    private void AddShortcut(string key)
    {
        var text = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, key);
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
