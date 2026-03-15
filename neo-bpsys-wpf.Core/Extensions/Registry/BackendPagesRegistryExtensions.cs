using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Services.Registry;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

/// <summary>
/// 后台页面注册扩展
/// </summary>
public static class BackendPagesRegistryExtensions
{
    /// <summary>
    /// 注册后台页面
    /// </summary>
    /// <param name="services">服务容器</param>
    /// <typeparam name="TView">页面类型</typeparam>
    /// <typeparam name="TViewModel">页面视图模型类型</typeparam>
    /// <exception cref="ArgumentException">添加失败</exception>
    public static void AddBackendPage<TView, TViewModel>(this IServiceCollection services)
        where TView : Page where TViewModel : class
    {
        var type = typeof(TView);
        if (type.GetCustomAttributes(false).FirstOrDefault(x => x is BackendPageInfo) is not BackendPageInfo info)
        {
            throw new ArgumentException($"无法注册设置页面 {type.FullName}，因为设置页面没有注册信息。");
        }

        if (BackendPagesRegistryService.Registered.FirstOrDefault(x => x.Id == info.Id) != null)
        {
            throw new ArgumentException($"此设置页面id {info.Id} 已经被占用。");
        }

        info.PageType = type;
        BackendPagesRegistryService.Registered.Add(info);

        services.AddSingleton<TViewModel>();
        services.AddSingleton<TView>(sp =>
        {
            var view = ActivatorUtilities.CreateInstance<TView>(sp);
            view.DataContext = sp.GetRequiredService<TViewModel>();
            return view;
        });
    }
}
