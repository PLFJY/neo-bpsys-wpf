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
    }
}
