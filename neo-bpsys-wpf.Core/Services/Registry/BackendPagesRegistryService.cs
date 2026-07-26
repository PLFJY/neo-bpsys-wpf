using neo_bpsys_wpf.Core.Attributes;

namespace neo_bpsys_wpf.Core.Services.Registry;

/// <summary>
/// 后台页面注册服务，维护所有已注册的后台页面信息列表。
/// </summary>
public static class BackendPagesRegistryService
{
    /// <summary>
    /// 已注册的后台页面信息列表。
    /// </summary>
    internal static List<BackendPageInfo> Registered { get; } = [];
}