using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Attributes;
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
    /// <exception cref="ArgumentException">窗口类型未注册 <see cref="FrontedWindowInfo"/> 特性，或窗口 ID 已被占用</exception>
    public static void AddFrontedWindow<TView, TViewModel>(this IServiceCollection services)
    where TView : Window where TViewModel : ViewModelBase
    {
        var type = typeof(TView);
        if (type.GetCustomAttributes(false).FirstOrDefault(x => x is FrontedWindowInfo) is not FrontedWindowInfo info)
        {
            throw new ArgumentException($"无法注册前台窗口 {type.FullName}，因为前台窗口没有注册信息。");
        }

        if (FrontedWindowRegistryService.RegisteredWindow.FirstOrDefault(x => x.Id == info.Id) != null)
        {
            throw new ArgumentException($"此前台窗口id {info.Id} 已经被占用。");
        }

        info.WindowType = type;

        FrontedWindowRegistryService.RegisteredWindow.Add(info);

        services.AddSingleton<TViewModel>();
        services.AddSingleton<TView>(sp =>
        {
            var view = ActivatorUtilities.CreateInstance<TView>(sp);
            view.DataContext = sp.GetRequiredService<TViewModel>();
            return view;
        });
    }
}
