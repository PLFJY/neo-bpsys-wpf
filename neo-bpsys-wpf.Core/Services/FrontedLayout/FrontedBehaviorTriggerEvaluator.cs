using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 评估 <see cref="FrontedBehaviorEvent" /> 是否匹配 <see cref="TriggerDescriptor" />，
/// 包括对事件负载的所有过滤条件。
/// </summary>
public sealed class FrontedBehaviorTriggerEvaluator
{
    private readonly ILogger<FrontedBehaviorTriggerEvaluator> _logger;

    /// <summary>
    /// 初始化 <see cref="FrontedBehaviorTriggerEvaluator" /> 的新实例。
    /// </summary>
    public FrontedBehaviorTriggerEvaluator(ILogger<FrontedBehaviorTriggerEvaluator>? logger = null)
    {
        _logger = logger ?? NullLogger<FrontedBehaviorTriggerEvaluator>.Instance;
    }

    /// <summary>
    /// 评估给定的 <paramref name="trigger" /> 是否匹配 <paramref name="behaviorEvent" />。
    /// </summary>
    /// <param name="trigger">包含事件类型和可选过滤条件的触发器描述符。</param>
    /// <param name="behaviorEvent">已发布的行为事件。</param>
    /// <returns>当事件类型匹配且所有过滤条件通过时返回 true；否则返回 false。</returns>
    public bool Evaluate(
        TriggerDescriptor trigger,
        FrontedBehaviorEvent behaviorEvent)
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
            if (!EvaluateFilter(filter, behaviorEvent))
            {
                return false;
            }
        }

        return true;
    }

    private bool EvaluateFilter(
        TriggerFilter filter,
        FrontedBehaviorEvent behaviorEvent)
    {
        try
        {
            var leftValue = ResolveLeftValue(filter.Left, behaviorEvent);
            var rightValue = ResolveOperandValue(filter.Right, behaviorEvent);
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
        FrontedBehaviorEvent behaviorEvent) =>
        ResolveOperandValue(left, behaviorEvent);

    private static object? ResolveOperandValue(
        string? text,
        FrontedBehaviorEvent behaviorEvent)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.StartsWith("Event.", StringComparison.Ordinal))
        {
            var key = text["Event.".Length..];
            if (behaviorEvent.Payload.TryGetValue(key, out var value)
                || behaviorEvent.Payload.TryGetValue(text, out value))
            {
                return value;
            }

            return null;
        }

        // Literal text
        return text;
    }
}
