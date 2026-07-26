using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

/// <summary>在线队伍图片的模型通知测试。</summary>
public sealed class RemoteImageModelNotificationTest
{
    /// <summary>Member.ImageUri 改变必须使 Player 的定妆照派生属性失效。</summary>
    [Fact]
    public void MemberImageUriInvalidatesPlayerPictureShown()
    {
        WpfTestThread.Run(() =>
        {
            var member = new Member(Camp.Sur);
            var player = new Player(member);
            var memberChanges = new List<string?>();
            var playerChanges = new List<string?>();
            member.PropertyChanged += (_, args) => memberChanges.Add(args.PropertyName);
            player.PropertyChanged += (_, args) => playerChanges.Add(args.PropertyName);

            member.ImageUri = "https://images.example.test/player-v1.png";

            Assert.Contains(nameof(Member.ImageUri), memberChanges);
            Assert.Contains(nameof(Member.Image), memberChanges);
            Assert.Contains(nameof(Player.PictureShown), playerChanges);
            var image = Assert.IsType<BitmapImage>(player.PictureShown);
            Assert.Equal(member.ImageUri, image.UriSource.AbsoluteUri);

            playerChanges.Clear();
            member.ImageUri = "https://images.example.test/player-v2.png";
            Assert.Contains(nameof(Player.PictureShown), playerChanges);
            Assert.Equal(member.ImageUri, Assert.IsType<BitmapImage>(player.PictureShown).UriSource.AbsoluteUri);
        });
    }

    /// <summary>角色选择和清除必须在角色半身图与当前在线定妆照之间切换。</summary>
    [Fact]
    public void ClearingCharacterRestoresCurrentRemoteMemberImage()
    {
        WpfTestThread.Run(() =>
        {
            var member = new Member(Camp.Sur) { ImageUri = "https://images.example.test/player.png" };
            var player = new Player(member);
            var memberImage = player.PictureShown;
            var character = new Character("幸运儿", Camp.Sur, "幸运儿.png");

            player.Character = character;
            Assert.Same(character.HalfImage, player.PictureShown);

            player.Character = null;
            Assert.Same(memberImage, player.PictureShown);
        });
    }

    /// <summary>Team.ImageUri 改变必须清除旧 Logo 缓存并通知 Logo 绑定。</summary>
    [Fact]
    public void TeamImageUriInvalidatesLogo()
    {
        WpfTestThread.Run(() =>
        {
            var team = new Team(Camp.Sur, TeamType.HomeTeam);
            var changes = new List<string?>();
            team.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

            team.ImageUri = "https://images.example.test/logo.png";

            Assert.Contains(nameof(Team.ImageUri), changes);
            Assert.Contains(nameof(Team.Logo), changes);
            Assert.Equal(team.ImageUri, Assert.IsType<BitmapImage>(team.Logo).UriSource.AbsoluteUri);
        });
    }

    /// <summary>导入空 Logo URI 必须覆盖此前本地上传的 Logo。</summary>
    [Fact]
    public void TeamImportWithEmptyImageUriClearsLocalLogo()
    {
        WpfTestThread.Run(() =>
        {
            var target = new Team(Camp.Sur, TeamType.HomeTeam);
            var localLogo = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
            localLogo.Freeze();
            target.Logo = localLogo;
            var imported = new Team(Camp.Sur, TeamType.HomeTeam) { ImageUri = string.Empty };

            target.ImportTeamInfo(imported);

            Assert.Equal(string.Empty, target.ImageUri);
            Assert.Null(target.Logo);
        });
    }
}
