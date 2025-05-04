using System.ComponentModel;
using System.Windows;

namespace neo_bpsys_wpf.Views.Windows
{
    /// <summary>
    /// InterludeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class InterludeWindow : Window
    {
        public InterludeWindow()
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
