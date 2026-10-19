using Microsoft.Extensions.Logging;
using System.IO;
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
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private CancellationTokenSource? _request;
    private bool _started;

    internal bool HasStarted => _started;

    internal bool TryLoad(string source, Action<ImageSource> apply)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (!_started)
        {
            _started = true;
            var request = new CancellationTokenSource();
            _request = request;
            _ = LoadAsync(uri, Dispatcher.CurrentDispatcher, request, apply);
        }

        return true;
    }

    internal void Reset()
    {
        _request?.Cancel();
        _request = null;
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
                var bytes = await Client.GetByteArrayAsync(uri, request.Token).ConfigureAwait(false);
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
}
