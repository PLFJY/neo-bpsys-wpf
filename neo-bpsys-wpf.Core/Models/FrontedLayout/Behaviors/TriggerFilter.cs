namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class TriggerFilter
{
    public string Left { get; set; } = string.Empty;

    public TriggerFilterOperator Operator { get; set; } = TriggerFilterOperator.Equals;

    public string? Right { get; set; }

    public TriggerFilterValueKind RightValueKind { get; set; } = TriggerFilterValueKind.Literal;
}

