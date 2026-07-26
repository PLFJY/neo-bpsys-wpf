using neo_bpsys_wpf.ViewModels.Windows;
using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// FrontedPackageFontManagerWindow.xaml 的交互逻辑。
/// </summary>
public partial class FrontedPackageFontManagerWindow : FluentWindow
{
    /// <summary>
    /// 初始化新的包字体管理器窗口。
    /// </summary>
    public FrontedPackageFontManagerWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 使用其视图模型初始化新的包字体管理器窗口。
    /// </summary>
    /// <param name="viewModel">窗口视图模型。</param>
    public FrontedPackageFontManagerWindow(FrontedPackageFontManagerWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    private FrontedPackageFontManagerWindowViewModel? ViewModel =>
        DataContext as FrontedPackageFontManagerWindowViewModel;

    private async void FrontedPackageFontManagerWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.LoadAsync();
        }
    }

    private async void DeleteSelectedFont_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.DeleteSelectedFontAsync();
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
