using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.ViewModels.Pages;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>在主页面滚动视图之外展示完整的 SmartBP 识别诊断信息。</summary>
public partial class SmartBpRecognitionDebugLogWindow : FluentWindow
{
    /// <summary>初始化 SmartBP 识别调试日志窗口。</summary>
    /// <param name="viewModel">持有实时诊断文本的 SmartBP 页面视图模型。</param>
    public SmartBpRecognitionDebugLogWindow(SmartBpModuleContentViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void LogTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is SmartBpModuleContentViewModel { IsRecognitionDebugLogAutoScrollEnabled: true } &&
            sender is TextBox textBox)
            textBox.ScrollToEnd();
    }
}
