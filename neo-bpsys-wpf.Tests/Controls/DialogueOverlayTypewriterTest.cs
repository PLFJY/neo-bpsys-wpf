using System;
using neo_bpsys_wpf.ProductTour.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

public sealed class DialogueOverlayTypewriterTest
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(27, 0)]
    [InlineData(28, 1)]
    [InlineData(84, 3)]
    [InlineData(280, 10)]
    [InlineData(560, 10)]
    public void VisibleCharacterCountFollowsElapsedTime(int elapsedMilliseconds, int expectedCount)
    {
        var count = DialogueOverlay.CalculateVisibleCharacterCount(
            lineLength: 10,
            elapsed: TimeSpan.FromMilliseconds(elapsedMilliseconds),
            characterInterval: TimeSpan.FromMilliseconds(28));

        Assert.Equal(expectedCount, count);
    }
}
