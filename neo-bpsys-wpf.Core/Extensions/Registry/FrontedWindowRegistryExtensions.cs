using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.Registry;
using System.Windows;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

/// <summary>
/// 前台窗口注册扩展方法
/// </summary>
public static class FrontedWindowRegistryExtensions
{
    /// <summary>
    /// 注册一个前台窗口到 DI 容器中。
    /// </summary>
    /// <typeparam name="TView">前台窗口类型，必须继承 <see cref="Window"/></typeparam>
    /// <typeparam name="TViewModel">前台窗口的视图模型类型，必须继承 <see cref="ViewModelBase"/></typeparam>
    /// <param name="services">服务容器</param>
    /// <exception cref="ArgumentException">窗口类型未注册 <see cref="FrontedWindowInfo"/> 特性，
    /// 或窗口 ID 未通过 <see cref="FrontedWindowIdentity.EnsureValidWindowLocalId"/>（空/空白、前后空白、路径分隔符、冒号或控制字符），
    /// 或当前插件包 ID 不是安全的 canonical path segment（含路径分隔符、<c>..</c> 等）时抛出。
    /// XAML 窗口 ID 不再要求为 GUID，只需非空且不与已注册窗口重复；重复检测由 <see cref="IFrontedWindowRegistry"/> 在构建 Canonical ID 索引时执行。</exception>
    public static void AddFrontedWindow<TView, TViewModel>(this IServiceCollection services)
    where TView : Window where TViewModel : ViewModelBase
    {
        var type = typeof(TView);
        if (type.GetCustomAttributes(false).FirstOrDefault(x => x is FrontedWindowInfo) is not FrontedWindowInfo info)
        {
            throw new ArgumentException($"无法注册前台窗口 {type.FullName}，因为前台窗口没有注册信息。");
        }

        FrontedWindowIdentity.EnsureValidWindowLocalId(info.Id);

        info.WindowType = type;

        var packageId = FrontedPluginRegistrationContext.CurrentPackageId;
        var isBuiltIn = info.IsBuiltIn;

        // 当 PackageId 非空（即插件作用域内）时，校验其可作为 canonical path segment，
        // 避免路径分隔符、.. 等字符在 LayoutService 拼接路径时才报错。
        EnsureSafePackageId(packageId);

        var canonicalId = FrontedWindowIdentity.BuildCanonicalId(info.Id, packageId, isBuiltIn);

        services.AddSingleton<TViewModel>();
        services.AddSingleton<TView>(sp =>
        {
            var view = ActivatorUtilities.CreateInstance<TView>(sp);
            view.DataContext = sp.GetRequiredService<TViewModel>();
            return view;
        });

        services.AddSingleton<FrontedWindowRegistration>(new FrontedXamlWindowRegistration
        {
            Id = canonicalId,
            LocalId = info.Id,
            PackageId = packageId,
            IsBuiltIn = isBuiltIn,
            DisplayName = info.Name,
            WindowType = type
        });
    }

    /// <summary>
    /// 校验插件包 ID 可安全作为 canonical path segment。非插件作用域（<c>null</c>）直接跳过。
    /// </summary>
    /// <param name="packageId">当前插件包 ID；非插件宿主直接注册时为 <see langword="null"/>。</param>
    /// <exception cref="ArgumentException">当 <paramref name="packageId"/> 非空但不是安全的 canonical path segment 时抛出。</exception>
    internal static void EnsureSafePackageId(string? packageId)
    {
        if (packageId is null)
        {
            return;
        }

        if (!FrontedV3LayoutWindowPathHelper.IsSafePathSegment(packageId))
        {
            throw new ArgumentException(
                $"Plugin package id '{packageId}' is not a safe canonical path segment. " +
                "It must not be null/whitespace, must not contain path separators ('/', '\\'), " +
                "'..', ':' or any character outside [A-Za-z0-9._-].",
                nameof(packageId));
        }
    }
}
