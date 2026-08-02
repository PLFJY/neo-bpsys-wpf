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
/// 检查已知数字单元格中心是否存在符合数字 1 的高窄亮色笔画。
/// </summary>
internal static class GameDataCellVisualAnalyzer
{
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
