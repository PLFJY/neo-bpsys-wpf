using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.GameStopwatch.Services;
using neo_bpsys_wpf.GameStopwatch.ViewModels;
using neo_bpsys_wpf.GameStopwatch.Views;
using System.IO;

namespace neo_bpsys_wpf.GameStopwatch;

/// <summary>
/// 比赛秒表插件入口。
/// </summary>
public sealed class GameStopwatchEntry : PluginBase
{
    private const string SettingsFileName = "Settings.json";

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="services"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">插件配置目录未初始化。</exception>
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(PluginConfigFolder))
        {
            throw new InvalidOperationException("GameStopwatch plugin config folder is not initialized.");
        }

        var settingsPath = Path.GetFullPath(Path.Combine(PluginConfigFolder, SettingsFileName));
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

        services.AddSingleton<IGameStopwatchService>(_ => new GameStopwatchService(settingsPath));
        services.AddSingleton<GameStopwatchSettingsPageViewModel>();
        // GameStopwatchWindowViewModel 由 AddFrontedWindow 内部注册，此处无需重复注册（P2-6）。
        services.AddFrontedWindow<GameStopwatchWindow, GameStopwatchWindowViewModel>();
        services.AddBackendPage<GameStopwatchSettingsPage, GameStopwatchSettingsPageViewModel>();
    }
}
