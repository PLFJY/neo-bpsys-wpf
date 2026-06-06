#nullable enable

using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

public class MapV2PresenterTest
{
    [Fact]
    public void MapV2DoesNotOwnBorderStyleAndStillNotifiesBannedDerivedProperties()
    {
        var constructors = typeof(MapV2).GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        var constructor = Assert.Single(constructors);
        Assert.Single(constructor.GetParameters());
        Assert.Null(typeof(MapV2).GetProperty("MapBorderBrush"));
        Assert.Null(typeof(MapV2).GetField("_mapBorderNormalBrush", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.Null(typeof(MapV2).GetField("_mapBorderBannedBrush", BindingFlags.Instance | BindingFlags.NonPublic));

        var map = new MapV2(Map.ArmsFactory);
        var changed = new List<string?>();
        map.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        map.IsBanned = true;

        Assert.Contains(nameof(MapV2.ImageSource), changed);
        Assert.Contains(nameof(MapV2.IsBreathing), changed);
        Assert.Contains(nameof(MapV2.CanBePicked), changed);
        Assert.Contains(nameof(MapV2.CanBeBanned), changed);
        Assert.DoesNotContain("MapBorderBrush", changed);
    }

    [Fact]
    public void PresenterDynamicallySelectsNormalAndBannedBorderBrushes()
    {
        RunOnStaThread(() =>
        {
            var map = new MapV2(Map.ArmsFactory);
            var presenter = new MapV2Presenter { Map = map };
            var border = Assert.IsType<Border>(presenter.FindName("MapBorder"));
            var normal = new SolidColorBrush(Colors.Blue);
            var banned = new SolidColorBrush(Colors.Red);

            presenter.MapBorderNormalBrush = normal;
            presenter.MapBorderBannedBrush = banned;
            Assert.Same(normal, border.BorderBrush);

            map.IsBanned = true;
            Assert.Same(banned, border.BorderBrush);

            var nextBanned = new SolidColorBrush(Colors.Orange);
            presenter.MapBorderBannedBrush = nextBanned;
            Assert.Same(nextBanned, border.BorderBrush);

            map.IsBanned = false;
            Assert.Same(normal, border.BorderBrush);

            var nextNormal = new SolidColorBrush(Colors.Green);
            presenter.MapBorderNormalBrush = nextNormal;
            Assert.Same(nextNormal, border.BorderBrush);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        exception?.Throw();
    }
}
