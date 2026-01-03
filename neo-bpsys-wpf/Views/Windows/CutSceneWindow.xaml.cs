using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Messages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// CutSceneWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("8716A6DB-3DEC-4D45-966B-ECD202DCFB0C", "CutSceneWindow", true)]
public partial class CutSceneWindow : Window
{
    public CutSceneWindow()
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