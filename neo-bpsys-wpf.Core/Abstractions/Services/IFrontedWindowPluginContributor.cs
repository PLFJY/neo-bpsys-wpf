using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 由贡献前台窗口的插件实现。
/// 未显式指定窗口类型的描述符默认创建插件自己的 XAML 窗口；
/// v3 layout host 必须由描述符显式声明。
/// </summary>
public interface IFrontedWindowPluginContributor
{
    /// <summary>
    /// 返回插件前台窗口描述符。宿主在启动期间、注册表构建之前对这些描述符进行校验。
    /// </summary>
    IEnumerable<FrontedPluginWindowDescriptor> GetFrontedWindows();
}
