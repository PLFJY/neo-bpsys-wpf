using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Collections.Generic;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class FrontedBehaviorTriggerEvaluatorTest
{
    private readonly FrontedBehaviorTriggerEvaluator _evaluator = new(NullLogger<FrontedBehaviorTriggerEvaluator>.Instance);

    [Fact]
    public void TriggerEvaluator_EventTypeMustMatch()
    {
        var trigger = new TriggerDescriptor { EventType = "ScoreChanged" };
        var matchingEvent = new FrontedBehaviorEvent { EventType = "ScoreChanged" };
        var nonMatchingEvent = new FrontedBehaviorEvent { EventType = "TimerElapsed" };

        Assert.True(_evaluator.Evaluate(trigger, matchingEvent));
        Assert.False(_evaluator.Evaluate(trigger, nonMatchingEvent));
    }

    [Fact]
    public void TriggerEvaluator_AllFiltersMustPass()
    {
        var trigger = new TriggerDescriptor
        {
            EventType = "ScoreChanged",
            Filters =
            [
                new TriggerFilter { Left = "Event.Team", Operator = TriggerFilterOperator.Equals, Right = "Home" },
                new TriggerFilter { Left = "Event.Score", Operator = TriggerFilterOperator.GreaterThan, Right = "10" }
            ]
        };
        var allPassEvent = new FrontedBehaviorEvent
        {
            EventType = "ScoreChanged",
            Payload = new Dictionary<string, object?>
            {
                ["Team"] = "Home",
                ["Score"] = 15
            }
        };
        var oneFailsEvent = new FrontedBehaviorEvent
        {
            EventType = "ScoreChanged",
            Payload = new Dictionary<string, object?>
            {
                ["Team"] = "Home",
                ["Score"] = 5
            }
        };

        Assert.True(_evaluator.Evaluate(trigger, allPassEvent));
        Assert.False(_evaluator.Evaluate(trigger, oneFailsEvent));
    }

    [Fact]
    public void TriggerEvaluator_EventPayload_Equals()
    {
        var trigger = new TriggerDescriptor
        {
            EventType = "StatusChanged",
            Filters =
            [
                new TriggerFilter { Left = "Event.X", Operator = TriggerFilterOperator.Equals, Right = "value" }
            ]
        };
        var matchingEvent = new FrontedBehaviorEvent
        {
            EventType = "StatusChanged",
            Payload = new Dictionary<string, object?> { ["X"] = "value" }
        };
        var nonMatchingEvent = new FrontedBehaviorEvent
        {
            EventType = "StatusChanged",
            Payload = new Dictionary<string, object?> { ["X"] = "other" }
        };

        Assert.True(_evaluator.Evaluate(trigger, matchingEvent));
        Assert.False(_evaluator.Evaluate(trigger, nonMatchingEvent));
    }

    [Fact]
    public void TriggerEvaluator_EventPayload_NumericCompare()
    {
        var trigger = new TriggerDescriptor
        {
            EventType = "ScoreChanged",
            Filters =
            [
                new TriggerFilter { Left = "Event.Score", Operator = TriggerFilterOperator.GreaterThan, Right = "100" }
            ]
        };
        var highScoreEvent = new FrontedBehaviorEvent
        {
            EventType = "ScoreChanged",
            Payload = new Dictionary<string, object?> { ["Score"] = 150 }
        };
        var lowScoreEvent = new FrontedBehaviorEvent
        {
            EventType = "ScoreChanged",
            Payload = new Dictionary<string, object?> { ["Score"] = 50 }
        };

        Assert.True(_evaluator.Evaluate(trigger, highScoreEvent));
        Assert.False(_evaluator.Evaluate(trigger, lowScoreEvent));
    }

    [Fact]
    public void TriggerEvaluator_Contains_NotContains()
    {
        var trigger = new TriggerDescriptor
        {
            EventType = "MessageReceived",
            Filters =
            [
                new TriggerFilter { Left = "Event.Text", Operator = TriggerFilterOperator.Contains, Right = "World" }
            ]
        };
        var containsEvent = new FrontedBehaviorEvent
        {
            EventType = "MessageReceived",
            Payload = new Dictionary<string, object?> { ["Text"] = "Hello World" }
        };
        var notContainsEvent = new FrontedBehaviorEvent
        {
            EventType = "MessageReceived",
            Payload = new Dictionary<string, object?> { ["Text"] = "Hello There" }
        };

        Assert.True(_evaluator.Evaluate(trigger, containsEvent));
        Assert.False(_evaluator.Evaluate(trigger, notContainsEvent));

        // NotContains
        var notContainsTrigger = new TriggerDescriptor
        {
            EventType = "MessageReceived",
            Filters =
            [
                new TriggerFilter { Left = "Event.Text", Operator = TriggerFilterOperator.NotContains, Right = "World" }
            ]
        };

        Assert.False(_evaluator.Evaluate(notContainsTrigger, containsEvent));
        Assert.True(_evaluator.Evaluate(notContainsTrigger, notContainsEvent));
    }

    [Fact]
    public void TriggerEvaluator_MissingPayload_FailsSafely()
    {
        var trigger = new TriggerDescriptor
        {
            EventType = "DataChanged",
            Filters =
            [
                new TriggerFilter { Left = "Event.Nonexistent", Operator = TriggerFilterOperator.Equals, Right = "value" }
            ]
        };
        var emptyPayloadEvent = new FrontedBehaviorEvent
        {
            EventType = "DataChanged",
            Payload = new Dictionary<string, object?>()
        };

        // Should return false without throwing
        var result = _evaluator.Evaluate(trigger, emptyPayloadEvent);
        Assert.False(result);
    }
}
