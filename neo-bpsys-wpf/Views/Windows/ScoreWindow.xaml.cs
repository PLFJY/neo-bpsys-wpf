using System.ComponentModel;
using System.Windows;

namespace neo_bpsys_wpf.Views.Windows
{
    /// <summary>
    /// ScoreWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ScoreWindow : Window
    {
        public ScoreWindow()
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
