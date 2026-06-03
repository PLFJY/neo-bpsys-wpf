using neo_bpsys_wpf.Helpers;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Hosts existing backend pages in Classic Mode pop-up windows.
/// </summary>
public partial class ClassicPageHostWindow : FluentWindow
{
    public ClassicPageHostWindow(string titleKey, Page page)
    {
        InitializeComponent();
        Title = I18nHelper.GetLocalizedString(titleKey);
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
