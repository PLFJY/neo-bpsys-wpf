using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using neo_bpsys_wpf.Core.Attributes;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// ScoreHunWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("EA69B342-DDA6-4394-BDFD-13368D76A6BA", "ScoreHunWindow", true)]
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
        DragMove();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}