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
/// 设计器 v3 字体资源的共享帮助程序。
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
    /// 确定文件扩展名是否作为布局包字体受支持。
    /// </summary>
    /// <param name="extension">包含前导点的文件扩展名。</param>
    /// <returns>扩展名是否受支持。</returns>
    public static bool IsSupportedFontExtension(string? extension)
    {
        return !string.IsNullOrWhiteSpace(extension) && SupportedFontExtensions.Contains(extension);
    }

    /// <summary>
    /// 为复制的字体创建安全的存储文件名。
    /// </summary>
    /// <param name="sourcePath">原始源路径。</param>
    /// <param name="sha256">源文件 SHA-256 哈希。</param>
    /// <returns>带短哈希后缀的安全文件名。</returns>
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
    /// 计算文件的 SHA-256 哈希。
    /// </summary>
    /// <param name="path">文件路径。</param>
    /// <returns>小写十六进制 SHA-256 哈希。</returns>
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
    /// 从字体文件读取 WPF 字体系列名称。
    /// </summary>
    /// <param name="path">字体文件路径。</param>
    /// <returns>WPF 可发现的去重系列名称。</returns>
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
    /// 从存储的布局字体值创建 WPF <see cref="FontFamily"/>。
    /// </summary>
    /// <param name="storedValue">存储的布局字体值。</param>
    /// <param name="resourceResolver">用于 bpui 字体路径的可选资源解析器。</param>
    /// <param name="logger">用于无效值的可选日志记录器。</param>
    /// <returns>安全的 WPF 字体系列。</returns>
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
    /// 从存储的字体值获取显示名称。
    /// </summary>
    /// <param name="storedValue">存储的字体值。</param>
    /// <returns>显示名称。</returns>
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
