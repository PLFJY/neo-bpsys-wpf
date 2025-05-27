using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.Helpers;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

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
            WeakReferenceMessenger.Default.Register<PropertyChangedMessage<bool>>(this, OnDesignModeChanged);
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            ScoreSurCanvas.Background = ImageHelper.GetUiImageBrush("scoreSur");
            ScoreHunCanvas.Background = ImageHelper.GetUiImageBrush("scoreHun");
            ScoreGlobalCanvas.Background = ImageHelper.GetUiImageBrush("scoreGlobal");
        }

        private void OnDesignModeChanged(object recipient, PropertyChangedMessage<bool> message)
        {
            if (message.PropertyName == "IsDesignMode")
            {
                if (!message.NewValue)
                {
                    MouseLeftButtonDown += OnMouseLeftButtonDown;
                }
                else
                {
                    MouseLeftButtonDown -= OnMouseLeftButtonDown;
                }
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
