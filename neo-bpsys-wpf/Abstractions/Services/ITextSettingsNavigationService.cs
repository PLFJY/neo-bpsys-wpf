using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Abstractions.Services;

public interface ITextSettingsNavigationService
{
    /// <summary>
    /// 关闭设置
    /// </summary>
    /// <param name="windowType"></param>
    void Close(FrontWindowType windowType);
    /// <summary>
    /// 导航到设置
    /// </summary>
    /// <param name="type">窗口类型</param>
    /// <param name="page">页面实例</param>
    void Navigate(FrontWindowType type, object page);
    /// <summary>
    /// 设置Frame控件
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="frame">Frame控件</param>
    void SetFrameControl(FrontWindowType windowType, Frame frame);
}