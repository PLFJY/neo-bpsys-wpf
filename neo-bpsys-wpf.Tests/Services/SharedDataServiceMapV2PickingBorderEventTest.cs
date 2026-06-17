using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SharedDataServiceMapV2PickingBorderEventTest
{
    [Fact]
    public void IsMapV2Breathing_PublishesVisibleStateForUnbannedMaps()
    {
        var service = CreateSharedDataService();
        var mapKey = GetFirstMapKey(service);
        var events = CaptureEvents(service);

        service.IsMapV2Breathing = true;

        var mapEvent = Assert.Single(events, item => item.MapKey == mapKey);
        Assert.True(mapEvent.IsMapV2Breathing);
        Assert.False(mapEvent.IsMapBanned);
        Assert.True(mapEvent.IsPickingBorderVisible);
    }

    [Fact]
    public void MapBanState_PublishesHiddenAndRestoredPickingBorderStates()
    {
        var service = CreateSharedDataService();
        service.IsMapV2Breathing = true;
        var mapKey = GetFirstMapKey(service);
        var map = service.CurrentGame.MapV2Dictionary[mapKey];
        var events = CaptureEvents(service);

        map.IsBanned = true;
        map.IsBanned = false;

        Assert.Contains(events, item =>
            item.MapKey == mapKey
            && item.IsMapV2Breathing
            && item.IsMapBanned
            && !item.IsPickingBorderVisible);
        Assert.Contains(events, item =>
            item.MapKey == mapKey
            && item.IsMapV2Breathing
            && !item.IsMapBanned
            && item.IsPickingBorderVisible);
    }

    private static List<MapV2PickingBorderStateChangedEventArgs> CaptureEvents(SharedDataService service)
    {
        var events = new List<MapV2PickingBorderStateChangedEventArgs>();
        service.MapV2PickingBorderStateChanged += (_, args) => events.Add(args);
        return events;
    }

    private static string GetFirstMapKey(SharedDataService service) =>
        service.CurrentGame.MapV2Dictionary.Keys.First(key => !string.Equals(key, "NoBans", StringComparison.Ordinal));

    private static SharedDataService CreateSharedDataService()
    {
        var settingsHostService = new Mock<ISettingsHostService>();
        settingsHostService.SetupProperty(service => service.Settings, new Settings());
        return new SharedDataService(settingsHostService.Object, NullLogger<SharedDataService>.Instance);
    }
}
