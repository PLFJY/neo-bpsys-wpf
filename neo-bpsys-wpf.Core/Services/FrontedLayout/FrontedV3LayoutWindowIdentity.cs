namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 生成 v3 前台窗口的 Canonical ID。
/// </summary>
/// <remarks>
/// 内置窗口或未关联插件包的窗口直接使用局部窗口标识；
/// 插件窗口使用 <c>plugin:{PackageId}/{LocalId}</c> 形式，与
/// <see cref="FrontedV3LayoutWindowPathHelper"/> 的 <c>CanonicalWindowId</c> 约定一致。
/// </remarks>
public static class FrontedV3LayoutWindowIdentity
{
    /// <summary>
    /// 根据局部窗口标识、插件包 ID 与是否内置，构造 Canonical ID。
    /// </summary>
    /// <param name="localWindowId">提供方内部的局部窗口标识。</param>
    /// <param name="packageId">插件包 ID；非插件时为 <see langword="null"/>。</param>
    /// <param name="isBuiltIn">是否为宿主内置窗口。</param>
    /// <returns>当 <paramref name="isBuiltIn"/> 为 <see langword="true"/> 或
    /// <paramref name="packageId"/> 为 <see langword="null"/> 时返回 <paramref name="localWindowId"/>；
    /// 否则返回 <c>plugin:{PackageId}/{LocalId}</c>。</returns>
    public static string BuildCanonicalId(string localWindowId, string? packageId, bool isBuiltIn)
    {
        if (isBuiltIn || packageId is null)
        {
            return localWindowId;
        }

        return $"plugin:{packageId}/{localWindowId}";
    }
}
