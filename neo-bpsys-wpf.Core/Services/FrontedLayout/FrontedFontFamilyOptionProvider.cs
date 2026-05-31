using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.IO;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Provides system and built-in font choices for Designer v3.
/// </summary>
public class FrontedFontFamilyOptionProvider
{
    private const string BuiltInFontPackUriPrefix = "pack://application:,,,/Assets/Fonts/#";
    private readonly string? _fontDirectory;
    private IReadOnlyList<FrontedFontFamilyOption>? _cachedOptions;

    /// <summary>
    /// Initializes a provider that discovers bundled fonts from known app/source paths.
    /// </summary>
    public FrontedFontFamilyOptionProvider()
    {
    }

    /// <summary>
    /// Initializes a provider with an explicit bundled font directory.
    /// </summary>
    public FrontedFontFamilyOptionProvider(string? fontDirectory)
    {
        _fontDirectory = fontDirectory;
    }

    /// <summary>
    /// Gets built-in and system font options.
    /// </summary>
    public IReadOnlyList<FrontedFontFamilyOption> GetFontFamilyOptions()
    {
        return _cachedOptions ??= BuildOptions();
    }

    /// <summary>
    /// Creates a safe preview FontFamily for a stored layout value.
    /// </summary>
    public FontFamily CreatePreviewFontFamily(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return new FontFamily("Arial");
        }

        try
        {
            var hashIndex = storedValue.IndexOf('#');
            if (storedValue.Contains("pack://application:,,,", StringComparison.Ordinal)
                && hashIndex >= 0)
            {
                return new FontFamily(new Uri(storedValue[..hashIndex]), "./" + storedValue[hashIndex..]);
            }

            return new FontFamily(storedValue);
        }
        catch
        {
            return new FontFamily("Arial");
        }
    }

    /// <summary>
    /// Gets a display name for a stored layout value.
    /// </summary>
    public string GetDisplayName(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return string.Empty;
        }

        return GetFontFamilyOptions().FirstOrDefault(
                   option => string.Equals(option.Value, storedValue, StringComparison.Ordinal))?.DisplayName
               ?? ExtractFontName(storedValue);
    }

    private IReadOnlyList<FrontedFontFamilyOption> BuildOptions()
    {
        var options = new List<FrontedFontFamilyOption>();
        var seenValues = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in GetBuiltInFontNames())
        {
            var value = BuiltInFontPackUriPrefix + name;
            AddOption(options, seenValues, new FrontedFontFamilyOption
            {
                DisplayName = name,
                Value = value,
                PreviewFontFamily = CreatePreviewFontFamily(value),
                IsBuiltIn = true
            });
        }

        foreach (var fontFamily in Fonts.SystemFontFamilies.OrderBy(font => font.Source, StringComparer.CurrentCultureIgnoreCase))
        {
            AddOption(options, seenValues, new FrontedFontFamilyOption
            {
                DisplayName = fontFamily.Source,
                Value = fontFamily.Source,
                PreviewFontFamily = fontFamily,
                IsBuiltIn = false
            });
        }

        return options;
    }

    private IEnumerable<string> GetBuiltInFontNames()
    {
        var discoveredNames = DiscoverBuiltInFontNames();
        var knownNames = new[]
        {
            "Noto Sans",
            "华康POP1体W5",
            "汉仪第五人格体简",
            "Essay Text",
            "Selawik"
        };

        return discoveredNames
            .Concat(knownNames)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase);
    }

    private IEnumerable<string> DiscoverBuiltInFontNames()
    {
        var directory = ResolveFontDirectory();
        if (directory is null || !Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(Path.GetExtension(path), ".ttf", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(Path.GetExtension(path), ".otf", StringComparison.OrdinalIgnoreCase))
            .SelectMany(TryReadFontNamesFromFile)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal);
    }

    private string? ResolveFontDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_fontDirectory))
        {
            return _fontDirectory;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "Fonts")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "neo-bpsys-wpf", "Assets", "Fonts"))
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static IEnumerable<string> TryReadFontNamesFromFile(string path)
    {
        GlyphTypeface glyphTypeface;
        try
        {
            glyphTypeface = new GlyphTypeface(new Uri(path, UriKind.Absolute));
        }
        catch
        {
            yield break;
        }

        foreach (var name in glyphTypeface.FamilyNames.Values)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return name;
            }
        }
    }

    private static string ExtractFontName(string storedValue)
    {
        var hashIndex = storedValue.IndexOf('#');
        return hashIndex >= 0 && hashIndex < storedValue.Length - 1
            ? storedValue[(hashIndex + 1)..]
            : storedValue;
    }

    private static void AddOption(
        ICollection<FrontedFontFamilyOption> options,
        ISet<string> seenValues,
        FrontedFontFamilyOption option)
    {
        if (string.IsNullOrWhiteSpace(option.Value) || !seenValues.Add(option.Value))
        {
            return;
        }

        options.Add(option);
    }
}
