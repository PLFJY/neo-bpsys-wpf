using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Abstractions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Windows.Media;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 为设计器 v3 提供系统和内置字体选择。
/// </summary>
public class FrontedFontFamilyOptionProvider
{
    private const string BuiltInFontPackUriPrefix = "pack://application:,,,/Assets/Fonts/#";
    private readonly string? _fontDirectory;
    private readonly IFrontedLayoutPackageManager? _packageManager;
    private IReadOnlyList<FrontedFontFamilyOption>? _cachedOptions;

    /// <summary>
    /// 初始化从已知应用/源路径发现捆绑字体的提供程序。
    /// </summary>
    public FrontedFontFamilyOptionProvider()
    {
    }

    /// <summary>
    /// 使用显式捆绑字体目录初始化提供程序。
    /// </summary>
    public FrontedFontFamilyOptionProvider(string? fontDirectory)
    {
        _fontDirectory = fontDirectory;
    }

    /// <summary>
    /// 使用活动包管理器初始化提供程序。
    /// </summary>
    /// <param name="packageManager">布局包管理器。</param>
    public FrontedFontFamilyOptionProvider(IFrontedLayoutPackageManager packageManager)
    {
        _packageManager = packageManager;
    }

    /// <summary>
    /// 获取内置和系统字体选项。
    /// </summary>
    public IReadOnlyList<FrontedFontFamilyOption> GetFontFamilyOptions()
    {
        return _cachedOptions ??= BuildOptions();
    }

    /// <summary>
    /// 清除缓存选项，使包字体更改可见。
    /// </summary>
    public void ClearCache()
    {
        _cachedOptions = null;
    }

    /// <summary>
    /// 为存储的布局值创建安全的预览 FontFamily。
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
    /// 获取存储布局值的显示名称。
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
