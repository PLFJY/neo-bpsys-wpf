using System.Windows.Media;
using Color = System.Windows.Media.Color;


namespace neo_bpsys_wpf.Core.Helpers;

public static class ColorHelper
{
    /// <summary>
    /// 将16进制ARGB或RGB颜色字符串转换为SolidColorBrush
    /// </summary>
    /// <param name="hexColor">颜色字符串</param>
    /// <returns>SolidColorBrush</returns>
    /// <exception cref="ArgumentException">颜色字符串不能为空</exception>
    /// <exception cref="ArgumentException">颜色字符串格式不正确</exception>
    /// <exception cref="ArgumentException">颜色字符串包含非16进制字符</exception>
    /// <exception cref="ArgumentException">颜色值超出范围</exception>
    public static SolidColorBrush HexToBrush(string hexColor)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
            throw new ArgumentException("颜色字符串不能为空", nameof(hexColor));

        // 移除可能存在的#前缀
        if (hexColor.StartsWith("#"))
            hexColor = hexColor[1..];

        Color color;

        try
        {
            switch (hexColor.Length)
            {
                // ARGB格式 (8个字符)
                case 8:
                    var a = byte.Parse(hexColor[..2], System.Globalization.NumberStyles.HexNumber);
                    var r = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    var g = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    var b = byte.Parse(hexColor.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                    color = Color.FromArgb(a, r, g, b);
                    break;

                // RGB格式 (6个字符)
                case 6:
                    r = byte.Parse(hexColor[..2], System.Globalization.NumberStyles.HexNumber);
                    g = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    b = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    color = Color.FromArgb(255, r, g, b); // 默认不透明
                    break;

                default:
                    throw new ArgumentException("颜色字符串格式不正确，应为#RRGGBB或#AARRGGBB格式", nameof(hexColor));
            }
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("颜色字符串包含非16进制字符", nameof(hexColor), ex);
        }
        catch (OverflowException ex)
        {
            throw new ArgumentException("颜色值超出范围", nameof(hexColor), ex);
        }

        return new SolidColorBrush(color);
    }
    
    public static string ToArgbHexString(this Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static Color ToColor(this string? argb)
    {
        if (string.IsNullOrWhiteSpace(argb))
            return Color.FromArgb(255,255,255,255);

        // 移除可能存在的#前缀
        if (argb.StartsWith("#"))
            argb = argb[1..];

        Color color;

        try
        {
            switch (argb.Length)
            {
                // ARGB格式 (8个字符)
                case 8:
                    var a = byte.Parse(argb[..2], System.Globalization.NumberStyles.HexNumber);
                    var r = byte.Parse(argb.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    var g = byte.Parse(argb.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    var b = byte.Parse(argb.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                    color = Color.FromArgb(a, r, g, b);
                    break;

                // RGB格式 (6个字符)
                case 6:
                    r = byte.Parse(argb[..2], System.Globalization.NumberStyles.HexNumber);
                    g = byte.Parse(argb.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    b = byte.Parse(argb.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    color = Color.FromArgb(255, r, g, b); // 默认不透明
                    break;

                default:
                    throw new ArgumentException("颜色字符串格式不正确，应为#RRGGBB或#AARRGGBB格式", nameof(argb));
            }
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("颜色字符串包含非16进制字符", nameof(argb), ex);
        }
        catch (OverflowException ex)
        {
            throw new ArgumentException("颜色值超出范围", nameof(argb), ex);
        }

        return color;
    }
}