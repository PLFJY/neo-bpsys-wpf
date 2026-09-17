namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 比较布局包版本字段中的数字版本部分。
/// </summary>
public static class FrontedPackageVersionComparer
{
    /// <summary>
    /// 尝试解析版本，忽略开头的 <c>v</c> 以及预发布和构建元数据后缀。
    /// </summary>
    /// <param name="value">完整版本文本。</param>
    /// <param name="version">解析出的数字版本。</param>
    /// <returns>版本可解析时为 <see langword="true"/>。</returns>
    public static bool TryParseNumeric(string? value, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        return Version.TryParse(normalized, out version!);
    }

    /// <summary>
    /// 比较两个完整版本文本的数字版本部分。
    /// </summary>
    /// <param name="left">左侧版本。</param>
    /// <param name="right">右侧版本。</param>
    /// <returns>左侧小于、等于或大于右侧时分别返回负数、零或正数。</returns>
    public static int CompareNumeric(string? left, string? right)
    {
        var leftParsed = TryParseNumeric(left, out var leftVersion);
        var rightParsed = TryParseNumeric(right, out var rightVersion);
        if (!leftParsed || !rightParsed)
        {
            throw new ArgumentException("Both versions must contain a numeric version.");
        }

        return leftVersion.CompareTo(rightVersion);
    }
}
