using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedBehaviorCopyPasteServiceTest
{
    private readonly FrontedBehaviorCopyPasteService _service =
        new(new FrontedBehaviorControlSemanticResolver());

    [Fact]
    public void Preview_LocalizesCompatibilityAndIndexMessages()
    {
        var sourceGuid = Guid.NewGuid();
        var service = new FrontedBehaviorCopyPasteService(
            new FrontedBehaviorControlSemanticResolver(),
            new TestLocalizationService());
        var source = ImageItem("Source", sourceGuid, pickingBorder: true);
        var target = ImageItem("Target", Guid.NewGuid(), pickingBorder: false);
        var payload = service.Copy(
            "BpWindow",
            source,
            BehaviorWithTargets($"part:{sourceGuid}:PickingBorder"));

        var preview = service.Preview(payload, target);

        Assert.Contains("localized:Designer.Behaviors.TargetMissingPickingBorder", preview.CompatibilityErrors);
        Assert.Equal(
            "localized:Designer.Behaviors.SourceIndexUnavailable",
            preview.TriggerIndexRemapUnavailableReason);
    }

    [Fact]
    public void Paste_RewritesPlainGuidAndGeneratedPartButNotExternalTarget()
    {
        var sourceGuid = Guid.NewGuid();
        var targetGuid = Guid.NewGuid();
        var otherGuid = Guid.NewGuid();
        var source = ImageItem("SurPick0", sourceGuid, pickingBorder: true);
        var target = ImageItem("SurPick1", targetGuid, pickingBorder: true);
        var behavior = BehaviorWithTargets(
            $"guid:{sourceGuid}",
            $"part:{sourceGuid}:PickingBorder",
            $"guid:{otherGuid}");
        var payload = _service.Copy("BpWindow", source, behavior);

        var result = _service.Paste(payload, target, new FrontedBehaviorDocument());

        Assert.True(result.Succeeded);
        Assert.Equal(
            [$"guid:{targetGuid}", $"part:{targetGuid}:PickingBorder", $"guid:{otherGuid}"],
            Targets(result.Behavior!).ToArray());
        Assert.Contains($"guid:{otherGuid}", result.Preview.ExternalReferences);
    }

    [Fact]
    public void Paste_BlocksPickingBorderWhenTargetDoesNotProvidePart()
    {
        var sourceGuid = Guid.NewGuid();
        var source = ImageItem("SurPick0", sourceGuid, pickingBorder: true);
        var target = ImageItem("SurPick1", Guid.NewGuid(), pickingBorder: false);
        var payload = _service.Copy(
            "BpWindow",
            source,
            BehaviorWithTargets($"part:{sourceGuid}:PickingBorder"));
        var document = new FrontedBehaviorDocument();

        var result = _service.Paste(payload, target, document);

        Assert.False(result.Succeeded);
        Assert.Empty(document.ControlBehaviorSets);
        Assert.Contains(result.Preview.CompatibilityErrors, message => message.Contains("Picking Border"));
    }

    [Fact]
    public void Paste_RewritesLockOverlayWhenCompatibleAndBlocksWhenMissing()
    {
        var sourceGuid = Guid.NewGuid();
        var source = ImageItem("SurBan0", sourceGuid, lockable: true);
        var payload = _service.Copy(
            "BpWindow",
            source,
            BehaviorWithTargets($"part:{sourceGuid}:LockOverlay"));
        var compatible = ImageItem("SurBan1", Guid.NewGuid(), lockable: true);
        var incompatible = ImageItem("SurBan2", Guid.NewGuid(), lockable: false);

        var pasted = _service.Paste(payload, compatible, new FrontedBehaviorDocument());
        var blocked = _service.Paste(payload, incompatible, new FrontedBehaviorDocument());

        Assert.True(pasted.Succeeded);
        Assert.Equal($"part:{compatible.Config.BehaviorGuid}:LockOverlay", Assert.Single(Targets(pasted.Behavior!)));
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Preview.CompatibilityErrors, message => message.Contains("Lock Overlay"));
    }

    [Fact]
    public void Paste_RewritesGenericAnimationPartAndCopiesMissingPartDefinition()
    {
        var sourceGuid = Guid.NewGuid();
        var source = ImageItem("Source", sourceGuid);
        var sourceDocument = new FrontedBehaviorDocument();
        sourceDocument.GetOrCreateSet(sourceGuid, source.Name).AnimationParts.Add(new FrontedAnimationPartConfig
        {
            Name = "wipeBar",
            Width = 4,
            Fill = "#FFFFFFFF"
        });
        var payload = _service.Copy(
            "BpWindow",
            source,
            BehaviorWithTargets($"part:{sourceGuid}:wipeBar"),
            sourceDocument);
        var compatible = ImageItem("Compatible", Guid.NewGuid());
        var document = new FrontedBehaviorDocument();

        var pasted = _service.Paste(payload, compatible, document);

        Assert.True(pasted.Succeeded);
        Assert.Equal($"part:{compatible.Config.BehaviorGuid}:wipeBar", Assert.Single(Targets(pasted.Behavior!)));
        var copiedPart = Assert.Single(document.FindSet(compatible.Config.BehaviorGuid)!.AnimationParts);
        Assert.Equal("wipeBar", copiedPart.Name);
        Assert.Equal(4, copiedPart.Width);
    }

    [Fact]
    public void Paste_RewritesCurrentAndPreviousTriggerIndexesExactly()
    {
        var source = ImageItem("SurPick0", Guid.NewGuid());
        var target = ImageItem("SurPick1", Guid.NewGuid());
        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Loop,
            StartTrigger = Trigger(
                ("Event.IndexesText", TriggerFilterOperator.Contains, "0"),
                ("Event.Index", TriggerFilterOperator.Equals, "10")),
            StopTriggers =
            [
                Trigger(
                ("Event.PreviousIndexesText", TriggerFilterOperator.Contains, "0"),
                ("Event.PreviousIndexesText", TriggerFilterOperator.Equals, "[0]"))
            ]
        };
        var payload = _service.Copy("BpWindow", source, behavior);

        var result = _service.Paste(payload, target, new FrontedBehaviorDocument());

        Assert.True(result.Succeeded);
        Assert.Equal("1", result.Behavior!.StartTrigger!.Filters[0].Right);
        Assert.Equal("10", result.Behavior.StartTrigger.Filters[1].Right);
        Assert.Equal("1", result.Behavior.StopTriggers[0].Filters[0].Right);
        Assert.Equal("[1]", result.Behavior.StopTriggers[0].Filters[1].Right);
    }

    [Fact]
    public void Paste_RewritesPlayerSwapTransitionAndBracketedIndexFilters()
    {
        var source = ImageItem("SurPick0", Guid.NewGuid());
        var target = ImageItem("SurPick2", Guid.NewGuid());
        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Transition,
            Trigger = Trigger(
                ("Event.PlayerIndex", TriggerFilterOperator.Equals, "0"),
                ("Event.SourceIndex", TriggerFilterOperator.Equals, "0"),
                ("Event.TargetIndex", TriggerFilterOperator.Equals, "0")),
            TransitionTrigger = Trigger(
                ("Event.Indexes", TriggerFilterOperator.Equals, "[0, 1]"),
                ("StartEvent.PreviousIndexesText", TriggerFilterOperator.Equals, "[0]"),
                ("StopEvent.PreviousIndex", TriggerFilterOperator.Equals, "0")),
            Graph = new FrontedNodeGraph
            {
                Nodes =
                [
                    IfNode("Event.PlayerIndex", TriggerFilterOperator.Equals, "0"),
                    IfNode("StartEvent.PreviousIndexesText", TriggerFilterOperator.Equals, "[0]")
                ]
            }
        };
        var payload = _service.Copy("BpWindow", source, behavior);

        var result = _service.Paste(payload, target, new FrontedBehaviorDocument());

        Assert.True(result.Succeeded);
        Assert.Equal("2", result.Behavior!.Trigger!.Filters[0].Right);
        Assert.Equal("2", result.Behavior.Trigger.Filters[1].Right);
        Assert.Equal("2", result.Behavior.Trigger.Filters[2].Right);
        Assert.Equal("[2, 1]", result.Behavior.TransitionTrigger!.Filters[0].Right);
        Assert.Equal("[2]", result.Behavior.TransitionTrigger.Filters[1].Right);
        Assert.Equal("2", result.Behavior.TransitionTrigger.Filters[2].Right);
        Assert.Equal("2", result.Behavior.Graph.Nodes[0].Properties["Right"].GetString());
        Assert.Equal("[2]", result.Behavior.Graph.Nodes[1].Properties["Right"].GetString());
    }

    [Fact]
    public void Paste_GeneratesBehaviorGuidAndUniqueBehaviorIdsForMultipleTargets()
    {
        var source = ImageItem("SurPick0", Guid.NewGuid());
        var sourceBehavior = BehaviorWithTargets($"guid:{source.Config.BehaviorGuid}");
        sourceBehavior.Name = "Fade";
        var payload = _service.Copy("BpWindow", source, sourceBehavior);
        var document = new FrontedBehaviorDocument();
        var targets = new[]
        {
            ImageItem("SurPick1", Guid.Empty),
            ImageItem("SurPick2", Guid.NewGuid()),
            ImageItem("SurPick3", Guid.NewGuid())
        };

        var results = targets.Select(target => _service.Paste(payload, target, document)).ToArray();

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.All(targets, target => Assert.NotEqual(Guid.Empty, target.Config.BehaviorGuid));
        Assert.Equal(3, results.Select(result => result.Behavior!.BehaviorId).Distinct().Count());
        Assert.All(results, result => Assert.NotEqual(sourceBehavior.BehaviorId, result.Behavior!.BehaviorId));
        Assert.Equal(
            targets.Select(target => $"guid:{target.Config.BehaviorGuid}"),
            results.Select(result => Assert.Single(Targets(result.Behavior!))));
    }

    [Fact]
    public void CopyAndPaste_DoNotMutateClipboardBehavior()
    {
        var source = ImageItem("SurPick0", Guid.NewGuid());
        var target = ImageItem("SurPick1", Guid.NewGuid());
        var behavior = BehaviorWithTargets($"guid:{source.Config.BehaviorGuid}");
        var payload = _service.Copy("BpWindow", source, behavior);
        var clipboardId = payload.Behavior.BehaviorId;

        _service.Paste(payload, target, new FrontedBehaviorDocument());

        Assert.Equal(clipboardId, payload.Behavior.BehaviorId);
        Assert.Equal($"guid:{source.Config.BehaviorGuid}", Assert.Single(Targets(payload.Behavior)));
    }

    [Fact]
    public void SemanticResolver_PrefersExplicitThenBindingThenName()
    {
        var resolver = new FrontedBehaviorControlSemanticResolver();

        Assert.Equal(2, resolver.Resolve(new FrontedControlDesignItem
        {
            Name = "Talent9",
            Config = new TalentTraitDisplayControlConfig { PlayerIndex = 2, BindingPath = "Players[4]" }
        }).Index);
        Assert.Equal(4, resolver.Resolve(new FrontedControlDesignItem
        {
            Name = "Pick9",
            Config = new ImageFrontedControlConfig { BindingPath = "CurrentGame.SurPlayerList[4].Picture" }
        }).Index);
        Assert.Equal(9, resolver.Resolve(new FrontedControlDesignItem
        {
            Name = "Pick9",
            Config = new ImageFrontedControlConfig()
        }).Index);
    }

    private static FrontedControlDesignItem ImageItem(
        string name,
        Guid guid,
        bool pickingBorder = false,
        bool lockable = false) =>
        new()
        {
            Name = name,
            Config = new ImageFrontedControlConfig
            {
                BehaviorGuid = guid,
                PickingBorderAvailable = pickingBorder,
                Lockable = lockable
            }
        };

    private static FrontedBehavior BehaviorWithTargets(params string[] targets) =>
        new()
        {
            Graph = new FrontedNodeGraph
            {
                Nodes = targets.Select((target, index) => new FrontedNode
                {
                    NodeType = index % 2 == 0 ? "action.setProperty" : "future.animation.action",
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["Target"] = JsonSerializer.SerializeToElement(target)
                    }
                }).ToList()
            }
        };

    private static IEnumerable<string> Targets(FrontedBehavior behavior) =>
        behavior.Graph.Nodes.Select(node => node.Properties["Target"].GetString()!);

    private static TriggerDescriptor Trigger(
        params (string Left, TriggerFilterOperator Operator, string Right)[] filters) =>
        new()
        {
            Filters = filters.Select(filter => new TriggerFilter
            {
                Left = filter.Left,
                Operator = filter.Operator,
                Right = filter.Right
            }).ToList()
        };

    private static FrontedNode IfNode(string left, TriggerFilterOperator @operator, string right) =>
        new()
        {
            NodeType = "flow.if",
            Properties = new Dictionary<string, JsonElement>
            {
                ["Left"] = JsonSerializer.SerializeToElement(left),
                ["Operator"] = JsonSerializer.SerializeToElement(@operator.ToString()),
                ["Right"] = JsonSerializer.SerializeToElement(right)
            }
        };

    private sealed class TestLocalizationService : FrontedDesignerLocalizationService
    {
        public override string GetDesignerText(string key, string fallback) => $"localized:{key}";
    }
}
