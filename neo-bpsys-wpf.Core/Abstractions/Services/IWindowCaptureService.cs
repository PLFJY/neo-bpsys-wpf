using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 窗口捕获服务
/// </summary>
/// <remarks>
/// 该服务面向 Windows/WPF 运行时设计。实现应持有并释放非托管捕获资源，并在
/// <see cref="GetCurrentFrame"/> 返回前对帧调用 <c>Freeze()</c>，确保帧可跨 Dispatcher 线程读取。
/// 调用方仍应在不再使用该服务时释放宿主（实现通常同时实现 <see cref="IDisposable"/>）。
/// </remarks>
public interface IWindowCaptureService
{
    /// <summary>
    /// 获取当前可用于捕获的窗口列表。
    /// </summary>
    /// <returns>窗口信息列表；由实现返回独立快照。</returns>
    List<WindowInfo> ListActiveWindows();

    /// <summary>
    /// 当前是否处于捕获状态。
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// 以指定方式启动对目标窗口的捕获。
    /// </summary>
    /// <param name="window">目标窗口信息。</param>
    /// <param name="captureMethod">捕获方式。</param>
    /// <returns>启动成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    bool StartCapture(WindowInfo? window, CaptureMethod captureMethod);

    /// <summary>
    /// 打开系统窗口选择器并对用户所选窗口启动捕获。
    /// </summary>
    /// <returns>启动成功返回 <see langword="true"/>；用户取消或失败返回 <see langword="false"/>。</returns>
    Task<bool> StartCaptureWithPickerAsync();

    /// <summary>
    /// 获取当前缓存的最新帧。
    /// </summary>
    /// <returns>当前帧；若尚未产生有效帧则返回 <see langword="null"/>。</returns>
    BitmapSource? GetCurrentFrame();

    /// <summary>
    /// 停止当前捕获会话并释放相关资源。
    /// </summary>
    void StopCapture();

    /// <summary>
    /// 打开捕获预览窗口。
    /// </summary>
    void OpenPreviewWindow();
}
