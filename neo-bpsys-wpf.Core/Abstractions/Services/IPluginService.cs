using System.Collections.ObjectModel;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IPluginService
{
    /// <summary>
    /// 已加载的插件信息列表。
    /// </summary>
    internal static ObservableCollection<PluginInfo> LoadedPluginsInternal { get; } = [];
    
    /// <summary>
    /// 已加载的插件ID列表。
    /// </summary>
    internal static ObservableCollection<string> LoadedPluginsIds { get; set; } = [];
    
    /// <summary>
    /// 已加载的插件信息列表。
    /// </summary>
    public static IReadOnlyList<PluginInfo> LoadedPlugins => LoadedPluginsInternal;
}