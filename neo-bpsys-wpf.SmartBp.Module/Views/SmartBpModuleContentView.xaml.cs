using System.Windows.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// SmartBP module content view.
/// </summary>
public partial class SmartBpModuleContentView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmartBpModuleContentView"/> class.
    /// </summary>
    public SmartBpModuleContentView()
    {
        InitializeComponent();
    }

    private void AiDebugConsoleTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.ScrollToEnd();
    }
}
