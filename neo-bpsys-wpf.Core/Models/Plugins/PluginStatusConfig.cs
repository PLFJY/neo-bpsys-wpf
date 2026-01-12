namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 插件状态配置模型
/// </summary>
public class PluginStatusConfig
{
    /// <summary>
    /// 插件启用状态配置，键为插件ID，值为是否启用
    /// </summary>
    public Dictionary<string, bool> PluginEnabledStatus { get; set; } = new();
}