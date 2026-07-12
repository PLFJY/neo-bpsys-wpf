using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 供插件注册设计器 v3 前台控件的注册表。
/// </summary>
public interface IFrontedControlPluginRegistry
{
    /// <summary>
    /// 注册插件前台控件描述符。
    /// </summary>
    void Register<TConfig>(FrontedPluginControlDescriptor<TConfig> descriptor)
        where TConfig : FrontedControlConfigBase;
}
