using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using neo_bpsys_wpf.Core.Attributes;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// ScoreSurWindow.xaml 的交互逻辑
/// </summary>
[FrontedWindowInfo("4ED64F79-E47C-490D-B86A-AE396F279889", "ScoreSurWindow", true)]
public partial class ScoreSurWindow : Window
{
    public ScoreSurWindow()
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