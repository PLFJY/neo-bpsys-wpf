using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using neo_bpsys_wpf.Core.Attributes;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// ScoreGlobalWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("3A4F66F7-BAC7-47AF-AC45-11657C50F7DD", "ScoreGlobalWindow", true)]
public partial class ScoreGlobalWindow : Window
{

    public ScoreGlobalWindow()
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