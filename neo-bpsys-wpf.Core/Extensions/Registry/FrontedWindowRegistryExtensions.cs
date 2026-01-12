using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Services.Registry;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

public static class FrontedWindowRegistryExtensions
{
    public static void AddFrontedWindow<TView, TViewModel>(this IServiceCollection services)
    where TView : Window, new() where TViewModel : ViewModelBase
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
        services.AddSingleton<TView>(sp => new TView
            {
                DataContext = sp.GetRequiredService<TViewModel>()
            }
        );
    }
}