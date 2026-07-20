using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>Web runtime 值的版本化、可序列化联合类型。</summary>
/// <param name="Kind">值种类。</param>
/// <param name="Value">标量值。</param>
/// <param name="SourceType">原始 CLR 类型，仅用于诊断。</param>
/// <param name="Diagnostic">不包含业务内容或物理路径的诊断代码。</param>
/// <param name="Asset">已稳定解析的动态资源。</param>
/// <param name="State">动态值解析状态。</param>
public sealed record WebRuntimeValue(string Kind, object? Value = null, string? SourceType = null,
    string? Diagnostic = null, WebRuntimeAsset? Asset = null, string State = WebRuntimeValueStates.Resolved);

/// <summary>Web runtime 值的稳定解析状态。</summary>
public static class WebRuntimeValueStates
{
    /// <summary>值已经稳定解析。</summary>
    public const string Resolved = "resolved";
    /// <summary>资源仍在后台准备。</summary>
    public const string Pending = "pending";
    /// <summary>绑定的稳定业务值为空。</summary>
    public const string Null = "null";
    /// <summary>资源准备失败；消费者应保留同 generation 的上一稳定值。</summary>
    public const string Failed = "failed";
}

/// <summary>浏览器可读取的动态资源描述。</summary>
/// <param name="Kind">资源种类。</param>
/// <param name="Token">不包含物理路径的稳定资源令牌。</param>
/// <param name="Url">sidecar 授权的资源 URL。</param>
/// <param name="ContentType">资源 MIME 类型。</param>
/// <param name="NaturalWidthDip">WPF 自然宽度（DIP）。</param>
/// <param name="NaturalHeightDip">WPF 自然高度（DIP）。</param>
/// <param name="PixelWidth">源位图像素宽度。</param>
/// <param name="PixelHeight">源位图像素高度。</param>
/// <param name="DpiX">源位图水平 DPI。</param>
/// <param name="DpiY">源位图垂直 DPI。</param>
/// <param name="Revision">资源内容修订标识。</param>
public sealed record WebRuntimeAsset(
    string Kind,
    string Token,
    string Url,
    string ContentType,
    double? NaturalWidthDip,
    double? NaturalHeightDip,
    int? PixelWidth,
    int? PixelHeight,
    double? DpiX,
    double? DpiY,
    string Revision);

/// <summary>运行时诊断。</summary>
public sealed record WebRuntimeDiagnostic(string BindingPath, string Code, string? SourceType);

/// <summary>将 WPF 值转换为受限的 Web runtime 值。</summary>
public sealed class WebRuntimeValueFactory(WebRuntimeAssetRegistry assets)
{
    private readonly WebRuntimeAssetRegistry _assets = assets;

    /// <summary>创建安全 runtime 值。</summary>
    public WebRuntimeValue Create(object? value, string bindingPath, out WebRuntimeDiagnostic? diagnostic)
    {
        diagnostic = null;
        var sourceType = value?.GetType().FullName;
        if (value is null) return new("null", null, sourceType, State: WebRuntimeValueStates.Null);
        if (value is string text) return new("string", text, sourceType);
        if (value is bool boolean) return new("boolean", boolean, sourceType);
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
            return new("number", Convert.ToDouble(value), sourceType);
        if (value.GetType().IsEnum) return new("enum", value.ToString(), sourceType);
        if (value is Color color) return new("color", $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}", sourceType);
        if (value is ImageSource image)
        {
            if (_assets.TryRegister(image, out var asset, out var error))
            {
                return new("asset", null, sourceType, null, asset);
            }

            var state = string.Equals(error, "RuntimeAssetPending", StringComparison.Ordinal)
                ? WebRuntimeValueStates.Pending
                : WebRuntimeValueStates.Failed;
            diagnostic = new(bindingPath, error ?? "RuntimeAssetConversionFailed", sourceType);
            return new("asset", null, sourceType, diagnostic.Code, State: state);
        }

        diagnostic = new(bindingPath, "UnsupportedBindingValue", sourceType);
        return new("null", null, sourceType, diagnostic.Code, State: WebRuntimeValueStates.Failed);
    }
}

/// <summary>进程间共享受控缓存中的动态图片注册表。</summary>
public sealed class WebRuntimeAssetRegistry : IDisposable
{
    private static readonly string CacheRoot = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-web-runtime-assets");
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _references = new(StringComparer.Ordinal);
    private readonly Dictionary<ImageSource, WebRuntimeAsset> _ready = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<ImageSource> _pending = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ImageSource, string> _failures = new(ReferenceEqualityComparer.Instance);

    /// <summary>后台编码完成或失败、可重新发布绑定状态时发生。</summary>
    public event EventHandler? AssetStateChanged;

    /// <summary>注册图片并返回不含物理路径的描述。</summary>
    public bool TryRegister(ImageSource source, out WebRuntimeAsset asset, out string? error)
    {
        asset = null!; error = null;
        lock (_gate)
        {
            if (_ready.TryGetValue(source, out var existing)) { asset = existing; return true; }
            if (_pending.Contains(source)) { error = "RuntimeAssetPending"; return false; }
            if (_failures.TryGetValue(source, out var failure)) { error = failure; return false; }
            try
            {
                _pending.Add(source);
                // BitmapImage 的 UriSource 读取不触发解码。导入队伍信息时最常见的 Logo/
                // 定妆照走这条路径，后续打开文件、解码、PNG 编码和磁盘写入全部在后台完成。
                var encoding = source is BitmapImage { UriSource: { IsFile: true } uri }
                    ? Task.Run(() => EncodeFile(uri.LocalPath))
                    : source is BitmapSource { IsFrozen: true } bitmap
                        ? Task.Run(() => Encode(bitmap))
                        : Task.FromException<WebRuntimeAsset>(new InvalidOperationException("RuntimeAssetRequiresFrozenSource"));
                _ = encoding.ContinueWith(task => Complete(source, task), TaskScheduler.Default);
                error = "RuntimeAssetPending";
                return false;
            }
            catch (Exception ex) { error = ex.GetType().Name; return false; }
        }
    }

    private static WebRuntimeAsset Encode(BitmapSource bitmap)
    {
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
        var bytes = stream.ToArray(); var token = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Directory.CreateDirectory(CacheRoot); var path = Path.Combine(CacheRoot, token + ".png");
        if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
        return new(
            "image",
            token,
            "/runtime-assets/" + token,
            "image/png",
            bitmap.Width,
            bitmap.Height,
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            bitmap.DpiX,
            bitmap.DpiY,
            token);
    }

    private static WebRuntimeAsset EncodeFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return Encode(frame);
    }

    private void Complete(ImageSource source, Task<WebRuntimeAsset> task)
    {
        lock (_gate)
        {
            _pending.Remove(source);
            if (task.Status == TaskStatus.RanToCompletion) _ready[source] = task.Result;
            else _failures[source] = task.Exception?.GetBaseException().GetType().Name ?? "RuntimeAssetEncodingFailed";
        }
        AssetStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>根据当前快照引用集合释放不再使用的资源。</summary>
    public void ReplaceReferences(IEnumerable<string> tokens)
    {
        var current = tokens.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            foreach (var token in _references.Keys.Where(token => !current.Contains(token)).ToArray())
            {
                _references.Remove(token);
                try { File.Delete(Path.Combine(CacheRoot, token + ".png")); } catch { }
            }
            foreach (var token in current) _references[token] = 1;
        }
    }

    /// <summary>获取 sidecar 使用的受控资源路径。</summary>
    public static string? GetPath(string token)
    {
        if (token.Length != 64 || token.Any(character => !Uri.IsHexDigit(character))) return null;
        return Path.Combine(CacheRoot, token.ToLowerInvariant() + ".png");
    }

    /// <inheritdoc />
    public void Dispose() { ReplaceReferences([]); lock (_gate) { _ready.Clear(); _pending.Clear(); _failures.Clear(); } }
}
