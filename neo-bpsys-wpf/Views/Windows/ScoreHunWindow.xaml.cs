using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Messages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// ScoreHunWindow.xaml 的交互逻辑
/// </summary>
public partial class ScoreHunWindow : Window
{
    public ScoreHunWindow()
    {
        InitializeComponent();
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