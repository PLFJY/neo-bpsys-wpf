using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Windows;
using System.IO;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>生成供 Web sidecar 使用的、经过资源授权的静态布局快照。</summary>
public sealed class WebRendererBootstrapBuilder(
    IFrontedLayoutPackageManager packageManager,
    IFrontedLayoutService layoutService,
    IFrontedWindowRegistry windowRegistry,
    IFrontedBehaviorService behaviorService)
{
    private static readonly IReadOnlyDictionary<string, string> PackFonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Noto Sans"] = "NotoSans-Regular.ttf",
        ["华康POP1体W5"] = "华康POP1体W5-简体.ttf",
        ["汉仪第五人格体简"] = "汉仪第五人格体.ttf",
        ["Arial"] = "NotoSans-Regular.ttf"
    };

    /// <summary>创建当前活动包的完整快照。</summary>
    public async Task<WebRendererBootstrapSnapshot> BuildAsync(long generation, CancellationToken cancellationToken = default)
    {
        var active = await packageManager.GetActivePackageStateAsync(cancellationToken);
        var resources = new Dictionary<string, WebRendererAsset>(StringComparer.Ordinal);
        var windows = new List<WebRendererBootstrapWindow>();
        foreach (var descriptor in windowRegistry.GetCustomizableLayoutWindows())
        {
            var result = await layoutService.LoadWindowConfigWithMetadataAsync(descriptor.FullWindowType, cancellationToken);
            var diagnostics = new List<string>();
            if (result.Config is null)
            {
                diagnostics.Add(result.Error ?? "LayoutMissing");
                windows.Add(new(descriptor.FullWindowType, descriptor.DisplayName, null, null, new Dictionary<string, string>(), diagnostics));
                continue;
            }

            var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
            var behavior = await behaviorService.LoadDocumentAsync(descriptor.FullWindowType, cancellationToken);
            foreach (var reference in EnumerateResourceReferences(result.Config).Concat(EnumerateBehaviorResourceReferences(behavior)))
            {
                var asset = TryCreateAsset(reference, active.PackageId, diagnostics);
                if (asset is null)
                    continue;
                resources.TryAdd(asset.Token, asset);
                mapping[reference] = $"/bpui-assets/{asset.Token}";
            }
            windows.Add(new(descriptor.FullWindowType, descriptor.DisplayName, result.Config, behavior, mapping, diagnostics));
        }

        return new WebRendererBootstrapSnapshot(WebRendererProtocolVersion.Value, generation, active.PackageId, windows, resources);
    }

    private static IEnumerable<string> EnumerateBehaviorResourceReferences(FrontedBehaviorDocument document) =>
        document.ControlBehaviorSets.SelectMany(set => set.AnimationParts)
            .Select(part => part.ImagePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>();

    private IEnumerable<string> EnumerateResourceReferences(FrontedWindowConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.CanvasSettings.BackgroundImage)) yield return config.CanvasSettings.BackgroundImage;
        foreach (var control in config.ControlLayout.Controls.Values)
        {
            if (control is ImageFrontedControlConfig image)
            {
                if (!string.IsNullOrWhiteSpace(image.ImagePath)) yield return image.ImagePath;
                if (!string.IsNullOrWhiteSpace(image.LockImagePath)) yield return image.LockImagePath;
                if (!string.IsNullOrWhiteSpace(image.PickingBorderImagePath)) yield return image.PickingBorderImagePath;
            }
            if (control is IFrontedTextStyleConfig text && !string.IsNullOrWhiteSpace(text.FontFamily))
                yield return text.FontFamily;
        }
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
            return CombineInside(Path.Combine(AppConstants.ResourcesPath, "bpui"), value["Resources/".Length..]);
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
}

/// <summary>Web Runtime 协议版本。</summary>
public static class WebRendererProtocolVersion
{
    /// <summary>当前版本。</summary>
    public const int Value = 3;
}

/// <summary>不可变 bootstrap 快照。</summary>
public sealed record WebRendererBootstrapSnapshot(int ProtocolVersion, long Generation, string ActivePackageId,
    IReadOnlyList<WebRendererBootstrapWindow> Windows,
    IReadOnlyDictionary<string, WebRendererAsset> Assets);

/// <summary>单个窗口的安全布局数据。</summary>
public sealed record WebRendererBootstrapWindow(string FullWindowType, string DisplayName, FrontedWindowConfig? Layout,
    FrontedBehaviorDocument? BehaviorDocument,
    IReadOnlyDictionary<string, string> Resources, IReadOnlyList<string> Diagnostics);

/// <summary>sidecar 专用的已授权资源。</summary>
public sealed record WebRendererAsset(string Token, string Reference, string ContentType, string? FilePath, byte[]? Data)
{
    /// <summary>由物理文件创建资源。</summary>
    public static WebRendererAsset CreateFile(string reference, string path, string contentType) => new(CreateToken(), reference, contentType, path, null);
    /// <summary>由内存创建资源。</summary>
    public static WebRendererAsset CreateMemory(string reference, byte[] data, string contentType) => new(CreateToken(), reference, contentType, null, data);
    private static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
