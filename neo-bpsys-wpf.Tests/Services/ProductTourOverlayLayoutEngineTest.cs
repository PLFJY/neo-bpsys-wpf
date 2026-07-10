using System.Collections.Generic;
using System.Windows;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.ProductTour.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// Pure unit tests for <see cref="ProductTourOverlayLayoutEngine"/> that verify
/// non-overlap, pose selection, preference handling and fallback behavior.
/// </summary>
public sealed class ProductTourOverlayLayoutEngineTest
{
    private static ProductTourOverlayLayoutResult Arrange(
        Rect safeArea,
        Rect spotlight,
        Size cardSize,
        Size aliceSize,
        ProductTourPlacement? preferredCard = null,
        ProductTourAvatarPlacement? preferredAlice = null,
        double gap = 16,
        bool aliceVisible = true)
    {
        var engine = new ProductTourOverlayLayoutEngine();
        return engine.Arrange(new ProductTourOverlayLayoutRequest
        {
            SafeArea = safeArea,
            SpotlightRect = spotlight,
            CardDesiredSize = cardSize,
            AliceDesiredSize = aliceSize,
            PreferredCardPlacement = preferredCard,
            PreferredAlicePlacement = preferredAlice,
            MinimumGap = gap,
            AliceVisible = aliceVisible
        });
    }

    private static ProductTourOverlayLayoutResult ArrangeWithObstacles(
        Rect safeArea,
        Rect spotlight,
        Size cardSize,
        Size aliceSize,
        IReadOnlyList<Rect> obstacles,
        ProductTourPlacement? preferredCard = null,
        double gap = 16)
    {
        var engine = new ProductTourOverlayLayoutEngine();
        return engine.Arrange(new ProductTourOverlayLayoutRequest
        {
            SafeArea = safeArea,
            SpotlightRect = spotlight,
            CardDesiredSize = cardSize,
            AliceDesiredSize = aliceSize,
            PreferredCardPlacement = preferredCard,
            MinimumGap = gap,
            AliceVisible = true,
            Obstacles = obstacles
        });
    }

    [Fact]
    public void OverlayLayout_ShouldPlaceCardOutsideSpotlight()
    {
        var safe = new Rect(0, 0, 1000, 800);
        var spot = new Rect(400, 300, 200, 200);
        var result = Arrange(safe, spot, new Size(300, 200), new Size(80, 80));

        var inflated = Rect.Inflate(spot, new Size(16, 16));
        Assert.False(StrictlyOverlaps(result.CardRect, inflated));
    }

    [Fact]
    public void OverlayLayout_ShouldPlaceAliceOutsideSpotlight()
    {
        var safe = new Rect(0, 0, 1000, 800);
        var spot = new Rect(400, 300, 200, 200);
        var result = Arrange(safe, spot, new Size(300, 200), new Size(80, 80));

        Assert.True(result.AliceVisible);
        var inflated = Rect.Inflate(spot, new Size(16, 16));
        Assert.False(StrictlyOverlaps(result.AliceRect, inflated));
    }

    [Fact]
    public void OverlayLayout_ShouldNotOverlapCardAndAlice()
    {
        var safe = new Rect(0, 0, 1000, 800);
        var spot = new Rect(400, 300, 200, 200);
        var result = Arrange(safe, spot, new Size(300, 200), new Size(80, 80));

        if (!result.AliceVisible)
        {
            return;
        }

        var inflated = Rect.Inflate(result.AliceRect, new Size(16, 16));
        Assert.False(StrictlyOverlaps(result.CardRect, inflated));
    }

    [Fact]
    public void OverlayLayout_ShouldChooseAlicePosePointingToSpotlight()
    {
        var aliceLeftOfSpot = new Point(100, 400);
        var spotCenter = new Point(500, 400);
        var pose = ProductTourOverlayLayoutEngine.ChooseAlicePose(aliceLeftOfSpot, spotCenter);
        Assert.True(pose is TutorialAvatarPose.RightTop or TutorialAvatarPose.RightBottom);

        var aliceRightOfSpot = new Point(900, 400);
        pose = ProductTourOverlayLayoutEngine.ChooseAlicePose(aliceRightOfSpot, spotCenter);
        Assert.True(pose is TutorialAvatarPose.LeftTop or TutorialAvatarPose.LeftBottom);

        var aliceAboveSpot = new Point(500, 100);
        pose = ProductTourOverlayLayoutEngine.ChooseAlicePose(aliceAboveSpot, spotCenter);
        Assert.True(pose is TutorialAvatarPose.LeftBottom or TutorialAvatarPose.RightBottom);

        var aliceBelowSpot = new Point(500, 700);
        pose = ProductTourOverlayLayoutEngine.ChooseAlicePose(aliceBelowSpot, spotCenter);
        Assert.True(pose is TutorialAvatarPose.LeftTop or TutorialAvatarPose.RightTop);
    }

    [Fact]
    public void OverlayLayout_ShouldRespectPreferredPlacementWhenValid()
    {
        var safe = new Rect(0, 0, 1200, 800);
        var spot = new Rect(200, 300, 100, 100);
        var result = Arrange(safe, spot, new Size(200, 150), new Size(80, 80),
            preferredCard: ProductTourPlacement.Right);

        Assert.True(result.CardRect.Left >= spot.Right,
            $"Card should be to the right of spotlight. CardLeft={result.CardRect.Left}, SpotRight={spot.Right}");
    }

    [Fact]
    public void OverlayLayout_ShouldOverridePreferredPlacementWhenItOverlapsSpotlight()
    {
        var safe = new Rect(0, 0, 500, 400);
        var spot = new Rect(350, 150, 140, 100);
        var result = Arrange(safe, spot, new Size(200, 150), new Size(60, 60),
            preferredCard: ProductTourPlacement.Right);

        var inflated = Rect.Inflate(spot, new Size(16, 16));
        Assert.False(StrictlyOverlaps(result.CardRect, inflated),
            "Card must not overlap spotlight even when preferred placement would overlap.");
    }

    [Fact]
    public void OverlayLayout_ShouldFallbackWithoutCrashOnSmallSafeArea()
    {
        var safe = new Rect(0, 0, 50, 50);
        var spot = new Rect(5, 5, 40, 40);
        var result = Arrange(safe, spot, new Size(200, 150), new Size(60, 60));

        Assert.True(result.IsFallback);
    }

    [Fact]
    public void OverlayLayout_ShouldKeepCardReadableInFallback()
    {
        var safe = new Rect(0, 0, 50, 50);
        var spot = new Rect(5, 5, 40, 40);
        var result = Arrange(safe, spot, new Size(200, 150), new Size(60, 60));

        Assert.True(result.CardRect.Width > 0, "Card rect should have positive width in fallback.");
        Assert.True(result.CardRect.Height > 0, "Card rect should have positive height in fallback.");
    }

    [Fact]
    public void OverlayLayout_ShouldNotShrinkCandidateCausingSpotlightOverlap()
    {
        var safe = new Rect(12, 12, 956, 776);
        var spot = new Rect(2, 42, 376, 120);
        var result = Arrange(safe, spot, new Size(380, 200), new Size(96, 96));

        var inflated = Rect.Inflate(spot, new Size(16, 16));
        Assert.False(StrictlyOverlaps(result.CardRect, inflated),
            "Card must not overlap spotlight when target is on the left side of the window.");
        Assert.True(result.CardRect.Width >= 380,
            "Card rect should preserve full width, not be shrunk by ClampToSafe.");
    }

    [Fact]
    public void OverlayLayout_ShouldAvoidObstacleRectangles()
    {
        var safe = new Rect(12, 12, 956, 776);
        var spot = new Rect(300, 50, 200, 48);
        var skipButton = new Rect(866, 20, 94, 32);
        var result = ArrangeWithObstacles(safe, spot, new Size(380, 200), new Size(96, 96), [skipButton]);

        var skipInflated = Rect.Inflate(skipButton, new Size(16, 16));
        Assert.False(StrictlyOverlaps(result.CardRect, skipInflated),
            "Card must not overlap obstacle rectangles like the skip button.");
        Assert.False(StrictlyOverlaps(result.AliceRect, skipInflated),
            "Avatar must not overlap obstacle rectangles like the skip button.");

        var spotInflated = Rect.Inflate(spot, new Size(16, 16));
        Assert.False(StrictlyOverlaps(result.CardRect, spotInflated),
            "Card must still avoid spotlight when obstacles are present.");
    }

    private static bool StrictlyOverlaps(Rect a, Rect b) =>
        a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;
}
