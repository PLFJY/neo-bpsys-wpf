using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 向设计器 v3 绑定目录添加虚拟或插件提供的节点。
/// </summary>
public interface IFrontedBindingCatalogContributor
{
    /// <summary>
    /// 构建额外的绑定节点，不读取运行时共享数据值。
    /// </summary>
    IReadOnlyList<FrontedBindingTreeNode> BuildNodes();
}
