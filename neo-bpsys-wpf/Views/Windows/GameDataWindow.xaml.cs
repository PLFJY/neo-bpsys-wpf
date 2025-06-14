using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Messages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

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
            BaseCanvas.Background = ImageHelper.GetUiImageBrush("gameData");
            WeakReferenceMessenger.Default.Register<DesignModeChangedMessage>(this, OnDesignModeChanged);
            MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        private void OnDesignModeChanged(object recipient, DesignModeChangedMessage message)
        {
            if (message.IsDesignMode)
                MouseLeftButtonDown -= OnMouseLeftButtonDown;
            else
                MouseLeftButtonDown += OnMouseLeftButtonDown;
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
