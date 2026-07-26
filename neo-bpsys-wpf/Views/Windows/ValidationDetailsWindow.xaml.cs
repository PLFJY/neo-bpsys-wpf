using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// 设计器 v3 的非模态校验详情表格。
/// </summary>
public partial class ValidationDetailsWindow : FluentWindow
{
    public ValidationDetailsWindow()
    {
        InitializeComponent();
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
