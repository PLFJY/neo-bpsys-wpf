using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 提供设计器 v3 绑定目录。
/// </summary>
public interface IFrontedBindingCatalogProvider
{
    /// <summary>
    /// 构建或返回缓存的绑定目录。
    /// </summary>
    IReadOnlyList<FrontedBindingTreeNode> BuildCatalog();
}
