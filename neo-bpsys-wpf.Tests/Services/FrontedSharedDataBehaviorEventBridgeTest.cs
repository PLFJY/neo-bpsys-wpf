#nullable enable

using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedSharedDataBehaviorEventBridgeTest
{
    [Fact]
    public async Task SharedDataBridge_PublishesAttributedEvent()
    {
        using var semaphore = new SemaphoreSlim(0, 1);
        FrontedBehaviorEvent? received = null;
        var service = new MockSharedDataService();
        var bus = new MockEventBus();

        using (bus.Subscribe(null, ev =>
        {
            received = ev;
            semaphore.Release();
            return Task.CompletedTask;
        }))
        {
            using var bridge = new FrontedSharedDataBehaviorEventBridge(service, bus, NullLogger<FrontedSharedDataBehaviorEventBridge>.Instance);
            bridge.Start();

            service.FireCountDownValueChanged();

            Assert.True(await semaphore.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        Assert.NotNull(received);
        Assert.Equal("SharedData.CountDownValueChanged", received!.EventType);
        Assert.Equal("SharedDataService", received.Source);
        Assert.False(received.IsPreview);
    }

    [Fact]
    public async Task SharedDataBridge_ServicePropertyPayload_ReadsValue()
    {
        using var semaphore = new SemaphoreSlim(0, 1);
        FrontedBehaviorEvent? received = null;
        var service = new MockSharedDataService { RemainingSeconds = "42" };
        var bus = new MockEventBus();

        using (bus.Subscribe(null, ev =>
        {
            received = ev;
            semaphore.Release();
            return Task.CompletedTask;
        }))
        {
            using var bridge = new FrontedSharedDataBehaviorEventBridge(service, bus, NullLogger<FrontedSharedDataBehaviorEventBridge>.Instance);
            bridge.Start();

            service.FireCountDownValueChanged();

            Assert.True(await semaphore.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        Assert.NotNull(received);
        Assert.True(received!.Payload.TryGetValue("RemainingSeconds", out var value));
        Assert.Equal("42", value);
    }

    [Fact]
    public async Task SharedDataBridge_EventArgsPayload_ReadsValue()
    {
        using var semaphore = new SemaphoreSlim(0, 1);
        FrontedBehaviorEvent? received = null;
        var service = new MockSharedDataService();
        var bus = new MockEventBus();

        using (bus.Subscribe(null, ev =>
        {
            received = ev;
            semaphore.Release();
            return Task.CompletedTask;
        }))
        {
            using var bridge = new FrontedSharedDataBehaviorEventBridge(service, bus, NullLogger<FrontedSharedDataBehaviorEventBridge>.Instance);
            bridge.Start();

            var args = new BanCountChangedEventArgs(BanListName.CanCurrentSurBanned, 2);
            service.FireBanCountChanged(args);

            Assert.True(await semaphore.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        Assert.NotNull(received);
        Assert.Equal("SharedData.BanCountChanged", received!.EventType);

        Assert.True(received.Payload.TryGetValue("ListName", out var listName));
        Assert.Equal(BanListName.CanCurrentSurBanned, listName);

        Assert.True(received.Payload.TryGetValue("Count", out var count));
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task SharedDataBridge_MapV2PickingBorderStatePayload_ReadsValues()
    {
        using var semaphore = new SemaphoreSlim(0, 1);
        FrontedBehaviorEvent? received = null;
        var service = new MockSharedDataService();
        var bus = new MockEventBus();

        using (bus.Subscribe("MapV2.PickingBorderStateChanged", ev =>
        {
            received = ev;
            semaphore.Release();
            return Task.CompletedTask;
        }))
        {
            using var bridge = new FrontedSharedDataBehaviorEventBridge(service, bus, NullLogger<FrontedSharedDataBehaviorEventBridge>.Instance);
            bridge.Start();

            service.FireMapV2PickingBorderStateChanged(new MapV2PickingBorderStateChangedEventArgs("ArmsFactory", true, false));

            Assert.True(await semaphore.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        Assert.NotNull(received);
        Assert.Equal("MapV2.PickingBorderStateChanged", received!.EventType);
        Assert.Equal("ArmsFactory", received.Payload["MapKey"]);
        Assert.Equal(true, received.Payload["IsMapV2Breathing"]);
        Assert.Equal(false, received.Payload["IsMapBanned"]);
        Assert.Equal(true, received.Payload["IsPickingBorderVisible"]);
    }

    [Fact]
    public async Task SharedDataBridge_UnmarkedEvents_NotPublished()
    {
        var service = new MockSharedDataService();
        var bus = new MockEventBus();
        var publishedCount = 0;

        using (bus.Subscribe(null, ev =>
        {
            Interlocked.Increment(ref publishedCount);
            return Task.CompletedTask;
        }))
        {
            using var bridge = new FrontedSharedDataBehaviorEventBridge(service, bus, NullLogger<FrontedSharedDataBehaviorEventBridge>.Instance);
            bridge.Start();

            service.FirePropertyChanged(nameof(MockSharedDataService.RemainingSeconds));

            // Give async handlers a moment to invoke
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, publishedCount);
    }

    [Fact]
    public async Task SharedDataBridge_Dispose_Unsubscribes()
    {
        var service = new MockSharedDataService();
        var bus = new MockEventBus();
        var publishedCount = 0;

        using (bus.Subscribe(null, ev =>
        {
            Interlocked.Increment(ref publishedCount);
            return Task.CompletedTask;
        }))
        {
            var bridge = new FrontedSharedDataBehaviorEventBridge(service, bus, NullLogger<FrontedSharedDataBehaviorEventBridge>.Instance);
            bridge.Start();

            bridge.Dispose();

            service.FireCountDownValueChanged();

            // Give async handlers a moment to invoke
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, publishedCount);
    }

    [Fact]
    public async Task CharacterSelectionBridge_PublishesCharacterSelected()
    {
        using var semaphore = new SemaphoreSlim(0, 1);
        FrontedBehaviorEvent? received = null;
        var sharedData = new MockSharedDataService();
        var selection = new MockCharacterSelectionService();
        var bus = new MockEventBus();

        using (bus.Subscribe(null, ev =>
        {
            received = ev;
            semaphore.Release();
            return Task.CompletedTask;
        }))
        {
            using var bridge = new FrontedSharedDataBehaviorEventBridge(
                sharedData,
                bus,
                NullLogger<FrontedSharedDataBehaviorEventBridge>.Instance,
                characterSelectionService: selection);
            bridge.Start();

            selection.FireCharacterSelected(new CharacterSelectedEventArgs(Camp.Sur, 0));

            Assert.True(await semaphore.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        Assert.NotNull(received);
        Assert.Equal("Selection.CharacterSelected", received!.EventType);
        Assert.Equal("CharacterSelectionService", received.Source);
        Assert.Equal(Camp.Sur, received.Payload["Camp"]);
        Assert.Equal(0, received.Payload["PlayerIndex"]);
    }

    [Fact]
    public async Task GameGuidanceBridge_PublishesPreviousStepPayloads()
    {
        using var semaphore = new SemaphoreSlim(0, 1);
        FrontedBehaviorEvent? received = null;
        var sharedData = new MockSharedDataService();
        var guidance = new MockGameGuidanceService();
        var bus = new MockEventBus();

        using (bus.Subscribe("Guidance.StepChanged", ev =>
        {
            received = ev;
            semaphore.Release();
            return Task.CompletedTask;
        }))
        {
            using var bridge = new FrontedSharedDataBehaviorEventBridge(
                sharedData,
                bus,
                NullLogger<FrontedSharedDataBehaviorEventBridge>.Instance,
                gameGuidanceService: guidance);
            bridge.Start();

            guidance.FireStepChanged(new GameGuidanceStepChangedEventArgs(
                stepIndex: 1,
                action: GameAction.PickHun,
                index: [0],
                time: 30,
                previousStepIndex: 0,
                previousAction: GameAction.PickSur,
                previousIndex: [1],
                previousTime: 40));

            Assert.True(await semaphore.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        Assert.NotNull(received);
        Assert.Equal("[0]", received!.Payload["IndexesText"]);
        Assert.Equal(GameAction.PickHun, received.Payload["Action"]);
        Assert.Equal(GameAction.PickSur, received.Payload["PreviousAction"]);
        Assert.Equal("[1]", received.Payload["PreviousIndexesText"]);
        Assert.Equal(1, received.Payload["PreviousIndex"]);
        Assert.False(received.Payload.ContainsKey("ActionName"));
        Assert.False(received.Payload.ContainsKey("PreviousActionName"));
    }

    [Fact]
    public async Task GameGuidanceBridge_PublishesCancelledPayloads()
    {
        using var semaphore = new SemaphoreSlim(0, 1);
        FrontedBehaviorEvent? received = null;
        var sharedData = new MockSharedDataService();
        var guidance = new MockGameGuidanceService();
        var bus = new MockEventBus();

        using (bus.Subscribe("Guidance.Cancelled", ev =>
        {
            received = ev;
            semaphore.Release();
            return Task.CompletedTask;
        }))
        {
            using var bridge = new FrontedSharedDataBehaviorEventBridge(
                sharedData,
                bus,
                NullLogger<FrontedSharedDataBehaviorEventBridge>.Instance,
                gameGuidanceService: guidance);
            bridge.Start();

            guidance.FireCancelled(new GameGuidanceStateChangedEventArgs(
                false,
                "Cancelled",
                30,
                2,
                GameAction.PickHun,
                [0]));

            Assert.True(await semaphore.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        Assert.NotNull(received);
        Assert.Equal("Cancelled", received!.Payload["Reason"]);
        Assert.Equal(GameAction.PickHun, received.Payload["PreviousAction"]);
        Assert.Equal("[0]", received.Payload["PreviousIndexesText"]);
    }

    /// <summary>
    /// Test event args for the CharacterPicked event on <see cref="MockSharedDataService"/>.
    /// </summary>
    public sealed class CharacterPickedEventArgs(string characterName) : EventArgs
    {
        /// <summary>
        /// Gets the picked character name.
        /// </summary>
        public string CharacterName { get; } = characterName;
    }

    /// <summary>
    /// Mock implementation of <see cref="ISharedDataService"/> for testing the behavior event bridge.
    /// Includes attributed test events (CharacterPicked) and unmarked events (UnmarkedEvent)
    /// to verify bridge subscription filtering.
    /// </summary>
    private sealed class MockSharedDataService : ISharedDataService
    {
        /// <summary>
        /// Attributed test event — should be bridged.
        /// </summary>
        [FrontedBehaviorEvent("Test.CharacterPicked")]
        [FrontedBehaviorEventPayload("Event.CharacterName", Source = FrontedBehaviorPayloadSource.EventArgsProperty, SourcePath = nameof(CharacterPickedEventArgs.CharacterName))]
        public event EventHandler<CharacterPickedEventArgs>? CharacterPicked;

        /// <summary>
        /// Unmarked test event — should NOT be bridged.
        /// </summary>
        public event EventHandler<EventArgs>? UnmarkedEvent;

        // ISharedDataService events

        public event EventHandler? CurrentGameChanged;
        public event EventHandler<BanCountChangedEventArgs>? BanCountChanged;
        public event EventHandler? IsTraitVisibleChanged;
        public event EventHandler? IsBo3ModeChanged;
        public event EventHandler? CountDownValueChanged;
        public event EventHandler? TeamSwapped;
        public event EventHandler? IsMapV2BreathingChanged;
        public event EventHandler<MapV2PickingBorderStateChangedEventArgs>? MapV2PickingBorderStateChanged;
        public event EventHandler? IsMapV2CampVisibleChanged;
        public event EventHandler? PickedMapChanged;
        public event EventHandler? MapV2BannedChanged;

        // INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        // Properties

        public string RemainingSeconds { get; set; } = string.Empty;

        public Team HomeTeam => throw new System.NotImplementedException();

        public Team AwayTeam => throw new System.NotImplementedException();

        public Game CurrentGame => throw new NotImplementedException();

        public SortedDictionary<string, Character> SurCharaDict
        {
            get => throw new System.NotImplementedException();
            set => throw new System.NotImplementedException();
        }

        public SortedDictionary<string, Character> HunCharaDict
        {
            get => throw new System.NotImplementedException();
            set => throw new System.NotImplementedException();
        }

        public ObservableCollection<bool> CanCurrentSurBannedList => throw new System.NotImplementedException();

        public ObservableCollection<bool> CanCurrentHunBannedList => throw new System.NotImplementedException();

        public ObservableCollection<bool> CanGlobalSurBannedList => throw new System.NotImplementedException();

        public ObservableCollection<bool> CanGlobalHunBannedList => throw new System.NotImplementedException();

        public bool IsTraitVisible
        {
            get => throw new System.NotImplementedException();
            set => throw new System.NotImplementedException();
        }

        public bool IsBo3Mode
        {
            get => throw new System.NotImplementedException();
            set => throw new System.NotImplementedException();
        }

        public bool IsMapV2Breathing
        {
            get => throw new System.NotImplementedException();
            set => throw new System.NotImplementedException();
        }

        public bool IsMapV2CampVisible
        {
            get => throw new System.NotImplementedException();
            set => throw new System.NotImplementedException();
        }

        public void NewGame() => throw new System.NotImplementedException();

        public Task ImportGameAsync(string filePath) => throw new System.NotImplementedException();

        public void SetBanCount(BanListName listName, int count) => throw new System.NotImplementedException();

        public void TimerStart(int? seconds) => throw new System.NotImplementedException();

        public void TimerStop() => throw new System.NotImplementedException();

        // Fire helpers

        public void FireCountDownValueChanged() => CountDownValueChanged?.Invoke(this, System.EventArgs.Empty);

        public void FireBanCountChanged(BanCountChangedEventArgs args) => BanCountChanged?.Invoke(this, args);

        public void FirePropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void FireCharacterPicked(CharacterPickedEventArgs args) => CharacterPicked?.Invoke(this, args);

        public void FireUnmarkedEvent() => UnmarkedEvent?.Invoke(this, System.EventArgs.Empty);

        public void FireMapV2PickingBorderStateChanged(MapV2PickingBorderStateChangedEventArgs args) =>
            MapV2PickingBorderStateChanged?.Invoke(this, args);
    }

    private sealed class MockCharacterSelectionService : ICharacterSelectionService
    {
        public event EventHandler<CharacterSelectedEventArgs>? CharacterSelected;
        public event EventHandler<CharacterBannedEventArgs>? CharacterBanned;

        public Character? ResolveCharacter(string text, Camp camp) => null;
        public Task SelectSurvivorAsync(int playerIndex, Character? character, bool playAnimation = true, bool isRecordGlobalBan = true) => Task.CompletedTask;
        public Task SelectHunterAsync(Character? character, bool playAnimation = true, bool isRecordGlobalBan = true) => Task.CompletedTask;
        public Task BanCharacterAsync(Camp camp, int index, Character? character, bool playAnimation = true) => Task.CompletedTask;
        public Task SwapSurvivorsAsync(int sourceIndex, int targetIndex, bool playAnimation = true) => Task.CompletedTask;
        public void FireCharacterSelected(CharacterSelectedEventArgs args) => CharacterSelected?.Invoke(this, args);
    }

    private sealed class MockGameGuidanceService : IGameGuidanceService
    {
        public bool IsGuidanceStarted { get; set; }
        public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStateChanged;
        public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStarted;
        public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceStopped;
        public event EventHandler<GameGuidanceStateChangedEventArgs>? GuidanceCancelled;
        public event EventHandler<GameGuidanceStepChangedEventArgs>? GuidanceStepChanged;
        public event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightChanged;
        public event EventHandler<GameGuidanceHighlightChangedEventArgs>? GuidanceHighlightCleared;

        public Task<string?> StartGuidance() => Task.FromResult<string?>(null);
        public Task<string?> NextStepAsync(bool playAnimation) => Task.FromResult<string?>(null);
        public Task<string?> PrevStepAsync(bool playAnimation) => Task.FromResult<string?>(null);
        public void StopGuidance() { }
        public void FireHighlightChanged(GameGuidanceHighlightChangedEventArgs args) => GuidanceHighlightChanged?.Invoke(this, args);
        public void FireStepChanged(GameGuidanceStepChangedEventArgs args) => GuidanceStepChanged?.Invoke(this, args);
        public void FireCancelled(GameGuidanceStateChangedEventArgs args) => GuidanceCancelled?.Invoke(this, args);
    }

    /// <summary>
    /// Mock implementation of <see cref="IFrontedEventBus"/> that captures published events.
    /// </summary>
    private sealed class MockEventBus : IFrontedEventBus
    {
        private readonly List<Subscription> _subscriptions = [];

        public event System.EventHandler<FrontedBehaviorEvent>? EventPublished;

        public void Publish(FrontedBehaviorEvent behaviorEvent)
        {
            PublishedEvents.Add(behaviorEvent);
            EventPublished?.Invoke(this, behaviorEvent);

            // Invoke matching subscriptions
            foreach (var sub in _subscriptions.ToArray())
            {
                if (sub.IsDisposed)
                    continue;
                if (sub.EventType == null || sub.EventType == behaviorEvent.EventType)
                    _ = sub.Handler.Invoke(behaviorEvent);
            }
        }

        public List<FrontedBehaviorEvent> PublishedEvents { get; } = [];

        public IDisposable Subscribe(string? eventType, Func<FrontedBehaviorEvent, Task> handler)
        {
            var subscription = new Subscription(eventType, handler, this);
            _subscriptions.Add(subscription);
            return subscription;
        }

        private sealed class Subscription : IDisposable
        {
            private readonly MockEventBus _owner;

            public Subscription(string? eventType, Func<FrontedBehaviorEvent, Task> handler, MockEventBus owner)
            {
                EventType = eventType;
                Handler = handler;
                _owner = owner;
            }

            public string? EventType { get; }
            public Func<FrontedBehaviorEvent, Task> Handler { get; }
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                if (IsDisposed) return;
                IsDisposed = true;
                _owner._subscriptions.Remove(this);
            }
        }
    }
}
