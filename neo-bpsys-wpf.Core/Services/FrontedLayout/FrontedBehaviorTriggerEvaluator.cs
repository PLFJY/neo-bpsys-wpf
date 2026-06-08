using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Evaluates whether a <see cref="FrontedBehaviorEvent" /> matches a <see cref="TriggerDescriptor" />,
/// including all filter conditions against event payload and self-tags.
/// </summary>
public sealed class FrontedBehaviorTriggerEvaluator
{
    private readonly ILogger<FrontedBehaviorTriggerEvaluator> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FrontedBehaviorTriggerEvaluator" />.
    /// </summary>
    public FrontedBehaviorTriggerEvaluator(ILogger<FrontedBehaviorTriggerEvaluator>? logger = null)
    {
        _logger = logger ?? NullLogger<FrontedBehaviorTriggerEvaluator>.Instance;
    }

    /// <summary>
    /// Evaluates whether the given <paramref name="trigger" /> matches the <paramref name="behaviorEvent" />.
    /// </summary>
    /// <param name="trigger">The trigger descriptor with event type and optional filters.</param>
    /// <param name="behaviorEvent">The published behavior event.</param>
    /// <param name="selfTags">Self-tags from the control configuration. Can be null or empty.</param>
    /// <returns>True if the event type matches and all filters pass; otherwise false.</returns>
    public bool Evaluate(
        TriggerDescriptor trigger,
        FrontedBehaviorEvent behaviorEvent,
        IReadOnlyDictionary<string, string>? selfTags)
    {
        if (trigger is null)
        {
            _logger.LogWarning("TriggerEvaluator: trigger is null.");
            return false;
        }

        // 1. EventType must match
        if (!string.Equals(trigger.EventType, behaviorEvent.EventType, StringComparison.Ordinal))
        {
            return false;
        }

        // 2. No filters means immediate match
        if (trigger.Filters is null || trigger.Filters.Count == 0)
        {
            return true;
        }

        // 3. All filters must pass
        foreach (var filter in trigger.Filters)
        {
            if (!EvaluateFilter(filter, behaviorEvent, selfTags))
            {
                return false;
            }
        }

        return true;
    }

    private bool EvaluateFilter(
        TriggerFilter filter,
        FrontedBehaviorEvent behaviorEvent,
        IReadOnlyDictionary<string, string>? selfTags)
    {
        try
        {
            var leftValue = ResolveLeftValue(filter.Left, behaviorEvent, selfTags);
            var rightValue = ResolveOperandValue(filter.Right, behaviorEvent, selfTags);
            return FrontedTriggerFilterTextComparer.Evaluate(leftValue, filter.Operator, rightValue?.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "TriggerEvaluator: filter evaluation threw for Left={Left}, Operator={Op}, Right={Right}. Treating as non-match.",
                filter.Left, filter.Operator, filter.Right);
            return false;
        }
    }

    private static object? ResolveLeftValue(
        string left,
        FrontedBehaviorEvent behaviorEvent,
        IReadOnlyDictionary<string, string>? selfTags) =>
        ResolveOperandValue(left, behaviorEvent, selfTags);

    private static object? ResolveOperandValue(
        string? text,
        FrontedBehaviorEvent behaviorEvent,
        IReadOnlyDictionary<string, string>? selfTags)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.StartsWith("Event.", StringComparison.Ordinal))
        {
            var key = text["Event.".Length..];
            return behaviorEvent.Payload.TryGetValue(key, out var value) ? value : null;
        }

        if (text.StartsWith("SelfTag.", StringComparison.Ordinal))
        {
            var key = text["SelfTag.".Length..];
            if (selfTags is not null && selfTags.TryGetValue(key, out var tagValue))
            {
                return tagValue;
            }

            return null;
        }

        // Literal text
        return text;
    }
}
