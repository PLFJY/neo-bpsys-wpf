using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 提供 v3 前台窗口布局的内存空模板。
/// </summary>
/// <remarks>
/// 当激活包和内置资源中均找不到窗口布局时，使用此工厂创建合法的空模板，
/// 确保窗口可正常渲染和打开设计器。空模板不写入磁盘。
/// </remarks>
public class FrontedV3LayoutWindowConfigFactory
{
    /// <summary>
    /// 创建指定窗口的空布局配置。
    /// </summary>
    /// <param name="canonicalWindowId">窗口的 Canonical ID。</param>
    /// <returns>可正常渲染的空 <see cref="FrontedWindowConfig"/> 实例。</returns>
    public FrontedWindowConfig CreateEmptyConfig(string canonicalWindowId)
    {
        // FrontedWindowConfig 的默认构造函数已设置合理的默认值：
        // Version=3、WindowSettings (1440x810)、CanvasSettings (1440x810)、ControlLayout (空)。
        return new FrontedWindowConfig
        {
            Version = 3
        };
    }
}
