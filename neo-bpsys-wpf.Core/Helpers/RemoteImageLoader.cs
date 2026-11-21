using Microsoft.Extensions.Logging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Core.Helpers;

/// <summary>
/// 按模型实例异步下载、解码在线图片，并在读取图片的 Dispatcher 上发布结果。
/// </summary>
internal sealed class RemoteImageLoader
{
    private const long MaxImageBytes = 20L * 1024 * 1024;
    private const int MaxRedirects = 3;
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = false
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private CancellationTokenSource? _request;
    private Uri? _currentUri;
    private bool _started;

    internal bool HasStarted => _started;

    internal bool TryLoad(string source, Action<ImageSource> apply)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || !RemoteUriSafety.IsSafeRemoteHttpUri(uri))
        {
            return false;
        }

        if (_started && _currentUri == uri)
        {
            return true;
        }

        // 图片源发生变化时取消旧请求并重新加载；旧请求的 finished 回调不会覆盖新值。
        _request?.Cancel();
        _started = true;
        _currentUri = uri;
        var request = new CancellationTokenSource();
        _request = request;
        _ = LoadAsync(uri, Dispatcher.CurrentDispatcher, request, apply);
        return true;
    }

    internal void Reset()
    {
        _request?.Cancel();
        _request = null;
        _currentUri = null;
        _started = false;
    }

    private async Task LoadAsync(
        Uri uri,
        Dispatcher dispatcher,
        CancellationTokenSource request,
        Action<ImageSource> apply)
    {
        try
        {
            // 不让网络初始化、WIC 解码或延迟像素解码进入属性 getter / UI 线程。
            var image = await Task.Run(async () =>
            {
                var bytes = await DownloadImageBytesAsync(uri, request.Token).ConfigureAwait(false);
                request.Token.ThrowIfCancellationRequested();
                using var stream = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }, request.Token).ConfigureAwait(false);

            if (!dispatcher.HasShutdownStarted)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    if (ReferenceEquals(_request, request) && !request.IsCancellationRequested)
                    {
                        apply(image);
                    }
                }, DispatcherPriority.DataBind, request.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // 更换 URI 或设置本地图片后，旧请求不得覆盖新值。
        }
        catch (Exception ex)
        {
            IAppHost.TryGetService<ILogger>()?.LogWarning(ex, "Failed to load remote image from {Uri}", uri);
        }
        finally
        {
            // _request 由 Dispatcher 管理；完成后仍保留 started，失败不会触发绑定重试风暴。
            try
            {
                if (!dispatcher.HasShutdownStarted)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        if (ReferenceEquals(_request, request)) _request = null;
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Dispatcher 已关闭，模型不再发布更新。
            }
            finally
            {
                request.Dispose();
            }
        }
    }

    private static async Task<byte[]> DownloadImageBytesAsync(Uri initialUri, CancellationToken cancellationToken)
    {
        var currentUri = initialUri;
        for (var redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            if (!await RemoteUriSafety.IsPublicHostAsync(currentUri, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Remote image host is not a public address.");
            }

            using var response = await Client
                .GetAsync(currentUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.MovedPermanently
                or HttpStatusCode.Redirect
                or HttpStatusCode.RedirectMethod
                or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location;
                if (location is null)
                {
                    throw new InvalidOperationException("Remote image redirect has no location.");
                }

                currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                continue;
            }

            response.EnsureSuccessStatusCode();
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > MaxImageBytes)
            {
                throw new InvalidDataException("Remote image exceeds the size limit.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream(contentLength is > 0 and <= MaxImageBytes
                ? (int)contentLength.Value
                : 64 * 1024);
            var chunk = new byte[64 * 1024];
            long total = 0;
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > MaxImageBytes)
                {
                    throw new InvalidDataException("Remote image exceeds the size limit.");
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }

        throw new InvalidDataException("Remote image has too many redirects.");
    }
}
