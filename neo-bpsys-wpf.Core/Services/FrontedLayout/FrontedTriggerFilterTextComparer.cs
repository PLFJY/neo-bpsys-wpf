using System.Globalization;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public static class FrontedTriggerFilterTextComparer
{
    public static bool Evaluate(object? left, TriggerFilterOperator filterOperator, string? right)
    {
        var leftText = FrontedBehaviorPayloadValueFormatter.Format(left);
        var rightText = right ?? string.Empty;

        return filterOperator switch
        {
            TriggerFilterOperator.Equals => string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase),
            TriggerFilterOperator.NotEquals => !string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase),
            TriggerFilterOperator.Contains => leftText.Contains(rightText, StringComparison.OrdinalIgnoreCase),
            TriggerFilterOperator.NotContains => !leftText.Contains(rightText, StringComparison.OrdinalIgnoreCase),
            TriggerFilterOperator.GreaterThan => Compare(leftText, rightText) > 0,
            TriggerFilterOperator.GreaterThanOrEqual => Compare(leftText, rightText) >= 0,
            TriggerFilterOperator.LessThan => Compare(leftText, rightText) < 0,
            TriggerFilterOperator.LessThanOrEqual => Compare(leftText, rightText) <= 0,
            TriggerFilterOperator.Exists => left is not null,
            _ => false
        };
    }

    private static int Compare(string left, string right)
    {
        if (decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out var leftNumber)
            && decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
