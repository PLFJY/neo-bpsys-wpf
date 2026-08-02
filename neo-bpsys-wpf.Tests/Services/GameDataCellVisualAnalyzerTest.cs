extern alias smartbp;

using OpenCvSharp;
using Xunit;
using GameDataCellVisualAnalyzer = smartbp::neo_bpsys_wpf.Services.GameDataCellVisualAnalyzer;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class GameDataCellVisualAnalyzerTest
{
    [Fact]
    public void TryDetectDigitOne_AcceptsCenteredTallNarrowStroke()
    {
        using var cell = CreateCell();
        Cv2.Line(cell, new Point(38, 6), new Point(38, 28), Scalar.White, 3);
        Cv2.Line(cell, new Point(34, 10), new Point(38, 6), Scalar.White, 2);

        var detected = GameDataCellVisualAnalyzer.TryDetectDigitOne(cell, out var evidence);

        Assert.True(detected);
        Assert.NotNull(evidence);
        Assert.True(evidence.AspectRatio > 1.35);
    }

    [Fact]
    public void TryDetectDigitOne_RejectsHorizontalDash()
    {
        using var cell = CreateCell();
        Cv2.Line(cell, new Point(29, 18), new Point(47, 18), Scalar.White, 3);

        Assert.False(GameDataCellVisualAnalyzer.TryDetectDigitOne(cell, out _));
    }

    [Fact]
    public void TryDetectDigitOne_RejectsOffCenterVerticalBackgroundStroke()
    {
        using var cell = CreateCell();
        Cv2.Line(cell, new Point(10, 5), new Point(10, 28), Scalar.White, 3);

        Assert.False(GameDataCellVisualAnalyzer.TryDetectDigitOne(cell, out _));
    }

    private static Mat CreateCell() => new(new Size(76, 34), MatType.CV_8UC3, new Scalar(30, 30, 30));
}
