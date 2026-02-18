using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 窗口捕获服务
/// </summary>
public interface IWindowCaptureService
{
    /// <summary>
    /// 获取当前可用于捕获的窗口列表。
    /// </summary>
    /// <returns>窗口信息列表。</returns>
    List<WindowInfo> ListActiveWindows();

    /// <summary>
    /// 当前是否处于捕获状态。
    /// </summary>
    public bool IsCapturing { get; }

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
