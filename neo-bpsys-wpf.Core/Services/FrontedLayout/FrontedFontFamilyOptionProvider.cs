using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Abstractions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Provides system and built-in font choices for Designer v3.
/// </summary>
public class FrontedFontFamilyOptionProvider
{
    private const string BuiltInFontPackUriPrefix = "pack://application:,,,/Assets/Fonts/#";
    private readonly string? _fontDirectory;
    private readonly IFrontedLayoutPackageManager? _packageManager;
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
    /// Initializes a provider with an active package manager.
    /// </summary>
    /// <param name="packageManager">Layout package manager.</param>
    public FrontedFontFamilyOptionProvider(IFrontedLayoutPackageManager packageManager)
    {
        _packageManager = packageManager;
    }

    /// <summary>
    /// Gets built-in and system font options.
    /// </summary>
    public IReadOnlyList<FrontedFontFamilyOption> GetFontFamilyOptions()
    {
        return _cachedOptions ??= BuildOptions();
    }

    /// <summary>
    /// Clears cached options so package font changes are visible.
    /// </summary>
    public void ClearCache()
    {
        _cachedOptions = null;
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
            return FrontedFontResourceHelper.CreateFontFamily(storedValue, CreateResolver());
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
               ?? FrontedFontResourceHelper.ExtractFontName(storedValue);
    }

    private IReadOnlyList<FrontedFontFamilyOption> BuildOptions()
    {
        var options = new List<FrontedFontFamilyOption>();
        var seenValues = new HashSet<string>(StringComparer.Ordinal);

        foreach (var option in DiscoverActivePackageFontOptions())
        {
            AddOption(options, seenValues, option);
        }

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
                           || string.Equals(Path.GetExtension(path), ".otf", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(Path.GetExtension(path), ".ttc", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => FrontedFontResourceHelper.ReadFontFamilyNames(path))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal);
    }

    private IEnumerable<FrontedFontFamilyOption> DiscoverActivePackageFontOptions()
    {
        if (_packageManager is null)
        {
            yield break;
        }

        FrontedLayoutActivePackageState state;
        try
        {
            state = _packageManager.GetActivePackageStateAsync().GetAwaiter().GetResult();
        }
        catch
        {
            yield break;
        }

        if (string.Equals(state.PackageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(state.PackageId, FrontedLayoutPackageManager.LocalPackageId, StringComparison.OrdinalIgnoreCase)
            || !FrontedLayoutPackageManager.IsSafePackageId(state.PackageId))
        {
            yield break;
        }

        var fontsRoot = Path.Combine(_packageManager.GetPackageRootFolder(), state.PackageId, "resources", "fonts");
        if (!Directory.Exists(fontsRoot))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(fontsRoot, "*.*", SearchOption.TopDirectoryOnly)
                     .Where(path => FrontedFontResourceHelper.IsSupportedFontExtension(Path.GetExtension(path)))
                     .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase))
        {
            foreach (var name in FrontedFontResourceHelper.ReadFontFamilyNames(path)
                         .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
            {
                var value = $"bpui://{state.PackageId}/resources/fonts/{Path.GetFileName(path)}#{name}";
                yield return new FrontedFontFamilyOption
                {
                    DisplayName = name,
                    Value = value,
                    PreviewFontFamily = CreateDirectoryFontFamily(path, name),
                    IsPackageFont = true,
                    BadgeText = "BPUI"
                };
            }
        }
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

    private static FrontedResourceResolver CreateResolver()
    {
        return new FrontedResourceResolver(NullLogger<FrontedResourceResolver>.Instance);
    }

    private static FontFamily CreateDirectoryFontFamily(string path, string name)
    {
        try
        {
            return new FontFamily(new Uri(Path.GetDirectoryName(path)! + Path.DirectorySeparatorChar), "./#" + name);
        }
        catch
        {
            return new FontFamily("Arial");
        }
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
