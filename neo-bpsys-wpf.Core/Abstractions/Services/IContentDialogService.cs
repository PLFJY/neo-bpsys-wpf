using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 为后台窗口中的 <see cref="ContentDialog"/> 提供宿主和显示入口。
/// </summary>
public interface IContentDialogService
{
    /// <summary>
    /// 设置用于承载内容对话框的窗口根宿主。
    /// </summary>
    /// <param name="dialogHost">内容对话框宿主。</param>
    /// <exception cref="ArgumentNullException"><paramref name="dialogHost"/> 为 <see langword="null"/> 时抛出。</exception>
    void SetContentDialogHost(ContentDialogHost dialogHost);

    /// <summary>
    /// 在当前宿主中异步显示内容对话框。
    /// </summary>
    /// <param name="dialog">要显示的内容对话框。</param>
    /// <param name="cancellationToken">用于取消等待对话框结果的令牌。</param>
    /// <returns>用户关闭对话框时的结果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dialog"/> 为 <see langword="null"/> 时抛出。</exception>
    /// <exception cref="InvalidOperationException">尚未设置宿主，或对话框已绑定到其他宿主时抛出。</exception>
    Task<ContentDialogResult> ShowAsync(ContentDialog dialog, CancellationToken cancellationToken = default);
}
