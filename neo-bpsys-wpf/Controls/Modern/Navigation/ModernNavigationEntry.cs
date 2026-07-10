#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using neo_bpsys_wpf.Helpers;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Controls.Modern.Navigation;

/// <summary>
/// 表示 <see cref="ModernNavigationView"/> 中的一个导航条目。
/// </summary>
public sealed class ModernNavigationEntry : INotifyPropertyChanged
{
    private string _displayText = string.Empty;
    private bool _isSelected;

    /// <summary>
    /// 初始化 <see cref="ModernNavigationEntry"/> 的新实例。
    /// </summary>
    /// <param name="sourceItem">源项对象。</param>
    /// <param name="content">显示内容。</param>
    /// <param name="icon">图标。</param>
    /// <param name="targetPageType">目标页面类型。</param>
    /// <param name="targetPageTag">目标页面标签。</param>
    /// <param name="isFooter">是否为底部菜单项。</param>
    /// <param name="isEnabled">是否启用。</param>
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

    /// <summary>
    /// 获取源项对象。
    /// </summary>
    public object SourceItem { get; }

    /// <summary>
    /// 获取显示内容。
    /// </summary>
    public object? Content { get; }

    /// <summary>
    /// 获取本地化键。
    /// </summary>
    public string? LocalizationKey { get; private set; }

    /// <summary>
    /// 获取显示文本（本地化后的文本）。
    /// </summary>
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

    /// <summary>
    /// 获取图标。
    /// </summary>
    public object? Icon { get; }

    /// <summary>
    /// 获取目标页面类型。
    /// </summary>
    public Type? TargetPageType { get; }

    /// <summary>
    /// 获取目标页面标签。
    /// </summary>
    public string? TargetPageTag { get; }

    /// <summary>
    /// 获取一个值，指示是否为底部菜单项。
    /// </summary>
    public bool IsFooter { get; }

    /// <summary>
    /// 获取一个值，指示是否启用。
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// 获取源导航视图项。
    /// </summary>
    public NavigationViewItem? SourceNavigationViewItem { get; }

    /// <summary>
    /// 获取或设置一个值，指示是否已选中。
    /// </summary>
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

    /// <summary>
    /// 当属性值更改时发生。
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 刷新显示文本，根据本地化键更新。
    /// </summary>
    public void RefreshDisplayText()
    {
        if (Content is string key)
        {
            LocalizationKey = key;
            var localized = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, key);
            DisplayText = string.IsNullOrWhiteSpace(localized) ? key : localized;
            return;
        }

        LocalizationKey = null;
        DisplayText = Content?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 从源项创建 <see cref="ModernNavigationEntry"/>。
    /// </summary>
    /// <param name="sourceItem">源项对象。</param>
    /// <param name="isFooter">是否为底部菜单项。</param>
    /// <returns>创建的导航条目。</returns>
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
