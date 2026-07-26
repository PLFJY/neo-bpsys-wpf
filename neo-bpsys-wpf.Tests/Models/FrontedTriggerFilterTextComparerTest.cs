using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Collections.Generic;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedTriggerFilterTextComparerTest
{
    [Fact]
    public void Equals_IsOrdinalIgnoreCase() =>
        Assert.True(FrontedTriggerFilterTextComparer.Evaluate("Alpha", TriggerFilterOperator.Equals, "alpha"));

    [Fact]
    public void Contains_IsOrdinalIgnoreCase() =>
        Assert.True(FrontedTriggerFilterTextComparer.Evaluate("Alpha Beta", TriggerFilterOperator.Contains, "BETA"));

    [Fact]
    public void NotContains_InvertsContains() =>
        Assert.True(FrontedTriggerFilterTextComparer.Evaluate("Alpha", TriggerFilterOperator.NotContains, "beta"));

    [Fact]
    public void NumericGreaterThan_WhenBothParse() =>
        Assert.True(FrontedTriggerFilterTextComparer.Evaluate("10.5", TriggerFilterOperator.GreaterThan, "2"));

    [Fact]
    public void FallbackStringCompare_WhenNotNumeric() =>
        Assert.True(FrontedTriggerFilterTextComparer.Evaluate("beta", TriggerFilterOperator.GreaterThan, "alpha"));

    [Fact]
    public void PayloadFormatter_UsesStableInvariantValues()
    {
        Assert.Equal("PickSur", FrontedBehaviorPayloadValueFormatter.Format(GameAction.PickSur));
        Assert.Equal("[0]", FrontedBehaviorPayloadValueFormatter.Format(new List<int> { 0 }));
        Assert.Equal("[1, 2]", FrontedBehaviorPayloadValueFormatter.Format(new List<int> { 1, 2 }));
        Assert.Equal(string.Empty, FrontedBehaviorPayloadValueFormatter.Format(null));
        Assert.Equal("true", FrontedBehaviorPayloadValueFormatter.Format(true));
    }
}
