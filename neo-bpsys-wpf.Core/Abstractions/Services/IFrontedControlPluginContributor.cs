namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 在应用启动期间贡献设计器 v3 插件前台控件。
/// </summary>
public interface IFrontedControlPluginContributor
{
    /// <summary>
    /// 注册插件前台控件。
    /// </summary>
    void RegisterFrontedControls(IFrontedControlPluginRegistry registry);
}
