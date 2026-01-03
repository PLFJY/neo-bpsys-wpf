using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Services.Registry;

namespace neo_bpsys_wpf.Core.Extensions.Registry;

public static class BackendPagesRegistryExtensions
{
    public static void AddBackendPage<TView, TViewModel>(this IServiceCollection services)
        where TView : Page, new() where TViewModel : class
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
        services.AddSingleton<TView>(sp => new TView
        {
            DataContext = sp.GetRequiredService<TViewModel>()
        }
        );
    }
}