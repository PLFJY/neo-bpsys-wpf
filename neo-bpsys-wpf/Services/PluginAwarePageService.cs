using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Plugins.Services;
using neo_bpsys_wpf.Core.Plugins.UI;
using System;
using Wpf.Ui.Abstractions;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 支持插件页面的 PageService 实现
/// </summary>
public class PluginAwarePageService : INavigationViewPageProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUIExtensionService _uiExtensionService;

    public PluginAwarePageService(IServiceProvider serviceProvider, IUIExtensionService uiExtensionService)
    {
        _serviceProvider = serviceProvider;
        _uiExtensionService = uiExtensionService;
    }

    public object? GetPage(Type pageType)
    {
        if (pageType == null)
        {
            throw new ArgumentNullException(nameof(pageType));
        }

        // 先检查是否是插件页面
        foreach (var extension in _uiExtensionService.GetExtensions<INavigationPageExtension>())
        {
            if (extension.PageType == pageType)
            {
                // 使用插件扩展的工厂方法创建页面实例
                return extension.CreatePageInstance();
            }
        }

        // 如果不是插件页面，使用标准的 DI 解析
        return _serviceProvider.GetService(pageType);
    }

    public T? GetPage<T>() where T : class
    {
        return GetPage(typeof(T)) as T;
    }
}
