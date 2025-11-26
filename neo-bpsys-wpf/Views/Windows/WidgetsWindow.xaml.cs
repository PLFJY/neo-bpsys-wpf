using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// WidgetsWindow.xaml 的交互逻辑
/// </summary>
public partial class WidgetsWindow : Window
{
    public WidgetsWindow()
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
        DragMove();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}