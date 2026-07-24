using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.GameStopwatch.Services;
using neo_bpsys_wpf.GameStopwatch.ViewModels;
using neo_bpsys_wpf.GameStopwatch.Views;

namespace neo_bpsys_wpf.GameStopwatch;

/// <summary>
/// 比赛秒表插件入口。
/// </summary>
public sealed class GameStopwatchEntry : PluginBase
{
    /// <inheritdoc />
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var service = new GameStopwatchService(System.IO.Path.Combine(PluginConfigFolder, "Settings.json"));
        services.AddSingleton<IGameStopwatchService>(service);
        services.AddSingleton<GameStopwatchSettingsPageViewModel>();
        // GameStopwatchWindowViewModel 由 AddFrontedWindow 内部注册，此处无需重复注册（P2-6）。
        services.AddFrontedWindow<GameStopwatchWindow, GameStopwatchWindowViewModel>();
        services.AddBackendPage<GameStopwatchSettingsPage, GameStopwatchSettingsPageViewModel>();
    }
}
