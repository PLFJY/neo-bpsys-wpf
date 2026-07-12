using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 声明可由设计器 v3 绑定目录扫描的显式根。
/// </summary>
public interface IFrontedBindingRootProvider
{
    /// <summary>
    /// 获取绑定根。实现不得读取运行时值。
    /// </summary>
    IReadOnlyList<FrontedBindingRootDescriptor> GetRoots();
}
