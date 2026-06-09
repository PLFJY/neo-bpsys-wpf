using System.Text.RegularExpressions;
using System.IO;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Converts v3 <c>FullWindowType</c> identities to filesystem-safe layout paths.
/// </summary>
/// <remarks>
/// Built-in identities map directly, for example <c>BpWindow</c> to <c>FrontedLayouts/BpWindow.json</c>.
/// Plugin identities map from <c>plugin:{PackageId}/{WindowTypeName}</c> to
/// <c>FrontedLayouts/plugin/{PackageId}/{WindowTypeName}.json</c>.
/// </remarks>
public static partial class FrontedLayoutWindowPathHelper
{
    /// <summary>
    /// Prefix used by plugin fronted window layout identities.
    /// </summary>
    public const string PluginPrefix = "plugin:";

    /// <summary>
    /// Gets the safe folder path relative to the fronted layout root for a full window type.
    /// </summary>
    /// <param name="fullWindowType">Built-in window type name or plugin full window type.</param>
    /// <returns>Safe relative folder path without the layout JSON filename.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fullWindowType"/> is not path-safe.</exception>
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

    /// <summary>
    /// Gets the safe window layout JSON path relative to the fronted layout root.
    /// </summary>
    /// <param name="fullWindowType">Built-in window type name or plugin full window type.</param>
    /// <returns>Safe relative layout JSON path.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fullWindowType"/> is not path-safe.</exception>
    public static string GetLayoutRelativePath(string fullWindowType)
    {
        if (TryParsePluginFullWindowType(fullWindowType, out var packageId, out var windowTypeName))
        {
            EnsureSafePathSegment(packageId, nameof(packageId));
            EnsureSafePathSegment(windowTypeName, nameof(windowTypeName));
            return Path.Combine("plugin", packageId, $"{windowTypeName}.json");
        }

        EnsureSafePathSegment(fullWindowType, nameof(fullWindowType));
        return $"{fullWindowType}.json";
    }

    /// <summary>
    /// Gets the safe legacy canvas layout JSON path relative to the fronted layout root.
    /// </summary>
    /// <param name="fullWindowType">Built-in window type name or plugin full window type.</param>
    /// <param name="canvasName">Legacy canvas name.</param>
    /// <returns>Safe relative legacy canvas layout JSON path.</returns>
    /// <exception cref="ArgumentException">Thrown when the window type or canvas name is not path-safe.</exception>
    public static string GetLegacyCanvasLayoutRelativePath(string fullWindowType, string canvasName)
    {
        EnsureSafePathSegment(canvasName, nameof(canvasName));
        return Path.Combine(GetLayoutFolderRelativePath(fullWindowType), $"{canvasName}.json");
    }

    /// <summary>
    /// Gets the safe window options JSON path relative to the fronted layout root.
    /// </summary>
    /// <param name="fullWindowType">Built-in window type name or plugin full window type.</param>
    /// <returns>Safe relative window options JSON path.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fullWindowType"/> is not path-safe.</exception>
    public static string GetWindowOptionsRelativePath(string fullWindowType)
    {
        return Path.Combine(GetLayoutFolderRelativePath(fullWindowType), "window.json");
    }

    /// <summary>
    /// Converts a safe relative layout folder back to the corresponding full window type.
    /// </summary>
    /// <param name="relativeFolder">Relative folder created by <see cref="GetLayoutFolderRelativePath"/>.</param>
    /// <returns>The full window type represented by the folder.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="relativeFolder"/> is not a valid layout folder.</exception>
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

    /// <summary>
    /// Returns whether a full window type can be safely mapped to a layout path.
    /// </summary>
    /// <param name="fullWindowType">Built-in window type name or plugin full window type.</param>
    /// <returns><see langword="true"/> when the value can be mapped to a safe layout path.</returns>
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

    /// <summary>
    /// Parses a plugin full window type in the form <c>plugin:{PackageId}/{WindowTypeName}</c>.
    /// </summary>
    /// <param name="fullWindowType">Full window type to parse.</param>
    /// <param name="packageId">Parsed package id when parsing succeeds.</param>
    /// <param name="windowTypeName">Parsed plugin window type name when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="fullWindowType"/> is a valid plugin full window type.</returns>
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

    /// <summary>
    /// Returns whether a value is safe for one layout path segment.
    /// </summary>
    /// <param name="value">Path segment value to validate.</param>
    /// <returns><see langword="true"/> when the value is safe for a single layout path segment.</returns>
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
