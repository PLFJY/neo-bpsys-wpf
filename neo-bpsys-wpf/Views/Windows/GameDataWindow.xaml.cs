using System.ComponentModel;
using System.Windows;

namespace neo_bpsys_wpf.Views.Windows
{
    /// <summary>
    /// GameDataWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GameDataWindow : Window
    {
        public GameDataWindow()
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
