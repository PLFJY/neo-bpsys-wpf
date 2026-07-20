using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// 在经典模式弹窗中承载现有后台页面。
/// </summary>
public partial class ClassicPageHostWindow : FluentWindow
{
    /// <summary>
    /// 初始化 <see cref="ClassicPageHostWindow"/> 类的新实例。
    /// </summary>
    /// <param name="title">已解析的窗口标题。</param>
    /// <param name="page">要承载的后台页面。</param>
    public ClassicPageHostWindow(string title, Page page)
    {
        InitializeComponent();
        Title = title;
        PageHost.Navigate(page);
    }

    protected override void OnClosed(EventArgs e)
    {
        PageHost.Content = null;
        while (PageHost.RemoveBackEntry() is not null)
        {
        }

        base.OnClosed(e);
    }
}
