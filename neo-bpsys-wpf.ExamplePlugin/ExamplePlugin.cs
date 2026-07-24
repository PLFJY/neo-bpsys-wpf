using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Core.Helpers;
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
        services.AddBackendPage<MainPage, ViewModels.MainPageViewModel>();

        services.AddFrontedWindow<ExampleXamlWindow, ViewModels.ExampleXamlWindowViewModel>();
        services.AddFrontedV3LayoutWindow("ExampleLayoutOverlay");

        services.AddFrontedPluginControlContributor<TeamCardFrontedControlContributor>();

        services.AddSingleton<IExampleService, ExampleService>();

        Settings = ConfigureFileHelper.LoadConfig<PluginSettings>(Path.Combine(PluginConfigFolder, "Settings.json"));
        Settings.PropertyChanged += (sender, args) =>
        {
            ConfigureFileHelper.SaveConfig<PluginSettings>(Path.Combine(PluginConfigFolder, "Settings.json"), Settings);
        };
    }
}
