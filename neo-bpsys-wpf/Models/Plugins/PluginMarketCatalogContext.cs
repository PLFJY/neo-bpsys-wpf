namespace neo_bpsys_wpf.Models.Plugins;

/// <summary>标识一个可独立缓存的插件目录呈现上下文。</summary>
/// <param name="Source">实际插件索引地址。</param>
/// <param name="Culture">当前应用语言名称。</param>
/// <param name="Mirror">当前配置的镜像地址。</param>
public sealed record PluginMarketCatalogContext(string Source, string Culture, string Mirror);
