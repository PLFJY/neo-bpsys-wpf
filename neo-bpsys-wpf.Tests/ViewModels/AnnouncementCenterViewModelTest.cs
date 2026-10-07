using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Models.RemoteAnnouncements;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Tests.Infrastructure;
using neo_bpsys_wpf.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.ViewModels;

/// <summary>公告中心启动提示行为测试。</summary>
public sealed class AnnouncementCenterViewModelTest
{
    /// <summary>远程检查结束后，新未读公告在 Shell 可交互时自动打开。</summary>
    [Fact]
    public void StartupRefreshWithUnseenAnnouncement_OpensAfterShellReady()
    {
        WpfTestThread.Run(() =>
        {
            var completed = false;
            var service = new Mock<IRemoteAnnouncementService>();
            service.SetupGet(value => value.Announcements).Returns(
                new[] { new RemoteAnnouncement
                {
                    Id = "20261007-001",
                    PublishedAt = DateTimeOffset.UtcNow,
                    Title = new Dictionary<string, string> { ["zh-CN"] = "公告" },
                    Content = new Dictionary<string, string> { ["zh-CN"] = "正文" }
                } });
            service.SetupGet(value => value.UnseenCount).Returns(1);
            service.SetupGet(value => value.SeenAnnouncementIds).Returns(Array.Empty<string>());
            service.SetupGet(value => value.IsStartupCheckCompleted).Returns(() => completed);
            service.Setup(value => value.RefreshOnDemandAsync(default)).Returns(Task.CompletedTask);
            var settings = new Mock<ISettingsHostService>();
            settings.SetupGet(value => value.Settings).Returns(new Settings());
            var center = new AnnouncementCenterViewModel(service.Object, settings.Object,
                NullLogger<AnnouncementCenterViewModel>.Instance);

            service.Raise(value => value.AnnouncementsChanged += null, EventArgs.Empty);
            Assert.False(center.IsOpen);

            completed = true;
            service.Raise(value => value.AnnouncementsChanged += null, EventArgs.Empty);
            Assert.False(center.IsOpen);

            center.NotifyShellReady();
            Assert.True(center.IsOpen);
            Assert.Single(center.Announcements);
        });
    }

    /// <summary>打开时展开最新和未读公告，稍后阅读保留未读状态，已读折叠并保存当前 ID。</summary>
    [Fact]
    public async Task OpeningAndReadActions_PreserveExpectedSeenAndExpandedStates()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var announcements = Enumerable.Range(1, 3).Select(index => new RemoteAnnouncement
            {
                Id = $"announcement-{index}",
                PublishedAt = DateTimeOffset.UtcNow.AddDays(-index),
                Title = new Dictionary<string, string> { ["zh-CN"] = $"公告 {index}" },
                Content = new Dictionary<string, string> { ["zh-CN"] = "正文" }
            }).ToArray();
            var seen = new HashSet<string> { "announcement-3" };
            var service = new Mock<IRemoteAnnouncementService>();
            service.SetupGet(value => value.Announcements).Returns(announcements);
            service.SetupGet(value => value.SeenAnnouncementIds).Returns(() => seen.ToArray());
            service.SetupGet(value => value.UnseenCount).Returns(() => announcements.Count(a => !seen.Contains(a.Id)));
            service.Setup(value => value.RefreshOnDemandAsync(default)).Returns(Task.CompletedTask);
            service.Setup(value => value.MarkAllCurrentAsSeenAsync(default)).Returns(() =>
            {
                seen.UnionWith(announcements.Select(a => a.Id));
                service.Raise(value => value.AnnouncementsChanged += null, EventArgs.Empty);
                return Task.CompletedTask;
            });
            var settings = new Mock<ISettingsHostService>();
            settings.SetupGet(value => value.Settings).Returns(new Settings());
            var center = new AnnouncementCenterViewModel(service.Object, settings.Object,
                NullLogger<AnnouncementCenterViewModel>.Instance);

            center.Open();
            Assert.Equal(new[] { true, true, false }, center.Announcements.Select(a => a.IsExpanded));
            Assert.Equal(new[] { true, true, false }, center.Announcements.Select(a => a.IsUnseen));
            service.Verify(value => value.RefreshOnDemandAsync(default), Times.Once);

            center.ReadLater();
            Assert.False(center.IsOpen);
            Assert.Equal(2, center.UnseenCount);
            service.Raise(value => value.AnnouncementsChanged += null, EventArgs.Empty);
            Assert.False(center.IsOpen);

            center.Open();
            service.Verify(value => value.RefreshOnDemandAsync(default), Times.Exactly(2));
            await center.MarkAsReadAndCloseAsync();
            Assert.False(center.IsOpen);
            Assert.Equal(0, center.UnseenCount);
            Assert.All(center.Announcements, item => Assert.False(item.IsExpanded));
            Assert.Equal(3, seen.Count);
        });
    }
}
