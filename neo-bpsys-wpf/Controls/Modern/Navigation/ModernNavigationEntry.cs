#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using neo_bpsys_wpf.Helpers;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Controls.Modern.Navigation;

public sealed class ModernNavigationEntry : INotifyPropertyChanged
{
    private string _displayText = string.Empty;
    private bool _isSelected;

    public ModernNavigationEntry(
        object sourceItem,
        object? content,
        object? icon,
        Type? targetPageType,
        string? targetPageTag,
        bool isFooter,
        bool isEnabled)
    {
        SourceItem = sourceItem;
        Content = content;
        Icon = icon;
        TargetPageType = targetPageType;
        TargetPageTag = targetPageTag;
        IsFooter = isFooter;
        IsEnabled = isEnabled;
        SourceNavigationViewItem = sourceItem as NavigationViewItem;
        RefreshDisplayText();
    }

    public object SourceItem { get; }

    public object? Content { get; }

    public string? LocalizationKey { get; private set; }

    public string DisplayText
    {
        get => _displayText;
        private set
        {
            if (_displayText == value)
            {
                return;
            }

            _displayText = value;
            OnPropertyChanged();
        }
    }

    public object? Icon { get; }

    public Type? TargetPageType { get; }

    public string? TargetPageTag { get; }

    public bool IsFooter { get; }

    public bool IsEnabled { get; }

    public NavigationViewItem? SourceNavigationViewItem { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshDisplayText()
    {
        if (Content is string key)
        {
            LocalizationKey = key;
            DisplayText = I18nHelper.GetLocalizedString(key);
            return;
        }

        LocalizationKey = null;
        DisplayText = Content?.ToString() ?? string.Empty;
    }

    public static ModernNavigationEntry FromSource(object sourceItem, bool isFooter)
    {
        if (sourceItem is ModernNavigationEntry entry)
        {
            entry.RefreshDisplayText();
            return entry;
        }

        if (sourceItem is NavigationViewItem navigationViewItem)
        {
            return new ModernNavigationEntry(
                navigationViewItem,
                navigationViewItem.Content,
                navigationViewItem.Icon,
                navigationViewItem.TargetPageType,
                GetTargetTag(navigationViewItem),
                isFooter,
                navigationViewItem.IsEnabled);
        }

        if (sourceItem is Type pageType)
        {
            return new ModernNavigationEntry(
                sourceItem,
                pageType.Name,
                null,
                pageType,
                pageType.FullName,
                isFooter,
                true);
        }

        return new ModernNavigationEntry(
            sourceItem,
            sourceItem,
            null,
            null,
            (sourceItem as FrameworkElement)?.Tag?.ToString(),
            isFooter,
            sourceItem is not UIElement element || element.IsEnabled);
    }

    private static string? GetTargetTag(NavigationViewItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.TargetPageTag))
        {
            return item.TargetPageTag;
        }

        if (item.Tag is not null)
        {
            return item.Tag.ToString();
        }

        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            return item.Id;
        }

        return item.Content?.ToString();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
