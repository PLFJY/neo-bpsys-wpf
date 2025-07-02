using System.Windows.Media;

namespace neo_bpsys_wpf.Helpers;

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
            hexColor = hexColor.Substring(1);

        Color color;

        try
        {
            switch (hexColor.Length)
            {
                // ARGB格式 (8个字符)
                case 8:
                    var a = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                    var r = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    var g = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                    var b = byte.Parse(hexColor.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                    color = Color.FromArgb(a, r, g, b);
                    break;

                // RGB格式 (6个字符)
                case 6:
                    r = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
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
}