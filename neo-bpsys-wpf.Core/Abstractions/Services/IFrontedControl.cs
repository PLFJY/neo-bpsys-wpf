using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台控件工厂。
/// </summary>
public interface IFrontedControl
{
    /// <summary>
    /// 支持的控件类型。
    /// </summary>
    string ControlType { get; }

    /// <summary>
    /// 支持的配置类型。
    /// </summary>
    Type ConfigType { get; }

    /// <summary>
    /// 创建前台控件。
    /// </summary>
    FrameworkElement Create(string name, FrontedControlConfigBase config, FrontedControlBuildContext context);
}
