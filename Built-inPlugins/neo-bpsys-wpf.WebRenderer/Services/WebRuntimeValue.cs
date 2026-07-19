using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>Web runtime 值的版本化、可序列化联合类型。</summary>
public sealed record WebRuntimeValue(string Kind, object? Value = null, string? SourceType = null,
    string? Diagnostic = null, WebRuntimeAsset? Asset = null);

/// <summary>浏览器可读取的动态资源描述。</summary>
public sealed record WebRuntimeAsset(string Kind, string Token, string Url, string ContentType,
    int? Width, int? Height, string Revision);

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
        if (value is null) return new("null", null, sourceType);
        if (value is string text) return new("string", text, sourceType);
        if (value is bool boolean) return new("boolean", boolean, sourceType);
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
            return new("number", Convert.ToDouble(value), sourceType);
        if (value.GetType().IsEnum) return new("enum", value.ToString(), sourceType);
        if (value is Color color) return new("color", $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}", sourceType);
        if (value is ImageSource image && _assets.TryRegister(image, out var asset, out var error)) return new("asset", null, sourceType, null, asset);
        diagnostic = new(bindingPath, value is ImageSource ? "RuntimeAssetConversionFailed" : "UnsupportedBindingValue", sourceType);
        return new("null", null, sourceType, diagnostic.Code);
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

    /// <summary>后台编码完成并可重新发布绑定值时发生。</summary>
    public event EventHandler? AssetReady;

    /// <summary>注册图片并返回不含物理路径的描述。</summary>
    public bool TryRegister(ImageSource source, out WebRuntimeAsset asset, out string? error)
    {
        asset = null!; error = null;
        lock (_gate)
        {
            if (_ready.TryGetValue(source, out var existing)) { asset = existing; return true; }
            if (_pending.Contains(source)) { error = "RuntimeAssetPending"; return false; }
            try
            {
                // 只在 UI 线程做轻量的冻结快照；PNG 编码和文件 I/O 永不占用导播 UI 线程。
                var snapshot = FreezeSnapshot(source);
                _pending.Add(source);
                _ = Task.Run(() => Encode(snapshot)).ContinueWith(task => Complete(source, task), TaskScheduler.Default);
                error = "RuntimeAssetPending";
                return false;
            }
            catch (Exception ex) { error = ex.GetType().Name; return false; }
        }
    }

    private static BitmapSource FreezeSnapshot(ImageSource source)
    {
        var bitmap = source as BitmapSource ?? Render(source);
        if (bitmap.IsFrozen) return bitmap;
        var snapshot = bitmap.CloneCurrentValue(); snapshot.Freeze(); return snapshot;
    }

    private static WebRuntimeAsset Encode(BitmapSource bitmap)
    {
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
        var bytes = stream.ToArray(); var token = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Directory.CreateDirectory(CacheRoot); var path = Path.Combine(CacheRoot, token + ".png");
        if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
        return new("image", token, "/runtime-assets/" + token, "image/png", bitmap.PixelWidth, bitmap.PixelHeight, token);
    }

    private void Complete(ImageSource source, Task<WebRuntimeAsset> task)
    {
        lock (_gate)
        {
            _pending.Remove(source);
            if (task.Status == TaskStatus.RanToCompletion) _ready[source] = task.Result;
        }
        if (task.Status == TaskStatus.RanToCompletion) AssetReady?.Invoke(this, EventArgs.Empty);
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

    private static BitmapSource Render(ImageSource source)
    {
        if (source.Width <= 0 || source.Height <= 0) throw new InvalidOperationException("ImageSourceSizeUnavailable");
        var visual = new System.Windows.Controls.Image { Source = source, Width = source.Width, Height = source.Height };
        visual.Measure(new System.Windows.Size(source.Width, source.Height)); visual.Arrange(new System.Windows.Rect(0, 0, source.Width, source.Height));
        var rendered = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(source.Width)), Math.Max(1, (int)Math.Ceiling(source.Height)), 96, 96, PixelFormats.Pbgra32);
        rendered.Render(visual); rendered.Freeze(); return rendered;
    }

    /// <inheritdoc />
    public void Dispose() { ReplaceReferences([]); lock (_gate) { _ready.Clear(); _pending.Clear(); } }
}
