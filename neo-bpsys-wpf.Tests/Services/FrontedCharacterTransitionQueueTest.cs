#nullable enable

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Tests.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class FrontedCharacterTransitionQueueTest
{
    [Fact]
    public async Task OverlappingSwapsWaitWhileDisjointSwapsCanRunTogether()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var game = CreateGame();
            var shared = CreateShared(game);
            var started = new List<string>();
            var gates = new Dictionary<string, TaskCompletionSource>();
            var orchestrator = new Mock<IFrontedTransitionOrchestrator>();
            orchestrator.Setup(value => value.RunMultiTargetTransitionAsync(
                    It.IsAny<IReadOnlyList<FrontedTransitionRequest>>(),
                    It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyList<FrontedTransitionRequest>, Func<Task>, CancellationToken>(async (requests, commit, token) =>
                {
                    var key = string.Join(',', requests.Select(request => request.TargetDisplayName));
                    started.Add(key);
                    await gates[key].Task.WaitAsync(token);
                    await commit();
                });
            var queue = CreateQueue(shared.Object, orchestrator.Object);
            queue.EnsureCurrentGame();
            var presented = GetPresented(queue, shared.Object, 0);
            var initial = game.SurPlayerList.Select(player => player.Character).ToArray();

            gates["SurPick0,SurPick1"] = NewGate();
            game.SwapCharactersInPlayers(0, 1);
            queue.EnqueueSwap(0, 1, initial[0], initial[1]);
            await WaitForAsync(() => started.Count == 1);

            gates["SurPick2,SurPick3"] = NewGate();
            var beforeDisjoint = game.SurPlayerList.Select(player => player.Character).ToArray();
            game.SwapCharactersInPlayers(2, 3);
            queue.EnqueueSwap(2, 3, beforeDisjoint[2], beforeDisjoint[3]);
            await WaitForAsync(() => started.Count == 2);
            Assert.Same(initial[0], presented.Character);

            gates["SurPick1,SurPick2"] = NewGate();
            var beforeOverlap = game.SurPlayerList.Select(player => player.Character).ToArray();
            game.SwapCharactersInPlayers(1, 2);
            queue.EnqueueSwap(1, 2, beforeOverlap[1], beforeOverlap[2]);
            Assert.Equal(2, started.Count);

            gates["SurPick2,SurPick3"].SetResult();
            await WaitForAsync(() => GetPresented(queue, shared.Object, 2).Character == beforeDisjoint[3]);
            Assert.Equal(2, started.Count);
            gates["SurPick0,SurPick1"].SetResult();
            await WaitForAsync(() => started.Count == 3);
            gates["SurPick1,SurPick2"].SetResult();
            await WaitForAsync(() => GetPresented(queue, shared.Object, 1).Character == game.SurPlayerList[1].Character);
            Assert.Equal(game.SurPlayerList.Select(player => player.Character),
                Enumerable.Range(0, 4).Select(index => GetPresented(queue, shared.Object, index).Character));
        });
    }

    [Fact]
    public async Task ManualSyncDropsPendingSwapAndAnimatesFinalPositionsAsPicks()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var game = CreateGame();
            var shared = CreateShared(game);
            var swapStarted = NewGate();
            var pickTargets = new List<string>();
            var orchestrator = new Mock<IFrontedTransitionOrchestrator>();
            orchestrator.Setup(value => value.RunMultiTargetTransitionAsync(
                    It.IsAny<IReadOnlyList<FrontedTransitionRequest>>(),
                    It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyList<FrontedTransitionRequest>, Func<Task>, CancellationToken>(async (requests, commit, token) =>
                {
                    if (requests[0].TransitionType == "Selection.CharacterSwap")
                    {
                        swapStarted.TrySetResult();
                        await Task.Delay(Timeout.Infinite, token);
                    }
                    else
                    {
                        pickTargets.Add(requests[0].TargetDisplayName);
                        await commit();
                    }
                });
            var queue = CreateQueue(shared.Object, orchestrator.Object);
            queue.EnsureCurrentGame();
            var first = game.SurPlayerList[0].Character;
            var second = game.SurPlayerList[1].Character;
            game.SwapCharactersInPlayers(0, 1);
            queue.EnqueueSwap(0, 1, first, second);
            await swapStarted.Task;

            await queue.RunManualSyncAsync(() => Task.FromResult(true));
            await WaitForAsync(() => pickTargets.Count == 2);
            Assert.Equal(["SurPick0", "SurPick1"], pickTargets.OrderBy(value => value));
            Assert.Same(second, GetPresented(queue, shared.Object, 0).Character);
            Assert.Same(first, GetPresented(queue, shared.Object, 1).Character);
        });
    }

    [Fact]
    public async Task ImmediateSyncCancelsActiveTransitionAndShowsAuthoritativeRoles()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var game = CreateGame();
            var shared = CreateShared(game);
            var started = NewGate();
            var orchestrator = new Mock<IFrontedTransitionOrchestrator>();
            orchestrator.Setup(value => value.RunMultiTargetTransitionAsync(
                    It.IsAny<IReadOnlyList<FrontedTransitionRequest>>(),
                    It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyList<FrontedTransitionRequest>, Func<Task>, CancellationToken>(async (_, _, token) =>
                {
                    started.TrySetResult();
                    await Task.Delay(Timeout.Infinite, token);
                });
            var queue = CreateQueue(shared.Object, orchestrator.Object);
            queue.EnsureCurrentGame();
            var before = game.SurPlayerList[0].Character;
            game.SurPlayerList[0].Character = new Character("replacement", Camp.Sur, "replacement.png");
            queue.EnqueuePick(Camp.Sur, 0, before, game.SurPlayerList[0].Character);
            await started.Task;
            Assert.Same(before, GetPresented(queue, shared.Object, 0).Character);

            queue.SynchronizeImmediately();
            Assert.Same(game.SurPlayerList[0].Character, GetPresented(queue, shared.Object, 0).Character);
        });
    }

    [Fact]
    public async Task DetachingBpWindowDiscardsOldPresentationWork()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var game = CreateGame();
            var shared = CreateShared(game);
            var started = NewGate();
            var orchestrator = new Mock<IFrontedTransitionOrchestrator>();
            orchestrator.Setup(value => value.RunMultiTargetTransitionAsync(
                    It.IsAny<IReadOnlyList<FrontedTransitionRequest>>(),
                    It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyList<FrontedTransitionRequest>, Func<Task>, CancellationToken>(async (_, _, token) =>
                {
                    started.TrySetResult();
                    await Task.Delay(Timeout.Infinite, token);
                });
            var queue = CreateQueue(shared.Object, orchestrator.Object, out var manager);
            await manager.AttachHostAsync(new FrontedBehaviorRuntimeContext
            {
                WindowId = "BpWindow",
                WindowType = "BpWindow",
                RootCanvas = new Canvas(),
                WindowConfig = new FrontedWindowConfig(),
                SharedDataService = shared.Object
            });
            try
            {
                queue.EnsureCurrentGame();
                var before = game.SurPlayerList[0].Character;
                game.SurPlayerList[0].Character = new Character("replacement", Camp.Sur, "replacement.png");
                queue.EnqueuePick(Camp.Sur, 0, before, game.SurPlayerList[0].Character);
                await started.Task;

                manager.DetachHost("BpWindow");
                Assert.Same(game.SurPlayerList[0].Character, GetPresented(queue, shared.Object, 0).Character);
            }
            finally
            {
                manager.DetachHost("BpWindow");
            }
        });
    }

    [Fact]
    public async Task DynamicCharacterBindingReadsPresentedSlotUntilItsTransitionCommits()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var game = CreateGame();
            game.GameProgress = GameProgress.Game1FirstHalf;
            var shared = CreateShared(game);
            var started = NewGate();
            var release = NewGate();
            var orchestrator = new Mock<IFrontedTransitionOrchestrator>();
            orchestrator.Setup(value => value.RunMultiTargetTransitionAsync(
                    It.IsAny<IReadOnlyList<FrontedTransitionRequest>>(),
                    It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyList<FrontedTransitionRequest>, Func<Task>, CancellationToken>(async (_, commit, token) =>
                {
                    started.TrySetResult();
                    await release.Task.WaitAsync(token);
                    await commit();
                });
            var queue = CreateQueue(shared.Object, orchestrator.Object);
            var text = new TextBlock();
            BindingOperations.SetBinding(text, TextBlock.TextProperty, FrontedBindingFactory.Create(
                "CurrentGame.SurPlayerList[CurrentGame.GameProgress].Character.Name", shared.Object, queue));
            Assert.Equal("role0", text.Text);

            var before = game.SurPlayerList[0].Character;
            game.SurPlayerList[0].Character = new Character("replacement", Camp.Sur, "replacement.png");
            queue.EnqueuePick(Camp.Sur, 0, before, game.SurPlayerList[0].Character);
            await started.Task;
            Assert.Equal("role0", text.Text);

            release.SetResult();
            await WaitForAsync(() => text.Text == "replacement");
        });
    }

    private static FrontedCharacterTransitionQueue CreateQueue(
        ISharedDataService shared,
        IFrontedTransitionOrchestrator orchestrator) =>
        CreateQueue(shared, orchestrator, out _);

    private static FrontedCharacterTransitionQueue CreateQueue(
        ISharedDataService shared,
        IFrontedTransitionOrchestrator orchestrator,
        out FrontedBehaviorRuntimeHostManager manager)
    {
        var layout = new Mock<IFrontedLayoutService>();
        layout.Setup(value => value.LoadWindowConfigAsync("BpWindow", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrontedWindowConfig());
        var behavior = new Mock<IFrontedBehaviorService>();
        behavior.Setup(value => value.LoadDocumentAsync("BpWindow", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrontedBehaviorDocument());
        manager = new FrontedBehaviorRuntimeHostManager(
            behavior.Object, Mock.Of<IFrontedEventBus>(),
            Mock.Of<IFrontedNodeGraphRuntime>(), Mock.Of<IFrontedAnimationRuntime>(),
            Mock.Of<IFrontedBehaviorAnimationPartRenderer>(), new FrontedBehaviorTriggerEvaluator(),
            NullLogger<FrontedBehaviorRuntimeHostManager>.Instance);
        return new FrontedCharacterTransitionQueue(
            shared, orchestrator, manager, layout.Object, NullLogger<FrontedCharacterTransitionQueue>.Instance);
    }

    private static Mock<ISharedDataService> CreateShared(Game game)
    {
        var shared = new Mock<ISharedDataService>();
        shared.SetupGet(value => value.CurrentGame).Returns(game);
        return shared;
    }

    private static Game CreateGame()
    {
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam),
            new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        for (var index = 0; index < 4; index++)
            game.SurPlayerList[index].Character = new Character($"role{index}", Camp.Sur, $"role{index}.png");
        return game;
    }

    private static Player GetPresented(FrontedCharacterTransitionQueue queue, ISharedDataService shared, int index)
    {
        Assert.True(queue.TryCreateBinding($"CurrentGame.SurPlayerList[{index}].Character.Name", shared, out var binding));
        var player = Assert.IsType<Player>(binding!.Source);
        var rendererBinding = FrontedBindingFactory.Create(
            $"CurrentGame.SurPlayerList[{index}].PictureShown", shared, queue);
        Assert.Same(player, rendererBinding.Source);
        return player;
    }

    private static TaskCompletionSource NewGate() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }
}
