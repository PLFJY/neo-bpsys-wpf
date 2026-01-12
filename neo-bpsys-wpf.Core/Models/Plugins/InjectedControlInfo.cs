using System.Windows;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 添加到前台窗口的控件信息
/// </summary>
/// <param name="Id">控件Id</param>
/// <param name="Control">控件</param>
/// <param name="TargetWindow">目标窗口</param>
/// <param name="DefaultInfo">默认信息</param>
public record InjectedControlInfo(
    string Id,
    FrameworkElement Control,
    string TargetWindow,
    string TargetCanvas,
    ElementInfo DefaultInfo);