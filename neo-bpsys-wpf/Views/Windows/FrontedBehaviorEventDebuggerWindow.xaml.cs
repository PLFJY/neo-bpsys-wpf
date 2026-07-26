using neo_bpsys_wpf.ViewModels.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// 独立的全局行为事件调试器窗口。
/// </summary>
public partial class FrontedBehaviorEventDebuggerWindow : FluentWindow
{
    /// <summary>
    /// 初始化 <see cref="FrontedBehaviorEventDebuggerWindow" /> 的新实例。
    /// </summary>
    /// <param name="viewModel">调试器窗口视图模型。</param>
    public FrontedBehaviorEventDebuggerWindow(FrontedBehaviorEventDebuggerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
