using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.Views.Windows
{
    /// <summary>
    /// BpWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BpWindow : Window
    {
        public BpWindow()
        {
            InitializeComponent();
            //Background = new ImageBrush(new BitmapImage(new Uri($"{Environment.CurrentDirectory}\\Resources\\bpui\\bp.png")));
        }

    }
}
