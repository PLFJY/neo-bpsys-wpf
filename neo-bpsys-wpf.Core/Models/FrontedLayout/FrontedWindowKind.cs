namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// Describes how a fronted window is provided.
/// </summary>
public enum FrontedWindowKind
{
    /// <summary>
    /// Built-in WPF fronted window shipped by the host app, with layouts stored by built-in window type name.
    /// </summary>
    BuiltIn,

    /// <summary>
    /// Plugin-owned WPF XAML window. It is launched by the host but is not Designer-editable by default.
    /// </summary>
    PluginXaml,

    /// <summary>
    /// Plugin fronted window rendered by the host v3 layout renderer and editable when its canvases are customizable.
    /// </summary>
    PluginLayout
}
