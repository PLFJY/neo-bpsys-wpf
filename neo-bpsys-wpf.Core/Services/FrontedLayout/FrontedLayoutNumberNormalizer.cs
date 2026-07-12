namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 规范化从旧布局源导入的数值。
/// </summary>
public static class FrontedLayoutNumberNormalizer
{
    private const double IntegerEpsilon = 0.000001D;

    public static double Normalize(double value)
    {
        var roundedInteger = Math.Round(value);
        if (Math.Abs(value - roundedInteger) < IntegerEpsilon)
        {
            return roundedInteger;
        }

        return Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    public static double? Normalize(double? value)
    {
        return value.HasValue ? Normalize(value.Value) : null;
    }
}
