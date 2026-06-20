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
    public void ResolveCharacter_SelectsHighestScoringCandidateInRequestedCamp()
    {
        var expected = new Character("心理学家", Camp.Sur, "psychologist.png");
        var service = CreateService(
            [
                expected,
                new Character("病患", Camp.Sur, "patient.png")
            ],
            [new Character("心理学家", Camp.Hun, "hunter.png")]);

        var result = service.ResolveCharacter("『心理学家』", Camp.Sur);

        Assert.Same(expected, result);
    }

    [Fact]
    public void ResolveCharacterDetailed_ExactSurvivorMatch()
    {
        var expected = new Character("入殓师", Camp.Sur, "embalmer.png");
        var service = CreateService([expected], []);

        var result = service.ResolveCharacterDetailed("入殓师", Camp.Sur);

        Assert.Same(expected, result.Character);
        Assert.Equal("入殓师", result.CanonicalName);
        Assert.Equal("exact", result.MatchMode);
        Assert.True(result.IsAutoApplySafe);
    }

    [Fact]
    public void ResolveCharacterDetailed_ChineseOcrTypoUsesShortNameCorrection()
    {
        var expected = new Character("入殓师", Camp.Sur, "embalmer.png");
        var service = CreateService([expected], []);

        var result = service.ResolveCharacterDetailed("入验师", Camp.Sur);

        Assert.Same(expected, result.Character);
        Assert.Equal("short-name-correction", result.MatchMode);
        Assert.True(result.IsAutoApplySafe);
    }

    [Fact]
    public void ResolveCharacterDetailed_NeverFallsBackAcrossCampsForSurvivorTypo()
    {
        var service = CreateService([new Character("入殓师", Camp.Sur, "embalmer.png")], []);

        var result = service.ResolveCharacterDetailed("入验师", Camp.Hun);

        Assert.Null(result.Character);
        Assert.False(result.IsAutoApplySafe);
    }

    [Fact]
    public void ResolveCharacterDetailed_HunterTypoUsesShortNameCorrection()
    {
        var expected = new Character("厂长", Camp.Hun, "hell-ember.png");
        var service = CreateService([], [expected]);

        var result = service.ResolveCharacterDetailed("广长", Camp.Hun);

        Assert.Same(expected, result.Character);
        Assert.Equal("short-name-correction", result.MatchMode);
        Assert.True(result.IsAutoApplySafe);
    }

    [Fact]
    public void ResolveCharacterDetailed_NeverFallsBackAcrossCampsForHunterTypo()
    {
        var service = CreateService([], [new Character("厂长", Camp.Hun, "hell-ember.png")]);

        var result = service.ResolveCharacterDetailed("广长", Camp.Sur);

        Assert.Null(result.Character);
        Assert.False(result.IsAutoApplySafe);
    }

    [Fact]
    public void ResolveCharacterDetailed_StripsDecorativeQuotes()
    {
        var expected = new Character("心理学家", Camp.Sur, "psychologist.png");
        var service = CreateService([expected], []);

        var result = service.ResolveCharacterDetailed("“心理学家”", Camp.Sur);

        Assert.Same(expected, result.Character);
        Assert.Equal("normalized-exact", result.MatchMode);
        Assert.True(result.IsAutoApplySafe);
    }

    [Fact]
    public void ResolveCharacterDetailed_AmbiguousFuzzyResultIsUnresolved()
    {
        var service = CreateService(
            [
                new Character("abcx", Camp.Sur, "left.png"),
                new Character("abcy", Camp.Sur, "right.png")
            ],
            []);

        var result = service.ResolveCharacterDetailed("abc", Camp.Sur);

        Assert.Null(result.Character);
        Assert.Equal("ambiguous", result.MatchMode);
        Assert.False(result.IsAutoApplySafe);
    }

    [Fact]
    public void ResolveCharacterDetailed_WeakMatchBelowThresholdIsUnresolved()
    {
        var service = CreateService([new Character("入殓师", Camp.Sur, "embalmer.png")], []);

        var result = service.ResolveCharacterDetailed("xyz", Camp.Sur);

        Assert.Null(result.Character);
        Assert.Equal("unresolved", result.MatchMode);
        Assert.False(result.IsAutoApplySafe);
    }

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
                Assert.False((bool)request.Payload["HasOldCharacter"]!);
                Assert.False((bool)request.Payload["Event.HasOldCharacter"]!);
                Assert.True((bool)request.Payload["HasNewCharacter"]!);
                Assert.True((bool)request.Payload["Event.HasNewCharacter"]!);
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

    private static CharacterSelectionService CreateService(
        IReadOnlyList<Character> survivors,
        IReadOnlyList<Character> hunters)
    {
        var sharedData = new Mock<ISharedDataService>();
        sharedData.SetupGet(service => service.SurCharaDict).Returns(new SortedDictionary<string, Character>(survivors.ToDictionary(item => item.Name)));
        sharedData.SetupGet(service => service.HunCharaDict).Returns(new SortedDictionary<string, Character>(hunters.ToDictionary(item => item.Name)));
        return new(
            sharedData.Object,
            Mock.Of<IFrontedTransitionOrchestrator>(),
            Mock.Of<IFrontedLayoutService>());
    }

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
