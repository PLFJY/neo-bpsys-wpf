
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Core.Helpers;

namespace neo_bpsys_wpf.ExamplePlugin
{
    public class ExamplePlugin : PluginBase
    {
        public override void Initialize(HostBuilderContext context, IServiceCollection services)
        {
            services.AddBackendPage<MainPage, MainPageViewModel>();
            services.AddFrontedWindow<MainWindow, MainWindowViewModel>();
        }
    }


}
