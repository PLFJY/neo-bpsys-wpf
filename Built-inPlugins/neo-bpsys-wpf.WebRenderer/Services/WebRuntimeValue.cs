using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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

/// <summary>MapV2 控件的主程序最终显示投影。</summary>
public sealed record WebMapV2DisplayState(
    string MapKey,
    string MapDisplayName,
    string CampDisplayName,
    string TeamName,
    WebRuntimeAsset? TeamLogo,
    WebRuntimeAsset? MapImage,
    bool IsBanned,
    bool IsPicked,
    bool IsCampVisible,
    string? CampKey);

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

/// <summary>图片引用的来源分类。</summary>
public enum WebImageSourceKind
{
    /// <summary>本地文件 URI。</summary>
    LocalFile,
    /// <summary>远程 HTTP 或 HTTPS URI。</summary>
    RemoteHttp,
    /// <summary>可跨线程读取的冻结位图。</summary>
    FrozenBitmap,
    /// <summary>静态 bpui 或内置资源。</summary>
    StaticBpuiResource,
    /// <summary>业务值为空。</summary>
    Null,
    /// <summary>不受支持或不安全的来源。</summary>
    Invalid
}

/// <summary>浏览器可读取的动态资源描述。</summary>
/// <param name="Kind">资源种类。</param>
/// <param name="SourceKind">资源来源的 wire 分类。</param>
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
    string SourceKind,
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

/// <summary>不可变的远程图片 URI 描述符。</summary>
/// <param name="NormalizedUri">规范化 HTTP/HTTPS URI。</param>
/// <param name="Revision">由规范化 URI 生成的修订。</param>
/// <param name="Token">当前主程序会话内的代理令牌。</param>
public sealed record RemoteImageDescriptor(string NormalizedUri, string Revision, string Token);

/// <summary>运行时诊断。</summary>
public sealed record WebRuntimeDiagnostic(string BindingPath, string Code, string? SourceType);

/// <summary>将 WPF 值转换为受限的 Web runtime 值。</summary>
public sealed class WebRuntimeValueFactory(WebRuntimeAssetRegistry assets)
{
    private readonly WebRuntimeAssetRegistry _assets = assets;

    /// <summary>创建安全 runtime 值。</summary>
    /// <param name="value">原始绑定值。</param>
    /// <param name="bindingPath">绑定路径。</param>
    /// <param name="diagnostic">转换诊断。</param>
    /// <returns>可序列化的 runtime 值。</returns>
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
                return new("asset", null, sourceType, Asset: asset);

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
    private static readonly byte[] RemoteTokenKey = RandomNumberGenerator.GetBytes(32);
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _references = new(StringComparer.Ordinal);
    private readonly Dictionary<ImageSource, WebRuntimeAsset> _ready = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<ImageSource> _pending = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ImageSource, string> _failures = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ImageSource, RemoteRegistration> _remote = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<RemoteImageDescriptor> _remoteRequests = new();
    private bool _disposed;

    /// <summary>后台编码或远程准备状态变化、可重新发布绑定状态时发生。</summary>
    public event EventHandler? AssetStateChanged;

    /// <summary>分类一个 WPF 图片来源而不读取图片内容。</summary>
    /// <param name="source">图片来源。</param>
    /// <returns>来源分类。</returns>
    public static WebImageSourceKind Classify(ImageSource? source)
    {
        if (source is null) return WebImageSourceKind.Null;
        if (source is BitmapImage { UriSource: not null } bitmapImage)
        {
            var uri = bitmapImage.UriSource;
            if (uri.IsFile) return WebImageSourceKind.LocalFile;
            return IsRemoteUriAllowed(uri) ? WebImageSourceKind.RemoteHttp : WebImageSourceKind.Invalid;
        }
        return source is BitmapSource { IsFrozen: true }
            ? WebImageSourceKind.FrozenBitmap
            : WebImageSourceKind.Invalid;
    }

    /// <summary>分类静态布局图片引用。</summary>
    /// <param name="reference">布局资源引用。</param>
    /// <returns>静态资源或无效来源。</returns>
    public static WebImageSourceKind ClassifyStaticReference(string? reference) =>
        !string.IsNullOrWhiteSpace(reference)
        && (reference.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase)
            || reference.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
            ? WebImageSourceKind.StaticBpuiResource
            : WebImageSourceKind.Invalid;

    /// <summary>注册图片并返回不含物理路径或远程 URI 的描述。</summary>
    /// <param name="source">WPF 图片。</param>
    /// <param name="asset">已准备资源。</param>
    /// <param name="error">稳定诊断代码。</param>
    /// <returns>资源是否已经准备完成。</returns>
    public bool TryRegister(ImageSource source, out WebRuntimeAsset asset, out string? error)
    {
        asset = null!;
        error = null;
        lock (_gate)
        {
            if (_ready.TryGetValue(source, out var existing)) { asset = existing; return true; }
            if (_pending.Contains(source)) { error = "RuntimeAssetPending"; return false; }
            if (_failures.TryGetValue(source, out var failure)) { error = failure; return false; }

            try
            {
                switch (Classify(source))
                {
                    case WebImageSourceKind.RemoteHttp:
                        var bitmap = (BitmapImage)source;
                        var descriptor = CreateRemoteDescriptor(bitmap.UriSource);
                        _remote[source] = new RemoteRegistration(descriptor);
                        _pending.Add(source);
                        _remoteRequests.Enqueue(descriptor);
                        error = "RuntimeAssetPending";
                        return false;
                    case WebImageSourceKind.LocalFile:
                        // BitmapImage is a WPF DependencyObject. Capture UriSource on the
                        // registering (dispatcher) thread before entering Task.Run.
                        var localPath = ((BitmapImage)source).UriSource.LocalPath;
                        _pending.Add(source);
                        StartEncoding(source, Task.Run(() => EncodeFile(localPath)));
                        error = "RuntimeAssetPending";
                        return false;
                    case WebImageSourceKind.FrozenBitmap:
                        _pending.Add(source);
                        StartEncoding(source, Task.Run(() => Encode((BitmapSource)source, "frozen")));
                        error = "RuntimeAssetPending";
                        return false;
                    default:
                        error = "RuntimeAssetSourceRejected";
                        return false;
                }
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name;
                return false;
            }
        }
    }

    /// <summary>取出尚未发送给 sidecar 的远程图片请求。</summary>
    /// <returns>远程描述符快照。</returns>
    public IReadOnlyList<RemoteImageDescriptor> DrainRemoteRequests()
    {
        lock (_gate)
        {
            var result = _remoteRequests.ToArray();
            _remoteRequests.Clear();
            return result;
        }
    }

    /// <summary>应用 sidecar 返回的远程图片结果。</summary>
    /// <param name="token">代理令牌。</param>
    /// <param name="revision">资源修订。</param>
    /// <param name="contentType">成功时的 MIME 类型。</param>
    /// <param name="diagnostic">失败时的稳定诊断。</param>
    public void CompleteRemote(string token, string revision, string? contentType, string? diagnostic)
    {
        ImageSource[] changed;
        lock (_gate)
        {
            changed = _remote.Where(pair => pair.Value.Descriptor.Token == token
                                             && pair.Value.Descriptor.Revision == revision)
                .Select(pair => pair.Key).ToArray();
            foreach (var source in changed)
            {
                _pending.Remove(source);
                if (!string.IsNullOrWhiteSpace(contentType))
                {
                    _failures.Remove(source);
                    _remote[source].RetryCount = 0;
                    _ready[source] = new WebRuntimeAsset("image", "remote", token,
                        "/remote-assets/" + token, contentType, null, null, null, null, null, null, revision);
                }
                else
                {
                    var code = string.IsNullOrWhiteSpace(diagnostic) ? "RemoteAssetDownloadFailed" : diagnostic;
                    _failures[source] = code;
                    ScheduleRetry(source, _remote[source]);
                }
            }
        }
        if (changed.Length > 0) AssetStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>重置 sidecar 会话，使仍被引用的远程图片重新授权。</summary>
    public void ResetRemoteSession()
    {
        lock (_gate)
        {
            foreach (var source in _remote.Keys.ToArray())
            {
                _ready.Remove(source);
                _failures.Remove(source);
                _pending.Remove(source);
            }
            _remote.Clear();
            _remoteRequests.Clear();
        }
    }

    /// <summary>停止不再被当前绑定快照引用的远程资源重试。</summary>
    /// <param name="sources">当前绑定快照中的图片对象。</param>
    public void ReplaceRemoteSources(IEnumerable<ImageSource> sources)
    {
        var current = new HashSet<ImageSource>(sources, ReferenceEqualityComparer.Instance);
        lock (_gate)
        {
            foreach (var source in _remote.Keys.Where(source => !current.Contains(source)).ToArray())
            {
                _remote.Remove(source);
                _ready.Remove(source);
                _pending.Remove(source);
                _failures.Remove(source);
            }
        }
    }

    private static bool IsRemoteUriAllowed(Uri uri) => uri.IsAbsoluteUri
        && uri.Scheme is "http" or "https"
        && string.IsNullOrEmpty(uri.UserInfo)
        && !string.IsNullOrWhiteSpace(uri.Host);

    private static RemoteImageDescriptor CreateRemoteDescriptor(Uri uri)
    {
        if (!IsRemoteUriAllowed(uri)) throw new InvalidOperationException("RemoteAssetUriRejected");
        var builder = new UriBuilder(uri) { Fragment = string.Empty, Host = uri.IdnHost };
        if ((builder.Scheme == Uri.UriSchemeHttp && builder.Port == 80)
            || (builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443)) builder.Port = -1;
        var normalized = builder.Uri.AbsoluteUri;
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        var token = Convert.ToHexString(HMACSHA256.HashData(RemoteTokenKey, Encoding.UTF8.GetBytes(revision))).ToLowerInvariant();
        return new(normalized, revision, token);
    }

    private static WebRuntimeAsset Encode(BitmapSource bitmap, string sourceKind)
    {
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
        var bytes = stream.ToArray();
        var token = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Directory.CreateDirectory(CacheRoot);
        var path = Path.Combine(CacheRoot, token + ".png");
        if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
        return new("image", sourceKind, token, "/runtime-assets/" + token, "image/png",
            bitmap.Width, bitmap.Height, bitmap.PixelWidth, bitmap.PixelHeight, bitmap.DpiX, bitmap.DpiY, token);
    }

    private static WebRuntimeAsset EncodeFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return Encode(frame, "local");
    }

    private void StartEncoding(ImageSource source, Task<WebRuntimeAsset> encoding) =>
        _ = encoding.ContinueWith(task => CompleteEncoding(source, task), TaskScheduler.Default);

    private void CompleteEncoding(ImageSource source, Task<WebRuntimeAsset> task)
    {
        lock (_gate)
        {
            _pending.Remove(source);
            if (task.Status == TaskStatus.RanToCompletion) _ready[source] = task.Result;
            else _failures[source] = task.Exception?.GetBaseException().GetType().Name ?? "RuntimeAssetEncodingFailed";
        }
        AssetStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ScheduleRetry(ImageSource source, RemoteRegistration registration)
    {
        registration.RetryCount++;
        var delay = registration.RetryCount == 1 ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
        _ = Task.Delay(delay).ContinueWith(_ =>
        {
            lock (_gate)
            {
                if (_disposed || !_remote.TryGetValue(source, out var current)
                              || !ReferenceEquals(current, registration) || !_failures.Remove(source)) return;
                _pending.Add(source);
                _remoteRequests.Enqueue(registration.Descriptor);
            }
            AssetStateChanged?.Invoke(this, EventArgs.Empty);
        }, TaskScheduler.Default);
    }

    /// <summary>根据当前快照引用集合释放不再使用的本地资源。</summary>
    /// <param name="tokens">仍被稳定值引用的 token。</param>
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

    /// <summary>获取 sidecar 使用的受控本地资源路径。</summary>
    /// <param name="token">本地资源 token。</param>
    /// <returns>受控缓存路径；非法 token 返回 null。</returns>
    public static string? GetPath(string token)
    {
        if (token.Length != 64 || token.Any(character => !Uri.IsHexDigit(character))) return null;
        return Path.Combine(CacheRoot, token.ToLowerInvariant() + ".png");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ReplaceReferences([]);
        lock (_gate)
        {
            _disposed = true;
            _ready.Clear();
            _pending.Clear();
            _failures.Clear();
            _remote.Clear();
            _remoteRequests.Clear();
        }
    }

    private sealed class RemoteRegistration(RemoteImageDescriptor descriptor)
    {
        public RemoteImageDescriptor Descriptor { get; } = descriptor;
        public int RetryCount { get; set; }
    }
}
