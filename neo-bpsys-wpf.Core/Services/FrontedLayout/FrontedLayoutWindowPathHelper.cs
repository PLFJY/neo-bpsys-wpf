using System.Text.RegularExpressions;
using System.IO;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Converts v3 FullWindowType identities to filesystem-safe layout paths.
/// </summary>
public static partial class FrontedLayoutWindowPathHelper
{
    public const string PluginPrefix = "plugin:";

    public static string GetLayoutFolderRelativePath(string fullWindowType)
    {
        if (TryParsePluginFullWindowType(fullWindowType, out var packageId, out var windowTypeName))
        {
            EnsureSafePathSegment(packageId, nameof(packageId));
            EnsureSafePathSegment(windowTypeName, nameof(windowTypeName));
            return Path.Combine("plugin", packageId, windowTypeName);
        }

        EnsureSafePathSegment(fullWindowType, nameof(fullWindowType));
        return fullWindowType;
    }

    public static string GetLayoutRelativePath(string fullWindowType, string canvasName)
    {
        EnsureSafePathSegment(canvasName, nameof(canvasName));
        return Path.Combine(GetLayoutFolderRelativePath(fullWindowType), $"{canvasName}.json");
    }

    public static string GetWindowOptionsRelativePath(string fullWindowType)
    {
        return Path.Combine(GetLayoutFolderRelativePath(fullWindowType), "window.json");
    }

    public static string ToFullWindowTypeFromRelativeFolder(string relativeFolder)
    {
        var parts = relativeFolder
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && string.Equals(parts[0], "plugin", StringComparison.OrdinalIgnoreCase))
        {
            EnsureSafePathSegment(parts[1], "packageId");
            EnsureSafePathSegment(parts[2], "windowTypeName");
            return $"{PluginPrefix}{parts[1]}/{parts[2]}";
        }

        if (parts.Length == 1)
        {
            EnsureSafePathSegment(parts[0], "windowTypeName");
            return parts[0];
        }

        throw new ArgumentException("Layout folder is not a valid FullWindowType path.", nameof(relativeFolder));
    }

    public static bool IsSafeFullWindowType(string fullWindowType)
    {
        try
        {
            _ = GetLayoutFolderRelativePath(fullWindowType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParsePluginFullWindowType(
        string fullWindowType,
        out string packageId,
        out string windowTypeName)
    {
        packageId = string.Empty;
        windowTypeName = string.Empty;
        if (string.IsNullOrWhiteSpace(fullWindowType)
            || !fullWindowType.StartsWith(PluginPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = fullWindowType[PluginPrefix.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1 || rest.IndexOf('/', slash + 1) >= 0)
        {
            return false;
        }

        packageId = rest[..slash];
        windowTypeName = rest[(slash + 1)..];
        return IsSafePathSegment(packageId) && IsSafePathSegment(windowTypeName);
    }

    public static bool IsSafePathSegment(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && SafeSegmentRegex().IsMatch(value)
               && !value.Contains("..", StringComparison.Ordinal);
    }

    private static void EnsureSafePathSegment(string value, string name)
    {
        if (!IsSafePathSegment(value))
        {
            throw new ArgumentException($"{name} is not a safe layout path segment: {value}", name);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSegmentRegex();
}
