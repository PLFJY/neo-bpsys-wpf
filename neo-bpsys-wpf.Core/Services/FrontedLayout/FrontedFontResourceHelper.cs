using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Shared helpers for Designer v3 font resources.
/// </summary>
public static class FrontedFontResourceHelper
{
    private static readonly Regex UnsafeFileNameChars = new("[^A-Za-z0-9._-]+", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedFontExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf",
        ".otf",
        ".ttc"
    };

    /// <summary>
    /// Determines whether the file extension is supported as a layout package font.
    /// </summary>
    /// <param name="extension">File extension including the leading dot.</param>
    /// <returns>Whether the extension is supported.</returns>
    public static bool IsSupportedFontExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && SupportedFontExtensions.Contains(extension);
    }

    /// <summary>
    /// Creates a safe stored file name for a copied font.
    /// </summary>
    /// <param name="sourcePath">Original source path.</param>
    /// <param name="sha256">Source file SHA-256 hash.</param>
    /// <returns>Safe file name with a short hash suffix.</returns>
    public static string CreateFontFileName(string sourcePath, string sha256)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var safeBaseName = UnsafeFileNameChars.Replace(Path.GetFileNameWithoutExtension(sourcePath), "-")
            .Replace("..", "-", StringComparison.Ordinal)
            .Trim('.', '-', '_');
        if (string.IsNullOrWhiteSpace(safeBaseName))
        {
            safeBaseName = "font";
        }

        return $"{safeBaseName}-{sha256[..12]}{extension}";
    }

    /// <summary>
    /// Computes the SHA-256 hash for a file.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <returns>Lowercase hex SHA-256 hash.</returns>
    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads WPF font family names from a font file.
    /// </summary>
    /// <param name="path">Font file path.</param>
    /// <returns>Distinct family names discoverable by WPF.</returns>
    public static IReadOnlyList<string> ReadFontFamilyNames(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        var names = new List<string>();
        try
        {
            foreach (var family in Fonts.GetFontFamilies(Path.GetDirectoryName(path) ?? string.Empty))
            {
                foreach (var typeface in family.GetTypefaces())
                {
                    if (!typeface.TryGetGlyphTypeface(out var glyph)
                        || !string.Equals(
                            Path.GetFullPath(glyph.FontUri.LocalPath),
                            Path.GetFullPath(path),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    names.AddRange(glyph.FamilyNames.Values.Where(name => !string.IsNullOrWhiteSpace(name)));
                }
            }
        }
        catch
        {
            // Fall back to direct GlyphTypeface for simple .ttf/.otf files.
        }

        if (names.Count == 0)
        {
            try
            {
                var glyphTypeface = new GlyphTypeface(new Uri(path, UriKind.Absolute));
                names.AddRange(glyphTypeface.FamilyNames.Values.Where(name => !string.IsNullOrWhiteSpace(name)));
            }
            catch
            {
                return [];
            }
        }

        return names.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Creates a WPF <see cref="FontFamily"/> from a stored layout font value.
    /// </summary>
    /// <param name="storedValue">Stored layout font value.</param>
    /// <param name="resourceResolver">Optional resource resolver used for bpui font paths.</param>
    /// <param name="logger">Optional logger for invalid values.</param>
    /// <returns>A safe WPF font family.</returns>
    public static FontFamily CreateFontFamily(
        string? storedValue,
        IFrontedResourceResolver? resourceResolver = null,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return new FontFamily("Arial");
        }

        try
        {
            var hashIndex = storedValue.IndexOf('#');
            if (storedValue.Contains("pack://application:,,,", StringComparison.Ordinal) && hashIndex >= 0)
            {
                return new FontFamily(new Uri(storedValue[..hashIndex]), "./" + storedValue[hashIndex..]);
            }

            if (storedValue.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase)
                && hashIndex >= 0
                && (resourceResolver ?? new FrontedResourceResolver(NullLogger<FrontedResourceResolver>.Instance))
                    .ResolveFilePath(storedValue[..hashIndex]) is { } fontPath)
            {
                return new FontFamily(new Uri(Path.GetDirectoryName(fontPath)! + Path.DirectorySeparatorChar), "./" + storedValue[hashIndex..]);
            }

            return new FontFamily(storedValue);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Invalid Fronted FontFamily value: {FontFamily}", storedValue);
            return new FontFamily("Arial");
        }
    }

    /// <summary>
    /// Gets a display name from a stored font value.
    /// </summary>
    /// <param name="storedValue">Stored font value.</param>
    /// <returns>Display name.</returns>
    public static string ExtractFontName(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return string.Empty;
        }

        var hashIndex = storedValue.IndexOf('#');
        return hashIndex >= 0 && hashIndex < storedValue.Length - 1
            ? storedValue[(hashIndex + 1)..]
            : storedValue;
    }
}
