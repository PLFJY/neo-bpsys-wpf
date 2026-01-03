using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using neo_bpsys_wpf.Core.Attributes;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// WidgetsWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("712D2E21-B8DF-4220-8E3D-8AD0003DD079", "WidgetsWindow",
    ["MapBpCanvas", "BpOverViewCanvas", "MapV2Canvas"], true)]
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