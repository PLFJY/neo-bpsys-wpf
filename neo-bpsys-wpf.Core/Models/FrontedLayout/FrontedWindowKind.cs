namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 描述前台窗口的提供方式。
/// </summary>
public enum FrontedWindowKind
{
    /// <summary>
    /// 宿主应用自带的内置 WPF 前台窗口，布局按内置窗口类型名存储。
    /// </summary>
    BuiltIn,

    /// <summary>
    /// 插件拥有的 WPF XAML 窗口。由宿主启动，但默认不可在设计器中编辑。
    /// </summary>
    PluginXaml,

    /// <summary>
    /// 由宿主 v3 布局渲染器渲染的插件前台窗口，当其画布可定制时可编辑。
    /// </summary>
    PluginLayout
}
