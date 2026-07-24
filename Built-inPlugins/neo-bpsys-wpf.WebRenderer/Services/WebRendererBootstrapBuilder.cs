using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.WebRenderer.Protocol;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Windows;
using System.IO;
using System.Globalization;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>生成供 Web sidecar 使用的、经过资源授权的静态布局快照。</summary>
public sealed class WebRendererBootstrapBuilder(
    IFrontedLayoutPackageManager packageManager,
    IFrontedLayoutService layoutService,
    IFrontedWindowRegistry windowRegistry,
    IFrontedBehaviorService behaviorService,
    IWebLocalizationProvider localizationProvider,
    ISettingsHostService settingsHostService)
{
    private static readonly IReadOnlyDictionary<string, string> PackFonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Noto Sans"] = "NotoSans-Regular.ttf",
        ["华康POP1体W5"] = "华康POP1体W5-简体.ttf",
        ["汉仪第五人格体简"] = "汉仪第五人格体.ttf"
    };

    /// <summary>创建当前活动包的完整快照。</summary>
    public async Task<WebRendererBootstrapSnapshot> BuildAsync(long generation, CancellationToken cancellationToken = default)
    {
        var active = await packageManager.GetActivePackageStateAsync(cancellationToken);
        var resources = new Dictionary<string, WebRendererAsset>(StringComparer.Ordinal);
        // 内存资源（当前为内置字体）必须按引用复用。否则每一个使用同一字体的控件都会
        // 把相同 base64 数据再写进 bootstrap，既浪费 IPC 带宽也可能耗尽主程序内存。
        var assetsByReference = new Dictionary<string, WebRendererAsset>(StringComparer.Ordinal);
        var windows = new List<WebRendererBootstrapWindow>();
        var culture = settingsHostService.Settings.CultureInfo;
        var language = settingsHostService.Settings.Language;
        foreach (var registration in windowRegistry.GetV3LayoutWindows())
        {
            var result = await layoutService.LoadWindowConfigWithMetadataAsync(registration.Id, cancellationToken);
            var diagnostics = new List<string>();
            var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
            FrontedBehaviorDocument? behavior = null;
            var behaviorLoaded = false;
            try
            {
                behavior = await behaviorService.LoadDocumentAsync(registration.Id, cancellationToken);
                behaviorLoaded = true;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                diagnostics.Add($"BehaviorLoadFailed:{ex.Message}");
            }
            foreach (var reference in EnumerateResourceReferences(result.Config).Concat(behavior is null ? [] : EnumerateBehaviorResourceReferences(behavior)))
            {
                if (!assetsByReference.TryGetValue(reference, out var asset))
                {
                    asset = TryCreateAsset(reference, active.PackageId, diagnostics);
                    if (asset is not null)
                    {
                        assetsByReference.Add(reference, asset);
                        resources.Add(asset.Token, asset);
                    }
                }
                if (asset is null)
                    continue;
                mapping[reference] = $"/bpui-assets/{asset.Token}";
            }
            windows.Add(new(registration.Id, localizationProvider.ResolveWindowDisplayName(registration, language, culture), true, true, behaviorLoaded,
                mapping.Count, result.Config, behavior, mapping, diagnostics)
            {
                DefaultPickingBorderResourceUrl = mapping.GetValueOrDefault(WebRendererResourceKeys.PickingBorder)
            });
        }

        var snapshot = new WebRendererBootstrapSnapshot(WebRendererIpcProtocol.Version, generation, active.PackageId, windows, resources);
        var localization = await Task.Run(
            () => BuildLocalization(snapshot, culture, generation, language),
            cancellationToken);
        return snapshot with { Localization = localization };
    }

    /// <summary>使用既有布局生成指定文化的独立本地化快照。</summary>
    /// <param name="bootstrap">已构建的布局快照。</param>
    /// <param name="culture">目标文化。</param>
    /// <param name="revision">本地化修订号。</param>
    /// <returns>不可变本地化快照。</returns>
    public WebLocalizationSnapshot BuildLocalization(
        WebRendererBootstrapSnapshot bootstrap,
        CultureInfo culture,
        long revision) =>
        BuildLocalization(bootstrap, culture, revision, settingsHostService.Settings.Language);

    private WebLocalizationSnapshot BuildLocalization(
        WebRendererBootstrapSnapshot bootstrap,
        CultureInfo culture,
        long revision,
        neo_bpsys_wpf.Core.Enums.LanguageKey language)
    {
        var requests = new List<WebLocalizationRequest>();
        foreach (var window in bootstrap.Windows)
        {
            requests.Add(new($"window:{window.FullWindowType}", null, window.DisplayName));
            if (window.Layout is null)
            {
                continue;
            }

            foreach (var pair in EnumerateControls(window.Layout))
            {
                if (pair.Value is not LocalizedTextControlConfig localized
                    || localized.TextBinding?.GetActiveSources().Count > 0)
                {
                    continue;
                }

                requests.Add(new(
                    GetControlId(window.FullWindowType, pair.Key),
                    localized.LocalizationKey,
                    localized.FallbackText));
            }
        }

        var snapshot = localizationProvider.Create(requests, culture, revision);
        var windowTexts = snapshot.StaticTexts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var registration in windowRegistry.GetV3LayoutWindows())
        {
            windowTexts[$"window:{registration.Id}"] =
                localizationProvider.ResolveWindowDisplayName(registration, language, culture);
        }

        return snapshot with { StaticTexts = windowTexts };
    }

    private static IEnumerable<KeyValuePair<string, FrontedControlConfigBase>> EnumerateControls(FrontedWindowConfig window)
    {
        foreach (var pair in window.ControlLayout.Controls)
        {
            yield return pair;
        }

        foreach (var state in window.CanvasSettings.BoModeStates.Values)
        {
            foreach (var pair in state.Controls)
            {
                yield return pair;
            }
        }
    }

    /// <summary>生成与布局和 runtime 共用的稳定控件身份。</summary>
    /// <param name="windowType">窗口类型。</param>
    /// <param name="controlName">布局控件名称。</param>
    /// <returns>稳定控件 ID。</returns>
    public static string GetControlId(string windowType, string controlName) => $"control:{windowType}:{controlName}";

    private static IEnumerable<string> EnumerateBehaviorResourceReferences(FrontedBehaviorDocument document) =>
        document.ControlBehaviorSets.SelectMany(set => set.AnimationParts)
            .Select(part => part.ImagePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>();

    private IEnumerable<string> EnumerateResourceReferences(FrontedWindowConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.CanvasSettings.BackgroundImage)) yield return config.CanvasSettings.BackgroundImage;
        foreach (var state in config.CanvasSettings.BoModeStates.Values)
        {
            if (!string.IsNullOrWhiteSpace(state.BackgroundImage)) yield return state.BackgroundImage;
            foreach (var reference in EnumerateResourceReferences(state.Controls)) yield return reference;
        }
        foreach (var reference in EnumerateResourceReferences(config.ControlLayout.Controls)) yield return reference;
    }

    private IEnumerable<string> EnumerateResourceReferences(IReadOnlyDictionary<string, FrontedControlConfigBase> controls)
    {
        if (controls.Values.Any(control => control is MapV2DisplayControlConfig))
        {
            yield return "Resources/surIcon.png";
            yield return "Resources/hunIcon.png";
        }
        foreach (var control in controls.Values)
        {
            if (control is ImageFrontedControlConfig image)
            {
                if (!string.IsNullOrWhiteSpace(image.ImagePath)) yield return image.ImagePath;
                if (!string.IsNullOrWhiteSpace(image.LockImagePath)) yield return image.LockImagePath;
                if (image.PickingBorderAvailable)
                    yield return string.IsNullOrWhiteSpace(image.PickingBorderImagePath) ? WebRendererResourceKeys.PickingBorder : image.PickingBorderImagePath;
            }
            if (control is MapV2DisplayControlConfig map)
            {
                yield return string.IsNullOrWhiteSpace(map.PickingBorderImagePath) ? WebRendererResourceKeys.PickingBorder : map.PickingBorderImagePath;
                foreach (var font in new[] { map.MapNameFontFamily, map.TeamNameFontFamily, map.CampNameFontFamily })
                    if (!string.IsNullOrWhiteSpace(font) && ClassifyFontReference(font) is not WebFontReferenceKind.SystemFont) yield return font;
            }
            if (control is IFrontedTextStyleConfig text && !string.IsNullOrWhiteSpace(text.FontFamily)
                && ClassifyFontReference(text.FontFamily) is not WebFontReferenceKind.SystemFont)
                yield return text.FontFamily;
        }
    }

    /// <summary>仅按字体引用形式分类，不探测系统字体或文件。</summary>
    /// <param name="reference">字体族或字体资源引用。</param>
    /// <returns>字体引用分类。</returns>
    public static WebFontReferenceKind ClassifyFontReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return WebFontReferenceKind.Invalid;
        if (reference.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            return reference.Contains('#') ? WebFontReferenceKind.ApplicationPack : WebFontReferenceKind.Invalid;
        if (reference.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase))
            return IsFontFile(reference.Split('#')[0]) ? WebFontReferenceKind.PackageFont : WebFontReferenceKind.Invalid;
        if (reference.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
            return IsFontFile(reference.Split('#')[0]) ? WebFontReferenceKind.PackageFont : WebFontReferenceKind.Invalid;
        return WebFontReferenceKind.SystemFont;
    }

    private WebRendererAsset? TryCreateAsset(string reference, string activePackageId, List<string> diagnostics)
    {
        try
        {
            if (reference.StartsWith("pack://application:,,,/Assets/Fonts/#", StringComparison.OrdinalIgnoreCase))
            {
                var family = reference[(reference.LastIndexOf('#') + 1)..];
                if (!PackFonts.TryGetValue(family, out var fileName))
                {
                    diagnostics.Add($"UnsupportedPackFont:{family}");
                    return null;
                }
                var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Fonts/{fileName}", UriKind.Absolute))?.Stream;
                if (stream is null) { diagnostics.Add($"PackFontMissing:{family}"); return null; }
                using (stream)
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return WebRendererAsset.CreateMemory(reference, memory.ToArray(), "font/ttf");
                }
            }

            var filePath = ResolveSafeFile(reference, activePackageId);
            if (filePath is null || !File.Exists(filePath))
            {
                diagnostics.Add($"ResourceUnavailable:{reference}");
                return null;
            }
            return WebRendererAsset.CreateFile(reference, filePath, GetContentType(filePath));
        }
        catch (Exception)
        {
            diagnostics.Add($"ResourceRejected:{reference}");
            return null;
        }
    }

    private string? ResolveSafeFile(string value, string activePackageId)
    {
        if (Path.IsPathRooted(value) || value.Contains('\\')) return null;
        if (value.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
            return CombineInside(Path.Combine(AppConstants.ResourcesPath, "bpui"), value["Resources/".Length..].Split('#')[0]);
        if (!value.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase)) return null;

        var raw = value["bpui://".Length..];
        var slash = raw.IndexOf('/');
        if (slash <= 0) return null;
        var packageId = DecodeSafe(raw[..slash]);
        var relative = DecodeSafe(raw[(slash + 1)..].Split('#')[0]);
        if (packageId is null || relative is null) return null;
        if (!string.Equals(packageId, activePackageId, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(packageId, "local", StringComparison.OrdinalIgnoreCase)) return null;
        var root = string.Equals(packageId, "builtin", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(AppConstants.ResourcesPath, "bpui")
            : Path.Combine(packageManager.GetPackageRootFolder(), packageId);
        return CombineInside(root, relative);
    }

    private static string? DecodeSafe(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('%')) return null;
        var decoded = Uri.UnescapeDataString(value);
        if (decoded.Contains('%') || decoded.Contains('\\') || Path.IsPathRooted(decoded)) return null;
        if (decoded.Split('/').Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or "..")) return null;
        return decoded;
    }

    private static string? CombineInside(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        return candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".gif" => "image/gif",
        ".ttf" => "font/ttf", ".otf" => "font/otf", ".woff" => "font/woff", ".woff2" => "font/woff2", _ => "application/octet-stream"
    };

    private static bool IsFontFile(string reference) => Path.GetExtension(reference).ToLowerInvariant()
        is ".ttf" or ".otf" or ".woff" or ".woff2";
}

/// <summary>Web Renderer 与布局资源解析器共享的稳定资源引用。</summary>
public static class WebRendererResourceKeys
{
    /// <summary>WPF Image 与 MapV2 使用的默认 PickingBorder alpha mask。</summary>
    public const string PickingBorder = "Resources/pickingBorder.png";
}

/// <summary>Web Renderer 字体引用分类。</summary>
public enum WebFontReferenceKind
{
    /// <summary>由浏览器和操作系统解析的普通字体族。</summary>
    SystemFont,
    /// <summary>应用 pack 内嵌字体。</summary>
    ApplicationPack,
    /// <summary>活动包或内置 Resources 中的字体文件。</summary>
    PackageFont,
    /// <summary>格式无效的资源式字体引用。</summary>
    Invalid
}

/// <summary>不可变 bootstrap 快照。</summary>
public sealed record WebRendererBootstrapSnapshot(int ProtocolVersion, long Generation, string ActivePackageId,
    IReadOnlyList<WebRendererBootstrapWindow> Windows,
    IReadOnlyDictionary<string, WebRendererAsset> Assets)
{
    /// <summary>bootstrap 结构版本。</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>主程序当前文化下的本地化快照。</summary>
    public WebLocalizationSnapshot? Localization { get; init; }
}

/// <summary>单个窗口的安全布局数据。</summary>
public sealed record WebRendererBootstrapWindow(string FullWindowType, string DisplayName, bool DescriptorFound,
    bool LayoutLoaded, bool BehaviorLoaded, int ResourceCount, FrontedWindowConfig? Layout,
    FrontedBehaviorDocument? BehaviorDocument,
    IReadOnlyDictionary<string, string> Resources, IReadOnlyList<string> Diagnostics)
{
    /// <summary>主程序解析出的默认 PickingBorder mask URL。</summary>
    public string? DefaultPickingBorderResourceUrl { get; init; }
}

/// <summary>sidecar 专用的已授权资源。</summary>
public sealed record WebRendererAsset(string Token, string Reference, string ContentType, string? FilePath, byte[]? Data)
{
    /// <summary>由物理文件创建资源。</summary>
    public static WebRendererAsset CreateFile(string reference, string path, string contentType) => new(CreateToken(), reference, contentType, path, null);
    /// <summary>由内存创建资源。</summary>
    public static WebRendererAsset CreateMemory(string reference, byte[] data, string contentType) => new(CreateToken(), reference, contentType, null, data);
    private static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
