using System;
using System.Threading;
using System.Threading.Tasks;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.ProductTour.Controls;
using neo_bpsys_wpf.Tests.Infrastructure;
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

    [Fact]
    public async Task Cancel_ShouldEndAnActiveDialogueAsCanceled()
    {
        await WpfTestThread.RunAsync(async () =>
        {
            var overlay = new DialogueOverlay
            {
                TypewriterInterval = TimeSpan.FromSeconds(1)
            };
            var showTask = overlay.ShowAsync("Speaker", ["A dialogue that is still running."], CancellationToken.None);

            overlay.Cancel();

            Assert.Equal(TutorialRunResult.Canceled, await showTask);
        });
    }
}
