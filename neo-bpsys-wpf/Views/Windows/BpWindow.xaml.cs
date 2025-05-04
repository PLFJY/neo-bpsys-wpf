using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ViewModels.Pages;

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
            BaseCanvas.Background = ImageHelper.GetUiImageBrush("bp");
            FrontManagePageViewModel.DesignModeChanged += OnDesignModeChanged;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        private void OnDesignModeChanged(object? sender, DesignModeChangedEventArgs e)
        {
            if (!e.IsDesignMode)
            {
                MouseLeftButtonDown += OnMouseLeftButtonDown;
            }
            else
            {
                MouseLeftButtonDown -= OnMouseLeftButtonDown;
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            base.OnClosing(e);
        }
    }
}
