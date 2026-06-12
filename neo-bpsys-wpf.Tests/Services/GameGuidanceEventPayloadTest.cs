#nullable enable

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Services;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Wpf.Ui;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class GameGuidanceEventPayloadTest
{
    /// <summary>
    /// Verifies stable bracketed index text for step and highlight event payloads.
    /// </summary>
    [Fact]
    public void IndexesText_UsesStableBracketedFormatting()
    {
        Assert.Equal("[]", CreateStepArgs(null).IndexesText);
        Assert.Equal("[]", CreateStepArgs([]).IndexesText);
        Assert.Equal("[1]", CreateStepArgs([1]).IndexesText);
        Assert.Equal("[1, 2]", CreateStepArgs([1, 2]).IndexesText);
        Assert.Equal("[1, 2]", new GameGuidanceHighlightChangedEventArgs(GameAction.PickSur, [1, 2]).IndexesText);
    }

    /// <summary>
    /// Verifies step changed args can be constructed without localized action display names.
    /// </summary>
    [Fact]
    public void StepChangedArgs_DoNotExposeLocalizedActionNames()
    {
        var args = new GameGuidanceStepChangedEventArgs(
            stepIndex: 1,
            action: GameAction.PickSur,
            index: [0],
            time: 30,
            previousStepIndex: 0,
            previousAction: GameAction.BanSur,
            previousIndex: [1, 2],
            previousTime: 40);

        Assert.Equal(GameAction.PickSur, args.Action);
        Assert.Equal("[0]", args.IndexesText);
        Assert.Equal(GameAction.BanSur, args.PreviousAction);
        Assert.Equal("[1, 2]", args.PreviousIndexesText);
        Assert.Null(typeof(GameGuidanceStepChangedEventArgs).GetProperty("ActionName"));
        Assert.Null(typeof(GameGuidanceStepChangedEventArgs).GetProperty("PreviousActionName"));
    }

    /// <summary>
    /// Verifies the first guidance step does not report a previous step.
    /// </summary>
    [Fact]
    public async Task NextStepAsync_FirstStep_HasNoPreviousStep()
    {
        var service = CreateService(
        [
            new GameGuidanceService.Step { Action = GameAction.PickSur, Index = [1] }
        ]);
        GameGuidanceStepChangedEventArgs? received = null;
        service.GuidanceStepChanged += (_, args) => received = args;

        await service.NextStepAsync();

        Assert.NotNull(received);
        Assert.Equal(0, received.StepIndex);
        Assert.Null(received.PreviousStepIndex);
        Assert.Null(received.PreviousAction);
        Assert.Null(received.PreviousIndexes);
        Assert.Equal("[]", received.PreviousIndexesText);
    }

    /// <summary>
    /// Verifies moving forward reports the step that was active before navigation.
    /// </summary>
    [Fact]
    public async Task NextStepAsync_ReportsPreviousStep()
    {
        var service = CreateService(
        [
            new GameGuidanceService.Step { Action = GameAction.PickSur, Index = [1] },
            new GameGuidanceService.Step { Action = GameAction.PickHun, Index = [0] }
        ]);
        GameGuidanceStepChangedEventArgs? received = null;
        service.GuidanceStepChanged += (_, args) => received = args;

        await service.NextStepAsync();
        await service.NextStepAsync();

        Assert.NotNull(received);
        Assert.Equal(GameAction.PickHun, received.Action);
        Assert.Equal("[0]", received.IndexesText);
        Assert.Equal(0, received.PreviousStepIndex);
        Assert.Equal(GameAction.PickSur, received.PreviousAction);
        Assert.Equal("[1]", received.PreviousIndexesText);
        Assert.Equal(1, received.PreviousFirstIndex);
    }

    /// <summary>
    /// Verifies moving backward reports the step that was active before navigation.
    /// </summary>
    [Fact]
    public async Task PrevStepAsync_ReportsStepBeforeMovingBackward()
    {
        var service = CreateService(
        [
            new GameGuidanceService.Step { Action = GameAction.PickSur, Index = [1] },
            new GameGuidanceService.Step { Action = GameAction.PickHun, Index = [0] },
            new GameGuidanceService.Step { Action = GameAction.BanSur, Index = [2] }
        ]);
        GameGuidanceStepChangedEventArgs? received = null;
        service.GuidanceStepChanged += (_, args) => received = args;

        await service.NextStepAsync();
        await service.NextStepAsync();
        await service.NextStepAsync();
        await service.PrevStepAsync();

        Assert.NotNull(received);
        Assert.Equal(1, received.StepIndex);
        Assert.Equal(2, received.PreviousStepIndex);
        Assert.Equal(GameAction.BanSur, received.PreviousAction);
        Assert.Equal("[2]", received.PreviousIndexesText);
    }

    private static GameGuidanceStepChangedEventArgs CreateStepArgs(List<int>? indexes) =>
        new(0, GameAction.PickSur, indexes, null);

    private static GameGuidanceService CreateService(List<GameGuidanceService.Step> workflow)
    {
        var service = new GameGuidanceService(
            new Mock<ISharedDataService>().Object,
            new Mock<INavigationService>().Object,
            new Mock<IInfoBarService>().Object,
            NullLogger<GameGuidanceService>.Instance)
        {
            IsGuidanceStarted = true
        };
        var gamePropertyField = typeof(GameGuidanceService).GetField(
            "_currentGameProperty",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(gamePropertyField);
        gamePropertyField.SetValue(service, new GameGuidanceService.GameProperty { WorkFlow = workflow });
        return service;
    }
}
