using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ViewModels.Pages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

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
            BaseCanvas.Background = ImageHelper.GetUiImageBrush("interlude");
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
