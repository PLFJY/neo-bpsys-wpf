using OpenCvSharp;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 相对坐标矩形（基于 0~100 百分比）。
/// </summary>
public readonly record struct RelativeRect(double X, double Y, double W, double H)
{
    private const double PercentBase = 100.0;

    /// <summary>
    /// 将相对坐标转换为像素坐标，并做边界保护。
    /// 这里的边界保护是为了避免：
    /// 1) 反序列化后出现微小越界
    /// 2) 极端缩放时产生 0 宽高
    /// </summary>
    /// <param name="imageWidth">目标图像宽度（像素）</param>
    /// <param name="imageHeight">目标图像高度（像素）</param>
    /// <returns>可直接用于 OpenCV 裁剪的像素矩形</returns>
    public Rect ToPixelRect(int imageWidth, int imageHeight)
    {
        var x = Math.Clamp((int)Math.Round((X / PercentBase) * imageWidth), 0, Math.Max(imageWidth - 1, 0));
        var y = Math.Clamp((int)Math.Round((Y / PercentBase) * imageHeight), 0, Math.Max(imageHeight - 1, 0));

        var maxW = Math.Max(imageWidth - x, 1);
        var maxH = Math.Max(imageHeight - y, 1);

        var w = Math.Clamp((int)Math.Round((W / PercentBase) * imageWidth), 1, maxW);
        var h = Math.Clamp((int)Math.Round((H / PercentBase) * imageHeight), 1, maxH);
        return new Rect(x, y, w, h);
    }

    /// <summary>
    /// 将像素坐标转换为相对坐标。
    /// 常用于编辑器在拖拽结束后把 UI 结果回写到配置模型。
    /// </summary>
    public static RelativeRect FromPixelRect(double x, double y, double w, double h, double imageWidth, double imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            return new RelativeRect(0, 0, 100, 100);

        return new RelativeRect(
            RoundPercent((x / imageWidth) * PercentBase),
            RoundPercent((y / imageHeight) * PercentBase),
            RoundPercent((w / imageWidth) * PercentBase),
            RoundPercent((h / imageHeight) * PercentBase));
    }

    /// <summary>
    /// 检查是否处于 0~100 的合法区域（闭区间，宽高必须大于 0）。
    /// </summary>
    public bool IsValid01()
    {
        return X >= 0 && Y >= 0 && W > 0 && H > 0 && X + W <= PercentBase && Y + H <= PercentBase;
    }

    private static double RoundPercent(double value)
    {
        return Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}
