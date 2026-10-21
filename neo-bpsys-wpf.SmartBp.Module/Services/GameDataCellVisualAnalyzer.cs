using OpenCvSharp;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 数字单元格中竖直细笔画的图像证据。
/// </summary>
/// <param name="BoundingBox">亮色连通区域边界框。</param>
/// <param name="AspectRatio">连通区域高宽比。</param>
/// <param name="FillRatio">边界框内前景像素占比。</param>
/// <param name="Threshold">生成前景掩码使用的灰度阈值。</param>
/// <param name="Confidence">形态证据置信度；只用于与 OCR 疑似候选联合判断。</param>
internal sealed record GameDataDigitOneVisualEvidence(
    Rect BoundingBox,
    double AspectRatio,
    double FillRatio,
    double Threshold,
    double Confidence);

/// <summary>
/// 定位数字单元格的亮色文字和空值横杠，并提供数字 1 的辅助形态证据。
/// </summary>
internal static class GameDataCellVisualAnalyzer
{
    /// <summary>
    /// 在表格切片中定位亮色文字，排除触边背景和贯穿整格的分隔线。
    /// 只返回裁剪几何及横杠形态，不根据笔画猜测数字值。
    /// </summary>
    /// <param name="cell">包含完整数字和周围边距的单元格。</param>
    /// <param name="bounds">文字边界（单元格坐标系）。</param>
    /// <param name="isEmptyMarker">文字是否仅为位于中央的短横杠。</param>
    /// <returns>存在完整且居中的文字前景时返回 <see langword="true"/>。</returns>
    internal static bool TryLocateText(Mat cell, out Rect bounds, out bool isEmptyMarker)
    {
        bounds = default;
        isEmptyMarker = false;
        if (cell.Empty()) return false;
        using var gray = new Mat();
        if (cell.Channels() == 1) cell.CopyTo(gray);
        else Cv2.CvtColor(cell, gray, cell.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);

        Cv2.MeanStdDev(gray, out var mean, out var deviation);
        using var mask = new Mat();
        Cv2.Threshold(gray, mask, Math.Clamp(mean.Val0 + Math.Max(24, deviation.Val0), 110, 210), 255, ThresholdTypes.Binary);
        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var boxes = contours.Select(Cv2.BoundingRect).Where(box =>
            box.X > 0 && box.Y > 0 && box.Right < cell.Width && box.Bottom < cell.Height &&
            box.Width < cell.Width * .85 && box.Height >= 1 && box.Width >= 2 && box.Width * box.Height >= 3 &&
            Math.Abs(box.Y + box.Height / 2d - cell.Height / 2d) < cell.Height * .32).ToArray();
        if (boxes.Length == 0) return false;
        var left = boxes.Min(box => box.Left);
        var top = boxes.Min(box => box.Top);
        bounds = new Rect(left, top, boxes.Max(box => box.Right) - left, boxes.Max(box => box.Bottom) - top);
        if (Math.Abs(bounds.X + bounds.Width / 2d - cell.Width / 2d) > cell.Width * .25)
            return false;
        isEmptyMarker = boxes.Length == 1 && bounds.Width >= bounds.Height * 3 &&
            bounds.Height <= cell.Height * .15 && bounds.Width <= cell.Width * .4;
        return isEmptyMarker || bounds.Height >= cell.Height * .28;
    }

    /// <summary>
    /// 尝试提取数字 1 的高窄亮色连通区域。该结果不能单独作为 OCR 值使用。
    /// </summary>
    /// <param name="cell">紧密裁剪后的原始数字单元格。</param>
    /// <param name="evidence">检测成功时返回的形态证据。</param>
    /// <returns>中心存在可信高窄笔画时返回 <see langword="true"/>。</returns>
    internal static bool TryDetectDigitOne(Mat cell, out GameDataDigitOneVisualEvidence? evidence)
    {
        ArgumentNullException.ThrowIfNull(cell);
        evidence = null;
        if (cell.Empty() || cell.Width < 12 || cell.Height < 16)
            return false;

        using var gray = new Mat();
        if (cell.Channels() == 1)
            cell.CopyTo(gray);
        else
            Cv2.CvtColor(cell, gray, cell.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);

        Cv2.MeanStdDev(gray, out var mean, out var standardDeviation);
        var threshold = Math.Clamp(mean.Val0 + Math.Max(24, standardDeviation.Val0 * 1.05), 110, 210);
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.Binary);

        // 最底部可能包含表格横向分隔线，不应与数字竖笔画连成一个宽区域。
        var ignoredBottomRows = Math.Min(4, binary.Height / 5);
        if (ignoredBottomRows > 0)
        {
            using var ignoredRows = binary.RowRange(binary.Height - ignoredBottomRows, binary.Height);
            ignoredRows.SetTo(Scalar.Black);
        }

        using var closeKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 3));
        using var connected = new Mat();
        Cv2.MorphologyEx(binary, connected, MorphTypes.Close, closeKernel);
        Cv2.FindContours(
            connected,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var centerX = cell.Width / 2d;
        var centerY = (cell.Height - ignoredBottomRows) / 2d;
        foreach (var contour in contours)
        {
            var box = Cv2.BoundingRect(contour);
            if (box.Width < 2 || box.Height < cell.Height * .45 || box.Height > cell.Height - ignoredBottomRows)
                continue;

            var aspectRatio = box.Height / (double)box.Width;
            if (aspectRatio < 1.35 || box.Width > cell.Width * .25)
                continue;

            var boxCenterX = box.X + box.Width / 2d;
            var boxCenterY = box.Y + box.Height / 2d;
            if (Math.Abs(boxCenterX - centerX) > cell.Width * .18 ||
                Math.Abs(boxCenterY - centerY) > cell.Height * .24)
                continue;

            using var foreground = new Mat(connected, box);
            var fillRatio = Cv2.CountNonZero(foreground) / (double)(box.Width * box.Height);
            if (fillRatio is < .10 or > .88)
                continue;

            var heightScore = Math.Clamp(box.Height / (cell.Height * .72), 0, 1);
            var aspectScore = Math.Clamp(aspectRatio / 2.2, 0, 1);
            var centerScore = 1 - Math.Clamp(Math.Abs(boxCenterX - centerX) / (cell.Width * .18), 0, 1);
            var confidence = .45 * heightScore + .35 * aspectScore + .20 * centerScore;
            var candidate = new GameDataDigitOneVisualEvidence(
                box,
                aspectRatio,
                fillRatio,
                threshold,
                Math.Clamp(confidence, 0, .82));
            if (evidence == null || candidate.Confidence > evidence.Confidence)
                evidence = candidate;
        }

        return evidence != null;
    }
}
