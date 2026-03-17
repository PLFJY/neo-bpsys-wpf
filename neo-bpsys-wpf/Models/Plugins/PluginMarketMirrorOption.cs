namespace neo_bpsys_wpf.Models.Plugins;

/// <summary>
/// 表示插件市场和应用更新共用的下载镜像选项。
/// </summary>
public class PluginMarketMirrorOption
{
    /// <summary>
    /// 下拉框中显示的本地化 Key 或直接显示文本。
    /// </summary>
    public string DisplayNameKey { get; init; } = string.Empty;

    /// <summary>
    /// 镜像实际对应的地址值。
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
