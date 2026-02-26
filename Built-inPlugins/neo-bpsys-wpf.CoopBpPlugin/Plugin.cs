using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.CoopBpPlugin.Models;
using neo_bpsys_wpf.CoopBpPlugin.Services;
using neo_bpsys_wpf.CoopBpPlugin.ViewModels;
using neo_bpsys_wpf.CoopBpPlugin.Views;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Core.Helpers;
using System.IO;

namespace neo_bpsys_wpf.CoopBpPlugin;

/// <summary>
/// 合作BP插件入口类
/// 提供多人联机BP功能，支持IPv4和IPv6连接
/// </summary>
public class Plugin : PluginBase
{
    /// <summary>插件设置</summary>
    private PluginSettings _settings = new();

    /// <summary>
    /// 初始化插件
    /// 注册服务和页面
    /// </summary>
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 加载设置
        LoadSettings();

        // 注册单例服务
        services.AddSingleton(_settings);

        // 注册网络服务
        services.AddSingleton<IServerService, ServerService>();
        services.AddSingleton<IClientService, ClientService>();
        services.AddSingleton<IDataSyncService, DataSyncService>();

        // 注册后端页面
        services.AddBackendPage<MainPage, MainPageViewModel>();

        // 将设置保存到上下文属性中，供其他组件访问
        context.Properties["CoopBpPlugin.Settings"] = _settings;
    }

    /// <summary>
    /// 加载插件设置
    /// 从配置文件读取设置，如果文件不存在则使用默认值
    /// </summary>
    private void LoadSettings()
    {
        var settingsPath = Path.Combine(PluginConfigFolder, "Settings.json");
        if (File.Exists(settingsPath))
        {
            try
            {
                _settings = ConfigureFileHelper.LoadConfig<PluginSettings>(settingsPath);
            }
            catch
            {
                _settings = new PluginSettings();
            }
        }
    }

    /// <summary>
    /// 保存插件设置
    /// 将当前设置写入配置文件
    /// </summary>
    public void SaveSettings()
    {
        var settingsPath = Path.Combine(PluginConfigFolder, "Settings.json");
        ConfigureFileHelper.SaveConfig(settingsPath, _settings);
    }
}
