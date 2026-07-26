using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.IO;
using System.Text.Json.Nodes;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 列出和删除存储在活动前台布局包中的字体。
/// </summary>
public sealed class FrontedPackageFontManager
{
    private readonly IFrontedLayoutPackageManager _packageManager;

    /// <summary>
    /// 初始化新的包字体管理器。
    /// </summary>
    /// <param name="packageManager">布局包管理器。</param>
    public FrontedPackageFontManager(IFrontedLayoutPackageManager packageManager)
    {
        _packageManager = packageManager;
    }

    /// <summary>
    /// 列出存储在活动可写包中的字体文件。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包字体项。</returns>
    public async Task<IReadOnlyList<FrontedPackageFontItem>> ListActivePackageFontsAsync(
        CancellationToken cancellationToken = default)
    {
        var packageId = await GetManageableActivePackageIdAsync(cancellationToken);
        if (packageId is null)
        {
            return [];
        }

        var fontsRoot = GetFontsRoot(packageId);
        if (!Directory.Exists(fontsRoot))
        {
            return [];
        }

        var references = CountFontReferences(packageId, cancellationToken);
        var items = new List<FrontedPackageFontItem>();
        foreach (var path in Directory.EnumerateFiles(fontsRoot, "*.*", SearchOption.TopDirectoryOnly)
                     .Where(path => FrontedFontResourceHelper.IsSupportedFontExtension(Path.GetExtension(path)))
                     .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(path);
            var familyNames = FrontedFontResourceHelper.ReadFontFamilyNames(path)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            var uriPrefix = CreateFontUriPrefix(packageId, fileName);
            items.Add(new FrontedPackageFontItem
            {
                FileName = fileName,
                PhysicalPath = path,
                FontFamilyNames = familyNames,
                ResourceUris = familyNames.Select(name => uriPrefix + "#" + name).ToArray(),
                ReferenceCount = references.GetValueOrDefault(uriPrefix)
            });
        }

        return items;
    }

    /// <summary>
    /// 从活动可写包中删除未引用的字体文件。
    /// </summary>
    /// <param name="fileName"><c>resources/fonts</c> 下的字体文件名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="ArgumentException">当文件名不安全或扩展名不受支持时抛出。</exception>
    /// <exception cref="InvalidOperationException">当活动包不可管理或字体仍被引用时抛出。</exception>
    /// <exception cref="FileNotFoundException">当字体文件不存在时抛出。</exception>
    public async Task DeleteActivePackageFontAsync(string fileName, CancellationToken cancellationToken = default)
    {
        ValidateFontFileName(fileName);
        var packageId = await GetManageableActivePackageIdAsync(cancellationToken)
                        ?? throw new InvalidOperationException("No manageable active layout package.");
        var fontsRoot = GetFontsRoot(packageId);
        var path = Path.GetFullPath(Path.Combine(fontsRoot, fileName));
        var normalizedRoot = Path.GetFullPath(fontsRoot) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Unsafe package font path.", nameof(fileName));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Package font file was not found.", path);
        }

        var uriPrefix = CreateFontUriPrefix(packageId, fileName);
        var references = CountFontReferences(packageId, cancellationToken);
        if (references.GetValueOrDefault(uriPrefix) > 0)
        {
            throw new InvalidOperationException("Package font is still referenced by the active layout package.");
        }

        File.Delete(path);
    }

    private async Task<string?> GetManageableActivePackageIdAsync(CancellationToken cancellationToken)
    {
        var state = await _packageManager.GetActivePackageStateAsync(cancellationToken);
        if (string.Equals(state.PackageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(state.PackageId, FrontedLayoutPackageManager.LocalPackageId, StringComparison.OrdinalIgnoreCase)
            || !FrontedLayoutPackageManager.IsSafePackageId(state.PackageId))
        {
            return null;
        }

        return state.PackageId;
    }

    private string GetFontsRoot(string packageId) =>
        Path.Combine(_packageManager.GetPackageRootFolder(), packageId, "resources", "fonts");

    private Dictionary<string, int> CountFontReferences(string packageId, CancellationToken cancellationToken)
    {
        var references = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var layoutsRoot = _packageManager.GetPackageLayoutsRootFolder(packageId);
        if (!Directory.Exists(layoutsRoot))
        {
            return references;
        }

        foreach (var path in Directory.EnumerateFiles(layoutsRoot, "*.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = JsonNode.Parse(File.ReadAllText(path));
                CountFontReferences(json, packageId, references);
            }
            catch
            {
                // Broken layout files are handled by the layout service; font management ignores them.
            }
        }

        return references;
    }

    private static void CountFontReferences(JsonNode? node, string packageId, IDictionary<string, int> references)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var child in jsonObject)
                {
                    CountFontReferences(child.Value, packageId, references);
                }

                break;
            case JsonArray jsonArray:
                foreach (var child in jsonArray)
                {
                    CountFontReferences(child, packageId, references);
                }

                break;
            case JsonValue value when value.TryGetValue<string>(out var text):
                var prefix = ExtractPackageFontUriPrefix(text, packageId);
                if (prefix is not null)
                {
                    references[prefix] = references.TryGetValue(prefix, out var count)
                        ? count + 1
                        : 1;
                }

                break;
        }
    }

    private static string? ExtractPackageFontUriPrefix(string text, string packageId)
    {
        var packageFontPrefix = $"bpui://{packageId}/resources/fonts/";
        if (!text.StartsWith(packageFontPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hashIndex = text.IndexOf('#');
        return hashIndex > packageFontPrefix.Length
            ? text[..hashIndex]
            : text;
    }

    private static string CreateFontUriPrefix(string packageId, string fileName) =>
        $"bpui://{packageId}/resources/fonts/{fileName}";

    private static void ValidateFontFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
            || !FrontedFontResourceHelper.IsSupportedFontExtension(Path.GetExtension(fileName)))
        {
            throw new ArgumentException("Invalid package font file name.", nameof(fileName));
        }
    }
}
