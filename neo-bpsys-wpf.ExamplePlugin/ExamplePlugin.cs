using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.ExamplePlugin.Models;
using neo_bpsys_wpf.ExamplePlugin.Services;
using neo_bpsys_wpf.ExamplePlugin.Views;
using System.IO;

namespace neo_bpsys_wpf.ExamplePlugin;

public class ExamplePlugin : PluginBase
{
    public PluginSettings Settings { get; set; } = new();

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        //注册后台页面
        services.AddBackendPage<MainPage, ViewModels.MainPageViewModel>();

        //注册前台窗口
        services.AddFrontedWindow<MainWindow, ViewModels.MainWindowViewModel>();

        //注册服务
        services.AddSingleton<IExampleService, ExampleService>();

        //注册注入控件
        ExampleInjectedControl injectedControl = new();
        FrontedWindowHelper.InjectControlToFrontedWindow("D9AFD731-DB3C-408B-8368-D70E688CE7CB",
            injectedControl, FrontedWindowType.BpWindow, "BaseCanvas",
            new ElementInfo(379, 100, 522, 312));

        // 加载配置文件
        Settings = ConfigureFileHelper.LoadConfig<PluginSettings>(Path.Combine(PluginConfigFolder, "Settings.json"));
        //监听属性变化
        Settings.PropertyChanged += (sender, args) =>
        {
            // 保存配置文件
            ConfigureFileHelper.SaveConfig<PluginSettings>(Path.Combine(PluginConfigFolder, "Settings.json"), Settings);
        };
    }
}