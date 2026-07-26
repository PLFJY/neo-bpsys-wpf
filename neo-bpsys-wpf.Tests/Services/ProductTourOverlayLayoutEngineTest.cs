using System;
using System.Collections.Generic;
using System.Windows;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.ProductTour.Controls;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 针对 <see cref="ProductTourOverlayLayoutEngine"/> 的纯单元测试，验证
/// 非重叠、姿势选择、偏好处理与回退行为。
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

    [Theory]
    [InlineData(ProductTourPlacement.LeftTop, "LeftTop")]
    [InlineData(ProductTourPlacement.LeftBottom, "LeftBottom")]
    [InlineData(ProductTourPlacement.RightTop, "RightTop")]
    [InlineData(ProductTourPlacement.RightBottom, "RightBottom")]
    public void OverlayLayout_ShouldRespectSideAlignedPreferredPlacements(ProductTourPlacement placement, string label)
    {
        var safe = new Rect(0, 0, 1200, 800);
        var spot = new Rect(500, 300, 100, 100);
        var result = Arrange(safe, spot, new Size(200, 150), new Size(80, 80),
            preferredCard: placement);

        switch (placement)
        {
            case ProductTourPlacement.LeftTop:
                Assert.True(result.CardRect.Right <= spot.Left,
                    $"{label}: card should be to the left of spotlight. CardRight={result.CardRect.Right}, SpotLeft={spot.Left}");
                Assert.True(Math.Abs(result.CardRect.Top - spot.Top) < 1,
                    $"{label}: card top should align with spotlight top. CardTop={result.CardRect.Top}, SpotTop={spot.Top}");
                break;
            case ProductTourPlacement.LeftBottom:
                Assert.True(result.CardRect.Right <= spot.Left,
                    $"{label}: card should be to the left of spotlight.");
                Assert.True(Math.Abs(result.CardRect.Bottom - spot.Bottom) < 1,
                    $"{label}: card bottom should align with spotlight bottom. CardBottom={result.CardRect.Bottom}, SpotBottom={spot.Bottom}");
                break;
            case ProductTourPlacement.RightTop:
                Assert.True(result.CardRect.Left >= spot.Right,
                    $"{label}: card should be to the right of spotlight. CardLeft={result.CardRect.Left}, SpotRight={spot.Right}");
                Assert.True(Math.Abs(result.CardRect.Top - spot.Top) < 1,
                    $"{label}: card top should align with spotlight top. CardTop={result.CardRect.Top}, SpotTop={spot.Top}");
                break;
            case ProductTourPlacement.RightBottom:
                Assert.True(result.CardRect.Left >= spot.Right,
                    $"{label}: card should be to the right of spotlight.");
                Assert.True(Math.Abs(result.CardRect.Bottom - spot.Bottom) < 1,
                    $"{label}: card bottom should align with spotlight bottom. CardBottom={result.CardRect.Bottom}, SpotBottom={spot.Bottom}");
                break;
        }
    }

    [Fact]
    public void OverlayLayout_ShouldUsePreferredPlacementAsHardConstraint()
    {
        var safe = new Rect(0, 0, 1200, 800);
        var spot = new Rect(500, 350, 100, 100);
        var result = Arrange(safe, spot, new Size(200, 150), new Size(80, 80),
            preferredCard: ProductTourPlacement.Bottom);

        Assert.True(Math.Abs(result.CardRect.Top - (spot.Bottom + 16)) < 1,
            $"Bottom placement should place card directly below spotlight with gap. CardTop={result.CardRect.Top}, Expected={spot.Bottom + 16}");
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
    public void OverlayLayout_ShouldRespectPreferredPlacementWhenAllCandidatesOverlapSpotlight()
    {
        // safe area 太小，所有方向的候选都和 spotlight 重叠（无路可走）。
        var safe = new Rect(0, 0, 300, 200);
        var spot = new Rect(50, 50, 200, 100);
        var cardSize = new Size(100, 80);
        var result = Arrange(safe, spot, cardSize, new Size(40, 40),
            preferredCard: ProductTourPlacement.Right);

        // Right 候选原始 X=266，被 ClampToSafe 推到 X=200（贴着 safe area 右边）。
        Assert.True(Math.Abs(result.CardRect.Left - 200) < 1,
            $"Right placement should pin card to safe area right edge when all candidates overlap. CardLeft={result.CardRect.Left}");
        Assert.True(result.IsFallback, "Should be a fallback result since card overlaps spotlight.");
    }

    [Fact]
    public void OverlayLayout_ShouldRespectPreferredLeftPlacementWhenAllCandidatesOverlapSpotlight()
    {
        var safe = new Rect(0, 0, 300, 200);
        var spot = new Rect(50, 50, 200, 100);
        var cardSize = new Size(100, 80);
        var result = Arrange(safe, spot, cardSize, new Size(40, 40),
            preferredCard: ProductTourPlacement.Left);

        // Left 候选原始 X=-66，被 ClampToSafe 推到 X=0（贴着 safe area 左边）。
        Assert.True(Math.Abs(result.CardRect.Left) < 1,
            $"Left placement should pin card to safe area left edge when all candidates overlap. CardLeft={result.CardRect.Left}");
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
