namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 设计器 v3 布局和 .bpui 清单使用的插件依赖元数据。
/// </summary>
public class FrontedPluginDependency
{
    /// <summary>
    /// 布局或包所要求的插件包 ID。
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// 安全渲染或编辑布局所需的最低插件包版本。
    /// </summary>
    public string? MinVersion { get; set; }

    /// <summary>
    /// 可选的显示名称，从已安装插件元数据或包清单复制而来。
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 可选的市场 ID，供安装/更新引导 UI 使用。
    /// </summary>
    public string? MarketplaceId { get; set; }

    /// <summary>
    /// 此依赖存在的原因，例如插件控件、插件窗口或两者兼有。
    /// </summary>
    public FrontedPluginDependencyReason Reason { get; set; } = FrontedPluginDependencyReason.Unknown;

    /// <summary>
    /// 依赖此插件的完整插件控件类型列表。
    /// </summary>
    public List<string> Controls { get; set; } = [];

    /// <summary>
    /// 依赖此插件的布局窗口列表，格式为 <c>{FullWindowType}</c>。
    /// 缺失的插件窗口布局在包中保留，但直到安装插件后才会加载。
    /// </summary>
    public List<string> RequiredBy { get; set; } = [];
}
