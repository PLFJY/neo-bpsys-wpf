#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Services;
using System;
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
        var service = new CharacterSelectionService(sharedData.Object, CreateProvider(transition.Object));
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

        var provider = CreateProvider(
            transition.Object,
            CreateLayoutService(targetGuid));
        var service = new CharacterSelectionService(sharedData.Object, provider);
        var selected = new Character("new", Camp.Sur, "new.png");

        await service.SelectSurvivorAsync(0, selected);

        transition.Verify(
            service => service.RunTransitionAsync(
                It.IsAny<FrontedTransitionRequest>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static IServiceProvider CreateProvider(
        IFrontedTransitionOrchestrator transition,
        IFrontedLayoutService? layoutService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(transition);
        services.AddSingleton(layoutService ?? Mock.Of<IFrontedLayoutService>());
        services.AddSingleton(Mock.Of<IAnimationService>());
        return services.BuildServiceProvider();
    }

    private static IFrontedLayoutService CreateLayoutService(Guid targetGuid)
    {
        var layoutService = new Mock<IFrontedLayoutService>();
        layoutService
            .Setup(service => service.LoadWindowConfigAsync("BpWindow", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrontedWindowConfig
            {
                ControlLayout = new FrontedControlLayout
                {
                    Controls =
                    {
                        ["SurPick0"] = new ImageFrontedControlConfig { BehaviorGuid = targetGuid }
                    }
                }
            });
        return layoutService.Object;
    }
}
