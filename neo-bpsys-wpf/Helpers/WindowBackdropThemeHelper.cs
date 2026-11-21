using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 让不使用 Mica 的 <see cref="FluentWindow"/> 在全局主题切换后保持纯色主题背景。
/// </summary>
internal static class WindowBackdropThemeHelper
{
    /// <summary>
    /// 为窗口附加主题切换后的背景恢复逻辑。窗口关闭时会自动解除订阅。
    /// </summary>
    /// <param name="window">需要固定为纯色背景的窗口。</param>
    public static void Attach(FluentWindow window)
    {
        ApplySolidBackground(window);

        void OnThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccentColor)
        {
            if (!window.Dispatcher.CheckAccess())
            {
                window.Dispatcher.BeginInvoke(() => OnThemeChanged(currentApplicationTheme, systemAccentColor));
                return;
            }

            // 全局 Apply 会按默认 Mica 背景刷新所有 FluentWindow；
            // 等这次刷新结束后再恢复纯色背景，避免窗口残留深色背景。
            window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => ApplySolidBackground(window)));
        }

        ApplicationThemeManager.Changed += OnThemeChanged;
        window.Closed += (_, _) => ApplicationThemeManager.Changed -= OnThemeChanged;
    }

    private static void ApplySolidBackground(FluentWindow window)
    {
        if (window.Dispatcher.HasShutdownStarted) return;

        window.WindowBackdropType = WindowBackdropType.None;
        window.SetResourceReference(Control.BackgroundProperty, "ApplicationBackgroundBrush");
    }
}
