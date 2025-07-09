using System.ComponentModel;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows
{
    /// <summary>
    /// ScoreManualWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ScoreManualWindow : FluentWindow
    {
        public ScoreManualWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}
