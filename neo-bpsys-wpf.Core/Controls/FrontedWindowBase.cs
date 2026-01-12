using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;

namespace neo_bpsys_wpf.Core.Controls;

public abstract class FrontedWindowBase : Window
{
    public FrontedWindowBase() {
        WeakReferenceMessenger.Default.Register<DesignerModeChangedMessage>(this, OnDesignerModeChanged);
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void OnDesignerModeChanged(object recipient, DesignerModeChangedMessage message)
    {
        if (message.IsDesignerMode)
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
