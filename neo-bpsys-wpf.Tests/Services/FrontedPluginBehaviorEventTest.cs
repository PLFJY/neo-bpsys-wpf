using Microsoft.Extensions.DependencyInjection;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.ExamplePlugin;
using neo_bpsys_wpf.ExamplePlugin.ViewModels;
using neo_bpsys_wpf.WebRenderer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 插件行为事件注册、目录与发布协议测试。
/// </summary>
public sealed class FrontedPluginBehaviorEventTest
{
    [Fact]
    public void RegistrationBuildsCanonicalIdentityAndAllowsSameLocalIdAcrossPlugins()
    {
        var services = new ServiceCollection();
        using (FrontedPluginRegistrationContext.BeginScope("first.overlay"))
        {
            services.AddFrontedBehaviorEvents<PluginA>(events => events.Add("ShowCard"));
        }

        using (FrontedPluginRegistrationContext.BeginScope("second.overlay"))
        {
            services.AddFrontedBehaviorEvents<PluginB>(events => events.Add("ShowCard"));
        }

        var registrations = services
            .Where(item => item.ServiceType == typeof(FrontedBehaviorEventRegistration))
            .Select(item => Assert.IsType<FrontedBehaviorEventRegistration>(item.ImplementationInstance))
            .ToArray();

        Assert.Equal(
            ["plugin:first.overlay/ShowCard", "plugin:second.overlay/ShowCard"],
            registrations.Select(item => item.EventType).ToArray());
        Assert.All(registrations, item => Assert.False(item.IsBuiltIn));
    }

    [Fact]
    public void RegistrationRejectsDuplicateAndInvalidLocalIds()
    {
        using var scope = FrontedPluginRegistrationContext.BeginScope("example.overlay");
        Assert.Throws<FrontedLayoutConfigException>(() =>
            new ServiceCollection().AddFrontedBehaviorEvents<PluginA>(events =>
            {
                events.Add("ShowCard");
                events.Add("ShowCard");
            }));

        foreach (var invalid in new[] { "", " ", "plugin:other/Event", "a/b", "a\\b", "a..b" })
        {
            Assert.Throws<FrontedLayoutConfigException>(() =>
                new ServiceCollection().AddFrontedBehaviorEvents<PluginA>(events => events.Add(invalid)));
        }
    }

    [Fact]
    public void RegistrationRejectsInvalidPayloadSchema()
    {
        using var scope = FrontedPluginRegistrationContext.BeginScope("example.overlay");
        Assert.Throws<FrontedLayoutConfigException>(() =>
            new ServiceCollection().AddFrontedBehaviorEvents<PluginA>(events =>
                events.Add("Duplicate", definition => definition
                    .AddPayload<int>("PlayerIndex", "Player")
                    .AddPayload<string>("playerindex", "Player again"))));
        Assert.Throws<FrontedLayoutConfigException>(() =>
            new ServiceCollection().AddFrontedBehaviorEvents<PluginA>(events =>
                events.Add("Prefixed", definition => definition.AddPayload<int>("Event.PlayerIndex", "Player"))));
        Assert.Throws<FrontedLayoutConfigException>(() =>
            new ServiceCollection().AddFrontedBehaviorEvents<PluginA>(events =>
                events.Add("Complex", definition => definition.AddPayload<ComplexPayload>("Value", "Value"))));
    }

    [Fact]
    public void CatalogCombinesBuiltInsAndPluginMetadataWithoutStaticStaleness()
    {
        var registration = CreateRegistration();
        var builtInOnly = new FrontedBehaviorEventCatalog();
        var catalog = new FrontedBehaviorEventCatalog([registration]);
        var descriptor = Assert.IsType<FrontedBehaviorEventDescriptor>(
            catalog.Find("plugin:example.overlay/ShowCard"));

        Assert.NotNull(catalog.Find("Selection.CharacterPick"));
        Assert.Null(builtInOnly.Find(registration.EventType));
        Assert.Equal("Example", descriptor.Category);
        Assert.Equal("Show card", descriptor.DisplayName);
        Assert.Equal(FrontedBehaviorEventUsage.EventBus, descriptor.SupportedUsages);
        var camp = Assert.Single(descriptor.PayloadFields, field => field.Path == "Event.Camp");
        Assert.Contains("Sur", camp.EnumValues);
    }

    [Fact]
    public void CatalogRejectsDuplicateCanonicalEventTypes()
    {
        var registration = CreateRegistration();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new FrontedBehaviorEventCatalog([registration, registration]));

        Assert.Contains(registration.EventType, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PublisherValidatesSchemaNormalizesEnumAndPublishesToUnifiedBus()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFrontedEventBus, FrontedEventBus>();
        using (FrontedPluginRegistrationContext.BeginScope("example.overlay"))
        {
            services.AddFrontedBehaviorEvents<PluginA>(events =>
                events.Add("ShowCard", definition => definition
                    .AddPayload<int>("PlayerIndex", "Player index")
                    .AddPayload<Camp>("Camp", "Camp")));
        }

        using var provider = services.BuildServiceProvider();
        var eventBus = provider.GetRequiredService<IFrontedEventBus>();
        FrontedBehaviorEvent? published = null;
        eventBus.EventPublished += (_, value) => published = value;
        var publisher = provider.GetRequiredService<IFrontedBehaviorEventPublisher<PluginA>>();

        publisher.Publish(
            "ShowCard",
            new Dictionary<string, object?> { ["PlayerIndex"] = 2, ["Camp"] = Camp.Sur },
            "window-1",
            "BpWindow");

        Assert.NotNull(published);
        Assert.Equal("plugin:example.overlay/ShowCard", published.EventType);
        Assert.Equal("Plugin:example.overlay", published.Source);
        Assert.Equal("window-1", published.WindowId);
        Assert.Equal("BpWindow", published.WindowType);
        Assert.Equal(2, published.Payload["PlayerIndex"]);
        Assert.Equal("Sur", published.Payload["Camp"]);
        Assert.Throws<InvalidOperationException>(() => publisher.Publish("Typo"));
        Assert.Throws<InvalidOperationException>(() => publisher.Publish(
            "ShowCard",
            new Dictionary<string, object?> { ["PlayerIndex"] = "2", ["Camp"] = Camp.Sur }));
    }

    [Fact]
    public void PluginPublisherFlowsThroughEventBusToWebRendererWithoutPluginChannel()
    {
        using var eventBus = new FrontedEventBus();
        var services = new ServiceCollection();
        services.AddSingleton<IFrontedEventBus>(eventBus);
        using (FrontedPluginRegistrationContext.BeginScope("example.overlay"))
        {
            services.AddFrontedBehaviorEvents<PluginA>(events =>
                events.Add("ShowCard", definition => definition
                    .AddPayload<int>("PlayerIndex", "Player index")
                    .AddPayload<Camp>("Camp", "Camp")));
        }

        using var provider = services.BuildServiceProvider();
        var sharedData = new Mock<ISharedDataService>();
        sharedData.SetupGet(service => service.CurrentGame).Returns(new Game(
            new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam),
            GameProgress.Free));
        using var webPublisher = new WebRendererRuntimeStatePublisher(sharedData.Object, eventBus);
        WebBehaviorEventMessage? webEvent = null;
        webPublisher.BehaviorEventPublished += (_, value) => webEvent = value;
        webPublisher.SetClientCount(1);
        var pluginPublisher = provider.GetRequiredService<IFrontedBehaviorEventPublisher<PluginA>>();

        pluginPublisher.Publish(
            "ShowCard",
            new Dictionary<string, object?> { ["PlayerIndex"] = 2, ["Camp"] = Camp.Sur });

        Assert.NotNull(webEvent);
        Assert.Equal("plugin:example.overlay/ShowCard", webEvent.EventType);
        Assert.Equal("Plugin:example.overlay", webEvent.Source);
        Assert.Equal(2, webEvent.Payload["PlayerIndex"]);
        Assert.Equal("Sur", webEvent.Payload["Camp"]);
        Assert.Empty(webEvent.Diagnostics);
    }

    [Fact]
    public void ExamplePluginPagePublishesDemoSemanticEvents()
    {
        var publisher = new Mock<IFrontedBehaviorEventPublisher<ExamplePlugin.ExamplePlugin>>();
        var viewModel = new MainPageViewModel(publisher.Object);

        viewModel.Plus1Command.Execute(null);
        viewModel.StartPulseCommand.Execute(null);
        viewModel.StopPulseCommand.Execute(null);

        publisher.Verify(service => service.Publish(
            "CounterChanged",
            It.Is<IReadOnlyDictionary<string, object?>>(payload =>
                Equals(payload["CounterValue"], 1)
                && Equals(payload["Delta"], 1)),
            null,
            null), Times.Once);
        publisher.Verify(service => service.Publish("StartPulse", null, null, null), Times.Once);
        publisher.Verify(service => service.Publish("StopPulse", null, null, null), Times.Once);
        Assert.Equal(1, viewModel.Counter);
        Assert.Equal("Published StopPulse.", viewModel.LastPublishedEvent);
    }

    private static FrontedBehaviorEventRegistration CreateRegistration()
    {
        var services = new ServiceCollection();
        using (FrontedPluginRegistrationContext.BeginScope("example.overlay"))
        {
            services.AddFrontedBehaviorEvents<PluginA>(events =>
                events.Add("ShowCard", definition => definition
                    .WithDisplayName("Show card")
                    .WithDescription("Show a player card")
                    .WithCategory("Example")
                    .AddPayload<int>("PlayerIndex", "Player index")
                    .AddPayload<Camp>("Camp", "Camp")));
        }

        return services
            .Where(item => item.ServiceType == typeof(FrontedBehaviorEventRegistration))
            .Select(item => Assert.IsType<FrontedBehaviorEventRegistration>(item.ImplementationInstance))
            .Single();
    }

    private sealed class PluginA;
    private sealed class PluginB;
    private sealed class ComplexPayload;
}
