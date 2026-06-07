#nullable enable

using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using System;
using Xunit;

namespace neo_bpsys_wpf.Tests.ViewModels;

public class BehaviorPanelViewModelTest
{
    [Fact]
    public void BehaviorPanel_AddOneShot_AssignsBehaviorGuidWhenEmpty()
    {
        var layoutDirtyCount = 0;
        var behaviorDirtyCount = 0;
        var panel = CreatePanel(() => layoutDirtyCount++, () => behaviorDirtyCount++);
        var item = CreateItem(Guid.Empty);

        panel.SetSelectedControl(item);
        panel.AddOneShotBehavior();

        Assert.NotEqual(Guid.Empty, item.Config.BehaviorGuid);
        Assert.Equal(1, layoutDirtyCount);
        Assert.True(behaviorDirtyCount > 0);
        Assert.NotNull(panel.CurrentDocument.FindSet(item.Config.BehaviorGuid));
    }

    [Fact]
    public void BehaviorPanel_AddOneShot_UsesExistingBehaviorGuid()
    {
        var existingGuid = Guid.NewGuid();
        var layoutDirtyCount = 0;
        var panel = CreatePanel(() => layoutDirtyCount++, static () => { });
        var item = CreateItem(existingGuid);

        panel.SetSelectedControl(item);
        panel.AddOneShotBehavior();

        Assert.Equal(existingGuid, item.Config.BehaviorGuid);
        Assert.Equal(0, layoutDirtyCount);
        Assert.NotNull(panel.CurrentDocument.FindSet(existingGuid));
    }

    [Fact]
    public void BehaviorPanel_AddLoop_CreatesStartLoopStopGraphs()
    {
        var panel = CreatePanel();
        var item = CreateItem(Guid.NewGuid());

        panel.SetSelectedControl(item);
        panel.AddLoopBehavior();

        var behavior = Assert.Single(panel.CurrentDocument.FindSet(item.Config.BehaviorGuid)!.Behaviors);
        Assert.Equal(FrontedBehaviorKind.Loop, behavior.Kind);
        Assert.NotNull(behavior.StartGraph);
        Assert.NotNull(behavior.LoopGraph);
        Assert.NotNull(behavior.StopGraph);
        Assert.NotNull(behavior.StartTrigger);
        Assert.NotNull(behavior.EndTrigger);
        Assert.NotNull(behavior.LoopPolicy);
    }

    [Fact]
    public void BehaviorPanel_DeleteBehavior_RemovesBehaviorAndMarksDirty()
    {
        var dirtyCount = 0;
        var panel = CreatePanel(markBehaviorsDirty: () => dirtyCount++);
        var item = CreateItem(Guid.NewGuid());
        panel.SetSelectedControl(item);
        panel.AddOneShotBehavior();
        dirtyCount = 0;

        panel.DeleteBehavior(panel.SelectedBehavior);

        Assert.Empty(panel.Behaviors);
        Assert.Null(panel.CurrentDocument.FindSet(item.Config.BehaviorGuid));
        Assert.Equal(1, dirtyCount);
    }

    [Fact]
    public void BehaviorPanel_DuplicateBehavior_GeneratesNewBehaviorId()
    {
        var panel = CreatePanel();
        var item = CreateItem(Guid.NewGuid());
        panel.SetSelectedControl(item);
        panel.AddOneShotBehavior();
        var originalId = panel.SelectedBehavior!.Model.BehaviorId;

        panel.DuplicateBehavior(panel.SelectedBehavior);

        var behaviors = panel.CurrentDocument.FindSet(item.Config.BehaviorGuid)!.Behaviors;
        Assert.Equal(2, behaviors.Count);
        Assert.NotEqual(originalId, behaviors[1].BehaviorId);
    }

    [Fact]
    public void BehaviorPanel_SelectedControlChanged_LoadsMatchingSet()
    {
        var guid = Guid.NewGuid();
        var document = new FrontedBehaviorDocument();
        document.GetOrCreateSet(guid, "Title").Behaviors.Add(new FrontedBehavior { Name = "Fade" });
        var panel = CreatePanel();
        panel.SetDocument(document);

        panel.SetSelectedControl(CreateItem(guid));

        Assert.Single(panel.Behaviors);
        Assert.Equal("Fade", panel.SelectedBehavior!.Name);
    }

    [Fact]
    public void BehaviorPanel_SelectedControlWithoutGuid_DoesNotGenerateGuidUntilAdd()
    {
        var item = CreateItem(Guid.Empty);
        var panel = CreatePanel();

        panel.SetSelectedControl(item);

        Assert.Equal(Guid.Empty, item.Config.BehaviorGuid);
        Assert.Empty(panel.Behaviors);
    }

    private static BehaviorPanelViewModel CreatePanel(
        Action? markLayoutDirty = null,
        Action? markBehaviorsDirty = null)
    {
        return new BehaviorPanelViewModel(
            new neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedDesignerLocalizationService(),
            new neo_bpsys_wpf.Core.Services.FrontedLayout.FrontedBehaviorEventCatalog(),
            markLayoutDirty ?? (() => { }),
            markBehaviorsDirty ?? (() => { }));
    }

    private static FrontedControlDesignItem CreateItem(Guid behaviorGuid)
    {
        return new FrontedControlDesignItem
        {
            Name = "Title",
            Config = new TextFrontedControlConfig
            {
                BehaviorGuid = behaviorGuid
            }
        };
    }
}
