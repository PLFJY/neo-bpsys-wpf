using System.Windows;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 形状渐变角度辅助类，提供角度归一化和将角度转换为相对坐标点的方法。
/// </summary>
public static class ShapeGradientAngleHelper
{
    /// <summary>
    /// 将任意角度归一化到 [0, 360) 范围内。非有限值返回 0。
    /// </summary>
    /// <param name="angle">待归一化的角度。</param>
    /// <returns>归一化后的角度。</returns>
    public static double Normalize(double angle)
    {
        if (!double.IsFinite(angle))
        {
            return 0;
        }

        var normalized = angle % 360D;
        return normalized < 0 ? normalized + 360D : normalized;
    }

    public static (Point StartPoint, Point EndPoint) ToRelativePoints(double angle)
    {
        var normalized = Normalize(angle);
        if (normalized == 0D)
        {
            return (new Point(0, 0.5), new Point(1, 0.5));
        }

        if (normalized == 90D)
        {
            return (new Point(0.5, 0), new Point(0.5, 1));
        }

        if (normalized == 180D)
        {
            return (new Point(1, 0.5), new Point(0, 0.5));
        }

        if (normalized == 270D)
        {
            return (new Point(0.5, 1), new Point(0.5, 0));
        }

        var radians = normalized * Math.PI / 180D;
        var x = Math.Cos(radians);
        var y = Math.Sin(radians);
        var scale = 0.5D / Math.Max(Math.Abs(x), Math.Abs(y));
        var offsetX = x * scale;
        var offsetY = y * scale;
        return (
            new Point(0.5D - offsetX, 0.5D - offsetY),
            new Point(0.5D + offsetX, 0.5D + offsetY));
    }
}
