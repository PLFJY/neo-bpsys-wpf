using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Controls.Modern.Frame;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// 在经典模式弹窗中承载现有后台页面。
/// </summary>
public partial class ClassicPageHostWindow : FluentWindow
{
    private readonly Page _page;
    private Window? _previousOwner;

    /// <summary>
    /// 初始化 <see cref="ClassicPageHostWindow"/> 类的新实例。
    /// </summary>
    /// <param name="title">已解析的窗口标题。</param>
    /// <param name="page">要承载的后台页面。</param>
    public ClassicPageHostWindow(string title, Page page)
    {
        InitializeComponent();
        Title = title;
        _page = page;
        PageHost.Navigate(page, new SuppressNavigationTransitionInfo());
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 在窗口实际关闭前断开 Owner 关系。
        // 关闭 owned window 时 WPF 会自动激活 Owner，但在当前 WindowChrome
        // (UseAeroCaptionButtons=False) 配置下该激活链路会把 Owner 误置于
        // Minimized 状态。断开 Owner 关系后 WPF 不再自动激活 Owner，从根源上
        // 避免该问题；关闭后由 OnClosed 手动激活原 Owner 以保持焦点自然过渡。
        _previousOwner = Owner;
        if (_previousOwner is not null)
        {
            Owner = null;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        PageHost.ClearJournal();
        // 释放 Page 与内部 ModernFramePageHost（Frame）的父子关系，
        // 避免 singleton Page 再次承载时触发 re-parent 异常。
        if (_page.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }

        base.OnClosed(e);

        // 手动激活原 Owner，确保焦点返回主窗口。
        // 由于 OnClosing 已断开 Owner 关系，WPF 不会自动激活 Owner，
        // 因此需要在这里手动激活。
        _previousOwner?.Activate();
    }
}
