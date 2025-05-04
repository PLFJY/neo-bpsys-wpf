using System.ComponentModel;
using System.Windows;

namespace neo_bpsys_wpf.Views.Windows
{
    /// <summary>
    /// WidgetsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class WidgetsWindow : Window
    {
        public WidgetsWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            base.OnClosing(e);
        }
    }
}
