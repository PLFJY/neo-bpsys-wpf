using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
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
}
