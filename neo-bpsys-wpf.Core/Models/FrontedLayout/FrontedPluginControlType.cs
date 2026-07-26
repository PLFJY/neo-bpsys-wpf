namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 已解析的设计器 v3 插件前台控件类型。
/// </summary>
public readonly record struct FrontedPluginControlType(string PackageId, string ControlTypeName)
{
    public const string Prefix = "plugin:";

    /// <summary>
    /// 返回原始控件类型是否使用了插件前缀。
    /// </summary>
    public static bool IsPluginControlType(string? controlType)
    {
        return controlType?.StartsWith(Prefix, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// 尝试解析插件控件类型。
    /// </summary>
    public static bool TryParse(string? controlType, out FrontedPluginControlType parsed)
    {
        parsed = default;
        if (!IsPluginControlType(controlType))
        {
            return false;
        }

        var body = controlType![Prefix.Length..];
        var separatorIndex = body.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex != body.LastIndexOf('/'))
        {
            return false;
        }

        var packageId = body[..separatorIndex];
        var controlTypeName = body[(separatorIndex + 1)..];
        if (!IsValidPart(packageId) || !IsValidPart(controlTypeName))
        {
            return false;
        }

        parsed = new FrontedPluginControlType(packageId, controlTypeName);
        return true;
    }

    /// <summary>
    /// 解析插件控件类型，无效时抛出异常。
    /// </summary>
    public static FrontedPluginControlType Parse(string controlType)
    {
        if (TryParse(controlType, out var parsed))
        {
            return parsed;
        }

        throw new FrontedLayoutConfigException(
            $"Plugin ControlType '{controlType}' must use 'plugin:<PackageId>/<ControlTypeName>' with safe non-empty parts.");
    }

    /// <summary>
    /// 返回包 ID 或插件控件类型名是否有效。
    /// </summary>
    public static bool IsValidPart(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Contains('/', StringComparison.Ordinal)
            && !value.Contains('\\', StringComparison.Ordinal)
            && !value.Contains(':', StringComparison.Ordinal)
            && !value.Contains("..", StringComparison.Ordinal)
            && !value.Any(char.IsWhiteSpace);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Prefix}{PackageId}/{ControlTypeName}";
    }
}
