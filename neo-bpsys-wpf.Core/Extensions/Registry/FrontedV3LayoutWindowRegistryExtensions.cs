using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.Registry;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

/// <summary>
/// v3 Layout 前台窗口注册扩展方法。
/// </summary>
/// <remarks>
/// 该扩展在插件初始化作用域（<see cref="FrontedPluginRegistrationContext"/>）内被调用时，
/// 会自动读取当前插件包 ID 并据此生成 Canonical ID。
/// </remarks>
public static class FrontedV3LayoutWindowRegistryExtensions
{
    /// <summary>
    /// 注册一个 v3 Layout host 前台窗口到 DI 容器中。
    /// </summary>
    /// <param name="services">服务容器。</param>
    /// <param name="windowId">提供方内部的局部窗口标识，必须通过
    /// <see cref="FrontedV3LayoutWindowIdValidator"/> 验证。</param>
    /// <param name="isBuiltIn">是否为宿主内置窗口。内置窗口不要求 PackageId，
    /// Canonical ID 直接使用 <paramref name="windowId"/>，并从内置元数据表填充分组、排序与本地化显示名。</param>
    /// <exception cref="ArgumentException">当 <paramref name="windowId"/> 不是合法的局部窗口标识时抛出。</exception>
    /// <remarks>
    /// <para>
    /// 当在插件初始化作用域内调用时，<see cref="FrontedV3LayoutWindowRegistration.PackageId"/>
    /// 取自 <see cref="FrontedPluginRegistrationContext.CurrentPackageId"/>，Canonical ID 为
    /// <c>plugin:{PackageId}/{windowId}</c>。
    /// </para>
    /// <para>
    /// 当 <paramref name="isBuiltIn"/> 为 <see langword="true"/> 或当前不在任何插件作用域内
    /// （PackageId 为 <see langword="null"/>）时，按"非插件宿主直接注册"语义处理，
    /// Canonical ID 为 <paramref name="windowId"/>，<see cref="FrontedV3LayoutWindowRegistration.PackageId"/>
    /// 为 <see langword="null"/>。
    /// </para>
    /// <para>
    /// 内置窗口的分组、排序与本地化显示名由 <see cref="FrontedBuiltInWindowMetadata"/> 提供；
    /// 插件窗口的显示名默认回退到 <paramref name="windowId"/>。
    /// </para>
    /// </remarks>
    public static void AddFrontedV3LayoutWindow(
        this IServiceCollection services,
        string windowId,
        bool isBuiltIn = false)
    {
        FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(windowId);

        var packageId = FrontedPluginRegistrationContext.CurrentPackageId;
        var canonicalId = FrontedV3LayoutWindowIdentity.BuildCanonicalId(windowId, packageId, isBuiltIn);

        string? groupKey = null;
        int? displayOrder = null;
        IReadOnlyDictionary<Core.Enums.LanguageKey, string>? i18nDisplayNames = null;

        if (isBuiltIn
            && FrontedBuiltInWindowMetadata.TryGetMetadata(
                windowId,
                out var metaGroupKey,
                out var metaDisplayOrder,
                out var metaI18n))
        {
            groupKey = metaGroupKey;
            displayOrder = metaDisplayOrder;
            i18nDisplayNames = metaI18n;
        }

        var registration = new FrontedV3LayoutWindowRegistration
        {
            Id = canonicalId,
            LocalId = windowId,
            PackageId = packageId,
            IsBuiltIn = isBuiltIn,
            DisplayName = windowId,
            GroupKey = groupKey,
            DisplayOrder = displayOrder,
            I18nDisplayNames = i18nDisplayNames
        };

        services.AddSingleton<FrontedWindowRegistration>(registration);
    }
}
