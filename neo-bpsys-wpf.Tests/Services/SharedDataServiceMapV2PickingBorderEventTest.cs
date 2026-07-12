using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class SharedDataServiceMapV2PickingBorderEventTest
{
    /// <summary>
    /// 验证直接修改当前对局进度会触发共享服务的对局进度改变事件。
    /// </summary>
    [Fact]
    public void CurrentGameProgressChange_PublishesGameProgressChanged()
    {
        var service = CreateSharedDataService();
        var eventCount = 0;
        service.GameProgressChanged += (_, _) => eventCount++;

        service.CurrentGame.GameProgress = GameProgress.Game1FirstHalf;

        Assert.Equal(1, eventCount);
    }

    /// <summary>
    /// 验证导入不同进度的对局并替换当前对局时会触发共享服务的对局进度改变事件。
    /// </summary>
    [Fact]
    public async Task ImportedGameWithDifferentProgress_PublishesGameProgressChanged()
    {
        var service = CreateSharedDataService();
        var importedGame = new Game(
            service.CurrentGame.SurTeam,
            service.CurrentGame.HunTeam,
            GameProgress.Game1SecondHalf,
            mapV2Dictionary: service.CurrentGame.MapV2Dictionary);
        var filePath = WriteGameJson(importedGame);
        var eventCount = 0;
        service.GameProgressChanged += (_, _) => eventCount++;

        try
        {
            await service.ImportGameAsync(filePath);
        }
        finally
        {
            File.Delete(filePath);
        }

        Assert.Equal(1, eventCount);
    }

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

    private static string WriteGameJson(Game game)
    {
        var path = Path.Combine(Path.GetTempPath(), $"neo-bpsys-game-{Guid.NewGuid():N}.json");
        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };
        File.WriteAllText(path, JsonSerializer.Serialize(game, options));
        return path;
    }
}
