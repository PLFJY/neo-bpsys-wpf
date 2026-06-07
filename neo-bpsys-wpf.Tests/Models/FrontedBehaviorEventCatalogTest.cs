#nullable enable

using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.ComponentModel;
using System.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedBehaviorEventCatalogTest
{
    [Fact]
    public void EventCatalog_OnlyAttributedSharedDataEventsIncluded()
    {
        var catalog = new FrontedBehaviorEventCatalog();
        var attributedNames = typeof(ISharedDataService).GetEvents()
            .Where(info => info.GetCustomAttributes(typeof(FrontedBehaviorEventAttribute), false).Length > 0)
            .Select(info => info.Name)
            .ToArray();

        Assert.Equal(attributedNames.Length, catalog.Events.Count);
        Assert.DoesNotContain(catalog.Events, descriptor => descriptor.EventType == nameof(INotifyPropertyChanged.PropertyChanged));
    }

    [Fact]
    public void EventCatalog_CountDownValueChanged_HasRemainingSecondsPayload()
    {
        var descriptor = new FrontedBehaviorEventCatalog().Find("SharedData.CountDownValueChanged");
        Assert.NotNull(descriptor);
        var field = Assert.Single(descriptor.PayloadFields);

        Assert.Equal("Event.RemainingSeconds", field.Path);
        Assert.False(string.IsNullOrWhiteSpace(field.DisplayNameKey));
        Assert.Equal(FrontedBehaviorPayloadSource.ServiceProperty, field.Source);
        Assert.Equal(nameof(ISharedDataService.RemainingSeconds), field.SourcePath);
    }

    [Fact]
    public void EventCatalog_BanCountChanged_HasEventArgsPayload()
    {
        var descriptor = new FrontedBehaviorEventCatalog().Find("SharedData.BanCountChanged");
        Assert.NotNull(descriptor);

        Assert.Contains(descriptor.PayloadFields, field => field.Path == "Event.ListName" && field.Source == FrontedBehaviorPayloadSource.EventArgsProperty);
        Assert.Contains(descriptor.PayloadFields, field => field.Path == "Event.Count" && field.Source == FrontedBehaviorPayloadSource.EventArgsProperty);
    }

    [Fact]
    public void EventCatalog_IsCachedAndDeterministic()
    {
        var first = new FrontedBehaviorEventCatalog().Events;
        var second = new FrontedBehaviorEventCatalog().Events;

        Assert.Same(first, second);
        Assert.Equal(first.Select(item => item.EventType), second.Select(item => item.EventType));
    }
}
