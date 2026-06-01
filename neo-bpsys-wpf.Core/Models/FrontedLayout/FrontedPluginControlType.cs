namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Parsed Designer v3 plugin fronted control type.
/// </summary>
public readonly record struct FrontedPluginControlType(string PackageId, string ControlTypeName)
{
    public const string Prefix = "plugin:";

    /// <summary>
    /// Returns whether the raw control type uses the plugin prefix.
    /// </summary>
    public static bool IsPluginControlType(string? controlType)
    {
        return controlType?.StartsWith(Prefix, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Attempts to parse a plugin control type.
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
    /// Parses a plugin control type or throws when it is invalid.
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
    /// Returns whether a package ID or plugin control type name is valid.
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
