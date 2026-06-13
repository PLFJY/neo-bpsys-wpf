#nullable enable

using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class CharacterSelectionServiceTransitionTest
{
    [Fact]
    public async Task SelectSurvivor_PlayAnimationFalse_BypassesTransitionAndFiresAfterCommit()
    {
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var sharedData = new Mock<ISharedDataService>();
        sharedData.Setup(service => service.CurrentGame).Returns(game);
        var transition = new Mock<IFrontedTransitionOrchestrator>(MockBehavior.Strict);
        var service = new CharacterSelectionService(
            sharedData.Object,
            transition.Object,
            Mock.Of<IFrontedLayoutService>());
        var selected = new Character("new", Camp.Sur, "new.png");
        CharacterSelectedEventArgs? received = null;
        Character? characterAtEvent = null;
        service.CharacterSelected += (_, args) =>
        {
            received = args;
            characterAtEvent = game.SurPlayerList[0].Character;
        };

        await service.SelectSurvivorAsync(0, selected, playAnimation: false);

        Assert.Same(selected, game.SurPlayerList[0].Character);
        Assert.Equal(new CharacterSelectedEventArgs(Camp.Sur, 0), received);
        Assert.Same(selected, characterAtEvent);
        transition.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SelectSurvivor_PlayAnimationTrue_CommitsInsideTransition()
    {
        var targetGuid = Guid.NewGuid();
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var sharedData = new Mock<ISharedDataService>();
        sharedData.Setup(service => service.CurrentGame).Returns(game);

        var transition = new Mock<IFrontedTransitionOrchestrator>();
        transition
            .Setup(service => service.RunTransitionAsync(
                It.IsAny<FrontedTransitionRequest>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<FrontedTransitionRequest, Func<Task>, CancellationToken>(async (request, commitAsync, _) =>
            {
                Assert.Equal(targetGuid, request.TargetBehaviorGuid);
                Assert.Null(game.SurPlayerList[0].Character);
                await commitAsync();
                Assert.NotNull(game.SurPlayerList[0].Character);
            });

        var service = new CharacterSelectionService(
            sharedData.Object,
            transition.Object,
            CreateLayoutService(targetGuid));
        var selected = new Character("new", Camp.Sur, "new.png");

        await service.SelectSurvivorAsync(0, selected);

        transition.Verify(
            service => service.RunTransitionAsync(
                It.IsAny<FrontedTransitionRequest>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SelectHunter_PlayAnimationTrue_UsesHunterTransitionAndCommitsInsideIt()
    {
        var targetGuid = Guid.NewGuid();
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var sharedData = new Mock<ISharedDataService>();
        sharedData.Setup(service => service.CurrentGame).Returns(game);
        var transition = new Mock<IFrontedTransitionOrchestrator>();
        transition
            .Setup(service => service.RunTransitionAsync(
                It.IsAny<FrontedTransitionRequest>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<FrontedTransitionRequest, Func<Task>, CancellationToken>(async (request, commitAsync, _) =>
            {
                Assert.Equal("HunPick", request.TargetDisplayName);
                Assert.Equal("Hun", request.Payload["Event.Camp"]);
                Assert.Equal(-1, request.Payload["Event.PlayerIndex"]);
                Assert.Null(game.HunPlayer.Character);
                await commitAsync();
                Assert.NotNull(game.HunPlayer.Character);
            });
        var layout = CreateLayoutService(("HunPick", targetGuid));
        var service = new CharacterSelectionService(sharedData.Object, transition.Object, layout);

        await service.SelectHunterAsync(new Character("new", Camp.Hun, "new.png"));

        transition.VerifyAll();
    }

    [Fact]
    public async Task SwapSurvivors_PlayAnimationTrue_UsesMultiTargetTransition()
    {
        var firstGuid = Guid.NewGuid();
        var secondGuid = Guid.NewGuid();
        var game = new Game(new Team(Camp.Sur, TeamType.HomeTeam), new Team(Camp.Hun, TeamType.AwayTeam), GameProgress.Free);
        var first = new Character("first", Camp.Sur, "first.png");
        var second = new Character("second", Camp.Sur, "second.png");
        game.SurPlayerList[0].Character = first;
        game.SurPlayerList[1].Character = second;
        var sharedData = new Mock<ISharedDataService>();
        sharedData.Setup(service => service.CurrentGame).Returns(game);
        var transition = new Mock<IFrontedTransitionOrchestrator>();
        transition
            .Setup(service => service.RunMultiTargetTransitionAsync(
                It.IsAny<IReadOnlyList<FrontedTransitionRequest>>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<FrontedTransitionRequest>, Func<Task>, CancellationToken>(async (requests, commitAsync, _) =>
            {
                Assert.Equal(["SurPick0", "SurPick1"], requests.Select(request => request.TargetDisplayName));
                Assert.Same(first, game.SurPlayerList[0].Character);
                await commitAsync();
                Assert.Same(second, game.SurPlayerList[0].Character);
            });
        var layout = CreateLayoutService(("SurPick0", firstGuid), ("SurPick1", secondGuid));
        var service = new CharacterSelectionService(sharedData.Object, transition.Object, layout);

        await service.SwapSurvivorsAsync(0, 1);

        transition.VerifyAll();
    }

    private static IFrontedLayoutService CreateLayoutService(Guid targetGuid)
        => CreateLayoutService(("SurPick0", targetGuid));

    private static IFrontedLayoutService CreateLayoutService(params (string Name, Guid Guid)[] controls)
    {
        var layoutService = new Mock<IFrontedLayoutService>();
        var config = new FrontedWindowConfig();
        foreach (var (name, guid) in controls)
        {
            config.ControlLayout.Controls[name] = new ImageFrontedControlConfig { BehaviorGuid = guid };
        }

        layoutService
            .Setup(service => service.LoadWindowConfigAsync("BpWindow", It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        return layoutService.Object;
    }
}
