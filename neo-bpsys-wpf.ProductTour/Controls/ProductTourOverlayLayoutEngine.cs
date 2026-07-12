using System.Windows;

namespace neo_bpsys_wpf.ProductTour.Controls;

/// <summary>
/// 描述一次提供给 <see cref="ProductTourOverlayLayoutEngine"/> 的遮罩布局尝试。
/// </summary>
public sealed class ProductTourOverlayLayoutRequest
{
    /// <summary>获取遮罩坐标系下的安全区域。</summary>
    public required Rect SafeArea { get; init; }

    /// <summary>获取应用聚光灯内边距后解析得到的聚光灯矩形。</summary>
    public required Rect SpotlightRect { get; init; }

    /// <summary>获取期望的卡片尺寸。</summary>
    public required Size CardDesiredSize { get; init; }

    /// <summary>获取期望的引导头像尺寸。</summary>
    public required Size AliceDesiredSize { get; init; }

    /// <summary>获取首选的卡片放置位置，作为偏好而非强制约束处理。</summary>
    public ProductTourPlacement? PreferredCardPlacement { get; init; }

    /// <summary>获取首选的引导头像放置位置，作为偏好处理。</summary>
    public ProductTourAvatarPlacement? PreferredAlicePlacement { get; init; }

    /// <summary>获取聚光灯、卡片和头像之间的最小间距。</summary>
    public double MinimumGap { get; init; } = 16;

    /// <summary>获取已放置元素与安全区域边缘之间保留的内边距。</summary>
    public double EdgePadding { get; init; } = 16;

    /// <summary>获取一个值，指示是否应将引导头像纳入放置考虑。</summary>
    public bool AliceVisible { get; init; } = true;

    /// <summary>获取额外的障碍物矩形（如遮罩跳过按钮），卡片和头像必须避开这些区域。</summary>
    public IReadOnlyList<Rect> Obstacles { get; init; } = [];
}

/// <summary>
/// 描述某个聚光灯步骤中解析得到的卡片和引导头像布局。
/// </summary>
public sealed class ProductTourOverlayLayoutResult
{
    /// <summary>获取解析得到的卡片位置。</summary>
    public required Point CardPosition { get; init; }

    /// <summary>获取解析得到的引导头像位置。</summary>
    public required Point AlicePosition { get; init; }

    /// <summary>获取指向聚光灯区域的引导头像姿态。</summary>
    public required TutorialAvatarPose AlicePose { get; init; }

    /// <summary>获取解析得到的卡片矩形。</summary>
    public required Rect CardRect { get; init; }

    /// <summary>获取解析得到的引导头像矩形。</summary>
    public required Rect AliceRect { get; init; }

    /// <summary>获取一个值，指示该结果是否为降级的回退放置。</summary>
    public required bool IsFallback { get; init; }

    /// <summary>获取一个值，指示该步骤中引导头像是否可见。</summary>
    public required bool AliceVisible { get; init; }
}

/// <summary>
/// 使用候选评分方式解析产品导览卡片和引导头像的放置位置，使卡片
/// 和头像避开聚光灯区域及彼此，同时让头像指向聚光灯。
/// </summary>
public sealed class ProductTourOverlayLayoutEngine
{
    /// <summary>
    /// 所有用于生成候选矩形的方向性放置位置。
    /// </summary>
    private static readonly ProductTourPlacement[] Directions =
    [
        ProductTourPlacement.Left,
        ProductTourPlacement.Right,
        ProductTourPlacement.Top,
        ProductTourPlacement.Bottom,
        ProductTourPlacement.LeftTop,
        ProductTourPlacement.LeftBottom,
        ProductTourPlacement.RightTop,
        ProductTourPlacement.RightBottom,
        ProductTourPlacement.TopLeft,
        ProductTourPlacement.TopRight,
        ProductTourPlacement.BottomLeft,
        ProductTourPlacement.BottomRight
    ];

    /// <summary>
    /// 为给定请求布置卡片和引导头像。
    /// </summary>
    /// <param name="request">布局请求。</param>
    /// <returns>解析得到的布局结果。不会产生负坐标，在安全区域过小时也不会抛出异常。</returns>
    public ProductTourOverlayLayoutResult Arrange(ProductTourOverlayLayoutRequest request)
    {
        var safe = request.SafeArea;
        if (safe.Width <= 0 || safe.Height <= 0)
        {
            safe = new Rect(0, 0, Math.Max(0, safe.Width), Math.Max(0, safe.Height));
        }

        var gap = Math.Max(0, request.MinimumGap);
        var cardSize = ClampSize(request.CardDesiredSize, safe.Size);
        var aliceSize = ClampSize(request.AliceDesiredSize, safe.Size);
        var spot = request.SpotlightRect;
        var spotInflated = Rect.Inflate(spot, new Size(gap, gap));
        var forbiddenZones = BuildForbiddenZones(spotInflated, request.Obstacles, gap);
        var spotlightCenter = Center(spot);
        var safeCenter = Center(safe);

        var cardCandidates = Directions
            .Select(dir => (dir, rect: ClampToSafe(CandidateRect(dir, spot, cardSize, gap), safe)))
            .ToList();
        var aliceCandidates = request.AliceVisible
            ? Directions
                .Select(dir => (dir, rect: ClampToSafe(CandidateRect(dir, spot, aliceSize, gap), safe)))
                .ToList()
            : [];

        // When the caller requests a specific card placement, treat it as a hard constraint:
        // use that direction directly when its candidate is valid (fits inside the safe area
        // and does not overlap the spotlight or obstacles). Only fall back to scoring when
        // the preferred candidate is unavailable.
        if (TryUsePreferredCard(request, cardCandidates, forbiddenZones, aliceCandidates, gap, spotlightCenter, out var preferredResult))
        {
            return preferredResult;
        }

        var best = TryScorePairs(request, cardCandidates, aliceCandidates, forbiddenZones, gap, spotlightCenter, safeCenter);
        if (best is not null)
        {
            return BuildResult(best.Value.cardDir, best.Value.cardRect, best.Value.aliceDir, best.Value.aliceRect, spotlightCenter, false, request.AliceVisible);
        }

        return BuildFallback(request, cardCandidates, aliceCandidates, spot, forbiddenZones, gap, spotlightCenter, safe);
    }

    /// <summary>
    /// 检查首选卡片放置位置是否可直接使用，若可以，
    /// 则在固定的卡片矩形周围解析头像位置。
    /// </summary>
    private static bool TryUsePreferredCard(
        ProductTourOverlayLayoutRequest request,
        List<(ProductTourPlacement dir, Rect rect)> cardCandidates,
        IReadOnlyList<Rect> forbiddenZones,
        List<(ProductTourPlacement dir, Rect rect)> aliceCandidates,
        double gap,
        Point spotlightCenter,
        out ProductTourOverlayLayoutResult result)
    {
        result = default!;
        var preferred = request.PreferredCardPlacement;
        if (!preferred.HasValue || preferred.Value is ProductTourPlacement.Auto or ProductTourPlacement.Center)
        {
            return false;
        }

        var match = cardCandidates.FirstOrDefault(c => c.dir == preferred.Value);
        if (match.rect.Width <= 0 || match.rect.Height <= 0)
        {
            return false;
        }

        if (OverlapsAny(match.rect, forbiddenZones))
        {
            return false;
        }

        if (!request.SafeArea.Contains(match.rect))
        {
            return false;
        }

        // Preferred card is valid — resolve the avatar around it.
        Rect aliceRect = Rect.Empty;
        ProductTourPlacement? aliceDir = null;
        var aliceVisible = false;

        if (request.AliceVisible)
        {
            var bestAliceScore = double.NegativeInfinity;
            foreach (var (dir, rect) in aliceCandidates)
            {
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    continue;
                }

                if (OverlapsAny(rect, forbiddenZones))
                {
                    continue;
                }

                if (Overlaps(match.rect, Rect.Inflate(rect, new Size(gap, gap))))
                {
                    continue;
                }

                var insideSafe = request.SafeArea.Contains(rect) ? 500 : 0;
                var proximity = -(Center(rect) - spotlightCenter).Length * 0.04;
                var diagonal = IsDiagonal(dir) ? 60 : 0;
                var alicePref = PreferredAliceDirectionMatch(dir, request.PreferredAlicePlacement);
                var score = insideSafe + proximity + diagonal + alicePref;
                if (score > bestAliceScore)
                {
                    bestAliceScore = score;
                    aliceRect = rect;
                    aliceDir = dir;
                    aliceVisible = true;
                }
            }
        }

        result = BuildResult(match.dir, match.rect, aliceDir, aliceRect, spotlightCenter, false, aliceVisible);
        return true;
    }

    private static ScoredPair? TryScorePairs(
        ProductTourOverlayLayoutRequest request,
        List<(ProductTourPlacement dir, Rect rect)> cardCandidates,
        List<(ProductTourPlacement dir, Rect rect)> aliceCandidates,
        IReadOnlyList<Rect> forbiddenZones,
        double gap,
        Point spotlightCenter,
        Point safeCenter)
    {
        ScoredPair? best = null;
        foreach (var (cardDir, cardRect) in cardCandidates)
        {
            if (cardRect.Width <= 0 || cardRect.Height <= 0)
            {
                continue;
            }

            var cardOverlapsSpot = OverlapsAny(cardRect, forbiddenZones);
            if (cardOverlapsSpot)
            {
                continue;
            }

            var cardInsideSafe = request.SafeArea.Contains(cardRect);
            var cardScore = ScoreCard(cardDir, cardRect, request, spotlightCenter, safeCenter, cardInsideSafe);

            foreach (var (aliceDir, aliceRect) in aliceCandidates)
            {
                if (aliceRect.Width <= 0 || aliceRect.Height <= 0)
                {
                    continue;
                }

                if (OverlapsAny(aliceRect, forbiddenZones))
                {
                    continue;
                }

                if (Overlaps(cardRect, Rect.Inflate(aliceRect, new Size(gap, gap))))
                {
                    continue;
                }

                var aliceInsideSafe = request.SafeArea.Contains(aliceRect);
                var aliceScore = ScoreAlice(aliceDir, aliceRect, request, spotlightCenter, safeCenter, aliceInsideSafe);
                var total = cardScore + aliceScore;
                if (best == null || total > best.Value.score)
                {
                    best = new ScoredPair(total, cardDir, cardRect, aliceDir, aliceRect);
                }
            }

            if (aliceCandidates.Count == 0)
            {
                if (best == null || cardScore > best.Value.score)
                {
                    best = new ScoredPair(cardScore, cardDir, cardRect, null, Rect.Empty);
                }
            }
        }

        return best;
    }

    private static ProductTourOverlayLayoutResult BuildFallback(
        ProductTourOverlayLayoutRequest request,
        List<(ProductTourPlacement dir, Rect rect)> cardCandidates,
        List<(ProductTourPlacement dir, Rect rect)> aliceCandidates,
        Rect spot,
        IReadOnlyList<Rect> forbiddenZones,
        double gap,
        Point spotlightCenter,
        Rect safe)
    {
        var card = ChooseFallbackCard(cardCandidates, spot, forbiddenZones, request, spotlightCenter, safe);
        var aliceRect = Rect.Empty;
        ProductTourPlacement? aliceDir = null;
        var aliceVisible = false;

        if (request.AliceVisible)
        {
            foreach (var (dir, rect) in aliceCandidates
                         .Where(pair => pair.rect.Width > 0 && pair.rect.Height > 0)
                         .OrderByDescending(pair => request.SafeArea.Contains(pair.rect))
                         .ThenBy(pair => (Center(pair.rect) - spotlightCenter).Length))
            {
                if (OverlapsAny(rect, forbiddenZones))
                {
                    continue;
                }

                if (Overlaps(card.rect, Rect.Inflate(rect, new Size(gap, gap))))
                {
                    continue;
                }

                aliceRect = rect;
                aliceDir = dir;
                aliceVisible = true;
                break;
            }
        }

        return BuildResult(card.dir, card.rect, aliceDir, aliceRect, spotlightCenter, true, aliceVisible);
    }

    private static (ProductTourPlacement dir, Rect rect) ChooseFallbackCard(
        List<(ProductTourPlacement dir, Rect rect)> cardCandidates,
        Rect spot,
        IReadOnlyList<Rect> forbiddenZones,
        ProductTourOverlayLayoutRequest request,
        Point spotlightCenter,
        Rect safe)
    {
        var valid = cardCandidates.Where(pair => pair.rect.Width > 0 && pair.rect.Height > 0).ToList();
        if (valid.Count == 0)
        {
            return (ProductTourPlacement.Center, new Rect(Math.Max(0, safe.X), Math.Max(0, safe.Y), request.CardDesiredSize.Width, request.CardDesiredSize.Height));
        }

        var nonOverlapping = valid.Where(pair => !OverlapsAny(pair.rect, forbiddenZones)).ToList();
        var pool = nonOverlapping.Count > 0 ? nonOverlapping : valid;
        if (nonOverlapping.Count > 0)
        {
            return pool.OrderByDescending(pair => request.SafeArea.Contains(pair.rect))
                       .ThenBy(pair => (Center(pair.rect) - spotlightCenter).Length)
                       .First();
        }

        var best = valid[0];
        var bestOverlap = double.PositiveInfinity;

        // 当所有候选都与 spotlight/obstacles 重叠（无路可走）时，若用户指定了首选方向，
        // 优先使用该方向。候选已由 ClampToSafe 处理，会贴着 safe area 对应边缘，
        // 即使部分覆盖 spotlight 也尊重用户的 Placement 设置。
        if (request.PreferredCardPlacement.HasValue
            && request.PreferredCardPlacement.Value is not ProductTourPlacement.Auto
                                               and not ProductTourPlacement.Center)
        {
            var preferredMatch = valid.FirstOrDefault(c => c.dir == request.PreferredCardPlacement.Value);
            if (preferredMatch.rect.Width > 0 && preferredMatch.rect.Height > 0)
            {
                return preferredMatch;
            }
        }

        foreach (var (dir, rect) in valid)
        {
            var overlap = TotalOverlapArea(rect, spot, request.Obstacles);
            var score = overlap - (request.SafeArea.Contains(rect) ? 1000 : 0);
            if (score < bestOverlap)
            {
                bestOverlap = score;
                best = (dir, rect);
            }
        }

        return best;
    }

    private static double ScoreCard(
        ProductTourPlacement dir,
        Rect rect,
        ProductTourOverlayLayoutRequest request,
        Point spotlightCenter,
        Point safeCenter,
        bool insideSafe)
    {
        var score = 0d;
        if (insideSafe) score += 1000;
        score += PreferredDirectionMatch(dir, request.PreferredCardPlacement);
        score -= (Center(rect) - spotlightCenter).Length * 0.05;
        score -= (Center(rect) - safeCenter).Length * 0.02;
        return score;
    }

    private static double ScoreAlice(
        ProductTourPlacement dir,
        Rect rect,
        ProductTourOverlayLayoutRequest request,
        Point spotlightCenter,
        Point safeCenter,
        bool insideSafe)
    {
        var score = 0d;
        if (insideSafe) score += 500;
        score += PreferredAliceDirectionMatch(dir, request.PreferredAlicePlacement);
        score -= (Center(rect) - spotlightCenter).Length * 0.04;
        if (IsDiagonal(dir)) score += 60;
        return score;
    }

    private static int PreferredDirectionMatch(ProductTourPlacement candidate, ProductTourPlacement? preferred)
    {
        if (!preferred.HasValue || preferred.Value is ProductTourPlacement.Auto or ProductTourPlacement.Center)
        {
            return 0;
        }

        if (candidate == preferred.Value)
        {
            return 300;
        }

        return SideGroup(candidate) == SideGroup(preferred.Value) ? 80 : 0;
    }

    private static int PreferredAliceDirectionMatch(ProductTourPlacement candidate, ProductTourAvatarPlacement? preferred)
    {
        if (!preferred.HasValue || preferred.Value == ProductTourAvatarPlacement.Auto)
        {
            return 0;
        }

        var mapped = preferred.Value switch
        {
            ProductTourAvatarPlacement.TopLeft => ProductTourPlacement.TopLeft,
            ProductTourAvatarPlacement.TopRight => ProductTourPlacement.TopRight,
            ProductTourAvatarPlacement.BottomRight => ProductTourPlacement.BottomRight,
            _ => (ProductTourPlacement?)null
        };

        return mapped.HasValue && candidate == mapped.Value ? 200 : 0;
    }

    private static int SideGroup(ProductTourPlacement placement) => placement switch
    {
        ProductTourPlacement.Left or ProductTourPlacement.LeftTop or ProductTourPlacement.LeftBottom => 1,
        ProductTourPlacement.Right or ProductTourPlacement.RightTop or ProductTourPlacement.RightBottom => 2,
        ProductTourPlacement.Top or ProductTourPlacement.TopLeft or ProductTourPlacement.TopRight => 3,
        ProductTourPlacement.Bottom or ProductTourPlacement.BottomLeft or ProductTourPlacement.BottomRight => 4,
        _ => 0
    };

    private static bool IsDiagonal(ProductTourPlacement placement) =>
        placement is ProductTourPlacement.TopLeft
            or ProductTourPlacement.TopRight
            or ProductTourPlacement.BottomLeft
            or ProductTourPlacement.BottomRight;

    private static Rect CandidateRect(ProductTourPlacement dir, Rect spot, Size size, double gap)
    {
        var w = size.Width;
        var h = size.Height;
        var cx = spot.X + spot.Width / 2;
        var cy = spot.Y + spot.Height / 2;
        return dir switch
        {
            ProductTourPlacement.Left => new Rect(spot.Left - gap - w, cy - h / 2, w, h),
            ProductTourPlacement.Right => new Rect(spot.Right + gap, cy - h / 2, w, h),
            ProductTourPlacement.Top => new Rect(cx - w / 2, spot.Top - gap - h, w, h),
            ProductTourPlacement.Bottom => new Rect(cx - w / 2, spot.Bottom + gap, w, h),
            ProductTourPlacement.LeftTop => new Rect(spot.Left - gap - w, spot.Top, w, h),
            ProductTourPlacement.LeftBottom => new Rect(spot.Left - gap - w, spot.Bottom - h, w, h),
            ProductTourPlacement.RightTop => new Rect(spot.Right + gap, spot.Top, w, h),
            ProductTourPlacement.RightBottom => new Rect(spot.Right + gap, spot.Bottom - h, w, h),
            ProductTourPlacement.TopLeft => new Rect(spot.Left - gap - w, spot.Top - gap - h, w, h),
            ProductTourPlacement.TopRight => new Rect(spot.Right + gap, spot.Top - gap - h, w, h),
            ProductTourPlacement.BottomLeft => new Rect(spot.Left - gap - w, spot.Bottom + gap, w, h),
            ProductTourPlacement.BottomRight => new Rect(spot.Right + gap, spot.Bottom + gap, w, h),
            _ => new Rect(cx - w / 2, cy - h / 2, w, h)
        };
    }

    private static ProductTourOverlayLayoutResult BuildResult(
        ProductTourPlacement cardDir,
        Rect cardRect,
        ProductTourPlacement? aliceDir,
        Rect aliceRect,
        Point spotlightCenter,
        bool isFallback,
        bool aliceVisible)
    {
        var aliceCenter = aliceVisible && aliceRect.Width > 0 ? Center(aliceRect) : spotlightCenter;
        return new ProductTourOverlayLayoutResult
        {
            CardPosition = new Point(cardRect.X, cardRect.Y),
            AlicePosition = aliceVisible ? new Point(aliceRect.X, aliceRect.Y) : new Point(0, 0),
            AlicePose = ChooseAlicePose(aliceCenter, spotlightCenter),
            CardRect = cardRect,
            AliceRect = aliceRect,
            IsFallback = isFallback,
            AliceVisible = aliceVisible
        };
    }

    /// <summary>
    /// 选择一个指向聚光灯中心的引导头像姿态。
    /// </summary>
    /// <param name="aliceCenter">遮罩坐标系下的引导头像中心点。</param>
    /// <param name="spotlightCenter">遮罩坐标系下的聚光灯中心点。</param>
    /// <returns>指向方向最匹配聚光灯方向的姿态。</returns>
    public static TutorialAvatarPose ChooseAlicePose(Point aliceCenter, Point spotlightCenter)
    {
        var dx = spotlightCenter.X - aliceCenter.X;
        var dy = spotlightCenter.Y - aliceCenter.Y;
        if (Math.Abs(dx) < 1 && Math.Abs(dy) < 1)
        {
            return TutorialAvatarPose.Idle;
        }

        var spotlightRight = dx >= 0;
        var spotlightBelow = dy >= 0;
        return (spotlightRight, spotlightBelow) switch
        {
            (true, true) => TutorialAvatarPose.RightBottom,
            (true, false) => TutorialAvatarPose.RightTop,
            (false, true) => TutorialAvatarPose.LeftBottom,
            (false, false) => TutorialAvatarPose.LeftTop
        };
    }

    private static Size ClampSize(Size size, Size max)
    {
        if (max.Width <= 0 || max.Height <= 0)
        {
            return new Size(Math.Max(0, size.Width), Math.Max(0, size.Height));
        }

        return new Size(Math.Min(size.Width, max.Width), Math.Min(size.Height, max.Height));
    }

    private static Rect ClampToSafe(Rect rect, Rect safe)
    {
        if (rect.Width > safe.Width || rect.Height > safe.Height)
        {
            return Rect.Empty;
        }

        var x = rect.X;
        var y = rect.Y;

        if (x < safe.X) x = safe.X;
        if (x + rect.Width > safe.Right) x = safe.Right - rect.Width;

        if (y < safe.Y) y = safe.Y;
        if (y + rect.Height > safe.Bottom) y = safe.Bottom - rect.Height;

        return new Rect(Math.Max(0, x), Math.Max(0, y), rect.Width, rect.Height);
    }

    private static bool Overlaps(Rect a, Rect b) =>
        a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;

    private static List<Rect> BuildForbiddenZones(Rect spotInflated, IReadOnlyList<Rect> obstacles, double gap)
    {
        var zones = new List<Rect>(1 + obstacles.Count) { spotInflated };
        foreach (var obs in obstacles)
        {
            zones.Add(Rect.Inflate(obs, new Size(gap, gap)));
        }
        return zones;
    }

    private static bool OverlapsAny(Rect rect, IReadOnlyList<Rect> zones)
    {
        foreach (var zone in zones)
        {
            if (Overlaps(rect, zone)) return true;
        }
        return false;
    }

    private static double TotalOverlapArea(Rect rect, Rect spot, IReadOnlyList<Rect> obstacles)
    {
        var total = IntersectionArea(rect, spot);
        foreach (var obs in obstacles)
        {
            total += IntersectionArea(rect, obs);
        }
        return total;
    }

    private static double IntersectionArea(Rect a, Rect b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        if (right <= x || bottom <= y)
        {
            return 0;
        }

        return (right - x) * (bottom - y);
    }

    private static Point Center(Rect rect) => new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

    private readonly record struct ScoredPair(
        double score,
        ProductTourPlacement cardDir,
        Rect cardRect,
        ProductTourPlacement? aliceDir,
        Rect aliceRect);
}
