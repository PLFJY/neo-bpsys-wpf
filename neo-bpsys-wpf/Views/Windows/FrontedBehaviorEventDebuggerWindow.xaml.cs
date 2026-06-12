using neo_bpsys_wpf.ViewModels.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Independent global behavior event debugger window.
/// </summary>
public partial class FrontedBehaviorEventDebuggerWindow : FluentWindow
{
    /// <summary>
    /// Initializes a new instance of <see cref="FrontedBehaviorEventDebuggerWindow" />.
    /// </summary>
    /// <param name="viewModel">Debugger window view model.</param>
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
