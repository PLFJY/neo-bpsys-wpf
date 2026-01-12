using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Messages;

namespace neo_bpsys_wpf.ExamplePlugin.ViewModels;

public partial class MainWindowViewModel : ViewModelBase,
    IRecipient<DesignerModeChangedMessage> // 接收设计者模式变更的消息
{
    [ObservableProperty] private bool _isDesignerMode;

    public void Receive(DesignerModeChangedMessage message)
    {
        // 判断设计者模式开启的窗口是否是当前庄口，并且消息中的设计者模式状态是否与当前状态不同
        if (message.FrontedWindowId == "3363BFE1-1393-4765-B926-001B6848FAF7" && message.IsDesignerMode != IsDesignerMode)
        {
            // 更新设计者模式状态
            IsDesignerMode = message.IsDesignerMode;
        }
    }
}