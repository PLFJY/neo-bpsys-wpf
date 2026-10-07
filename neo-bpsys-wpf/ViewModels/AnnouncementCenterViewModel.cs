using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.RemoteAnnouncements;
using neo_bpsys_wpf.Services.Abstractions;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels;

/// <summary>公告中心的共享界面状态。</summary>
public partial class AnnouncementCenterViewModel : ObservableObject
{
    private readonly IRemoteAnnouncementService _service;
    private readonly ISettingsHostService _settings;
    private readonly ILogger<AnnouncementCenterViewModel> _logger;
    private bool _shellReady;
    private bool _pendingAutoOpen;
    private bool _startupAutoOpenEvaluated;

    /// <summary>创建共享公告中心。</summary>
    /// <param name="service">公告缓存服务。</param>
    /// <param name="settings">语言设置服务。</param>
    /// <param name="logger">日志记录器。</param>
    public AnnouncementCenterViewModel(IRemoteAnnouncementService service,
        ISettingsHostService settings, ILogger<AnnouncementCenterViewModel> logger)
    {
        _service = service;
        _settings = settings;
        _logger = logger;
        _service.AnnouncementsChanged += OnAnnouncementsChanged;
        _settings.LanguageSettingChanged += (_, _) => Dispatch(Rebuild);
        Rebuild();
    }

    /// <summary>按发布时间倒序排列的本地公告。</summary>
    public ObservableCollection<AnnouncementDisplayItem> Announcements { get; } = [];

    /// <summary>公告层是否显示。</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    partial void OnIsOpenChanged(bool isOpen)
    {
        if (!isOpen) return;

        ExpandLatestAndUnseen();
        _ = RefreshAnnouncementsAsync();
    }

    /// <summary>未读公告数量。</summary>
    [ObservableProperty]
    public partial int UnseenCount { get; set; }

    /// <summary>是否有可显示公告。</summary>
    public bool HasAnnouncements => Announcements.Count > 0;

    /// <summary>是否存在尚未阅读的公告。</summary>
    public bool HasUnseenAnnouncements => UnseenCount > 0;

    /// <summary>当前 Shell 已完成启动遮罩动画，可以显示自动公告。</summary>
    public void NotifyShellReady()
    {
        _shellReady = true;
        if (_pendingAutoOpen && UnseenCount > 0)
        {
            _pendingAutoOpen = false;
            IsOpen = true;
        }
    }

    /// <summary>打开公告中心，并在打开时发起一次远程刷新。</summary>
    [RelayCommand]
    public void Open() => IsOpen = true;

    /// <summary>按用户操作重新同步远程公告；失败时保持当前本地列表。</summary>
    /// <returns>刷新任务。</returns>
    [RelayCommand]
    public async Task RefreshAnnouncementsAsync()
    {
        try
        {
            await _service.RefreshOnDemandAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual remote announcement refresh failed.");
        }
    }

    /// <summary>持久化当前公告的已读 ID 后关闭公告层。</summary>
    /// <returns>保存任务。</returns>
    [RelayCommand]
    public async Task MarkAsReadAndCloseAsync()
    {
        try
        {
            await _service.MarkAllCurrentAsSeenAsync();
            foreach (var announcement in Announcements)
                announcement.IsExpanded = false;
            IsOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save remote announcement seen state.");
        }
    }

    /// <summary>保留未读状态并关闭公告中心。</summary>
    [RelayCommand]
    public void ReadLater()
    {
        _startupAutoOpenEvaluated = true;
        _pendingAutoOpen = false;
        IsOpen = false;
    }

    private void OnAnnouncementsChanged(object? sender, EventArgs e) => Dispatch(() =>
    {
        Rebuild();
        if (_startupAutoOpenEvaluated || !_service.IsStartupCheckCompleted) return;
        _startupAutoOpenEvaluated = true;
        if (UnseenCount <= 0) return;
        if (_shellReady) IsOpen = true;
        else _pendingAutoOpen = true;
    });

    private void Rebuild()
    {
        var culture = _settings.Settings.CultureInfo;
        var expandedById = Announcements.ToDictionary(item => item.Id, item => item.IsExpanded, StringComparer.Ordinal);
        var seenIds = _service.SeenAnnouncementIds.ToHashSet(StringComparer.Ordinal);
        Announcements.Clear();
        foreach (var announcement in _service.Announcements)
        {
            Announcements.Add(new AnnouncementDisplayItem(
                announcement.Id,
                announcement.PublishedAt.ToLocalTime().ToString("yyyy/MM/dd", culture),
                Localize(announcement.Title, culture),
                Localize(announcement.Content, culture),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "AnnouncementLevel" + announcement.Level),
                announcement.Level,
                announcement.Level switch
                {
                    RemoteAnnouncementLevel.Warning => Brushes.Goldenrod,
                    RemoteAnnouncementLevel.Critical => Brushes.IndianRed,
                    _ => Brushes.DodgerBlue
                },
                !seenIds.Contains(announcement.Id),
                IsOpen && (expandedById.TryGetValue(announcement.Id, out var wasExpanded)
                    ? wasExpanded
                    : !seenIds.Contains(announcement.Id))));
        }
        UnseenCount = _service.UnseenCount;
        OnPropertyChanged(nameof(HasAnnouncements));
        OnPropertyChanged(nameof(HasUnseenAnnouncements));
    }

    private void ExpandLatestAndUnseen()
    {
        for (var index = 0; index < Announcements.Count; index++)
            Announcements[index].IsExpanded = index == 0 || Announcements[index].IsUnseen;
    }

    private static string Localize(IReadOnlyDictionary<string, string> values, CultureInfo culture)
    {
        foreach (var key in new[] { culture.Name, "zh-CN", "en-US" })
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }
        return values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}

/// <summary>按当前语言准备的公告展示内容。</summary>
public sealed partial class AnnouncementDisplayItem : ObservableObject
{
    /// <summary>创建公告展示项。</summary>
    /// <param name="id">稳定公告 ID。</param>
    /// <param name="publishedDate">本地显示日期。</param>
    /// <param name="title">标题。</param>
    /// <param name="content">Markdown 正文。</param>
    /// <param name="levelLabel">本地化级别文本。</param>
    /// <param name="level">公告级别。</param>
    /// <param name="levelBrush">级别提示色。</param>
    /// <param name="isUnseen">公告是否未读。</param>
    /// <param name="isExpanded">公告是否展开。</param>
    public AnnouncementDisplayItem(string id, string publishedDate, string title, string content,
        string levelLabel, RemoteAnnouncementLevel level, Brush levelBrush, bool isUnseen, bool isExpanded)
    {
        Id = id;
        PublishedDate = publishedDate;
        Title = title;
        Content = content;
        LevelLabel = levelLabel;
        Level = level;
        LevelBrush = levelBrush;
        IsUnseen = isUnseen;
        IsExpanded = isExpanded;
    }

    /// <summary>稳定公告 ID。</summary>
    public string Id { get; }

    /// <summary>本地显示日期。</summary>
    public string PublishedDate { get; }

    /// <summary>公告标题。</summary>
    public string Title { get; }

    /// <summary>Markdown 正文。</summary>
    public string Content { get; }

    /// <summary>本地化级别文本。</summary>
    public string LevelLabel { get; }

    /// <summary>公告级别。</summary>
    public RemoteAnnouncementLevel Level { get; }

    /// <summary>级别提示色。</summary>
    public Brush LevelBrush { get; }

    /// <summary>公告是否未读。</summary>
    public bool IsUnseen { get; }

    /// <summary>公告是否展开。</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }
}
