using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.FocusKeeper.ViewModels;
using neo_bpsys_wpf.FocusKeeper.Views;

namespace neo_bpsys_wpf.FocusKeeper;

/// <summary>
/// 焦点保持插件入口。注册服务与后台管理页。
/// </summary>
public sealed class FocusKeeperEntry : PluginBase
{
    /// <inheritdoc />
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var pluginDir = Path.GetDirectoryName(GetType().Assembly.Location)!;
        // 通过 DI 工厂注入 IElevationService，使 FocusKeeperService 能检测主程序权限级别
        services.AddSingleton<IFocusKeeperService>(sp => new FocusKeeperService(
            pluginDir,
            sp.GetRequiredService<IElevationService>()));
        services.AddBackendPage<FocusKeeperPage, FocusKeeperPageViewModel>();

        // 注册应用停止回调：确保主程序退出时一定卸载钩子，
        // 即使 Host.Dispose() 因异常未执行，ApplicationStopping 也会先触发。
        // 这保证目标进程中的 subclass 和 IAT hook 被正确清理，避免目标进程崩溃。
        services.AddSingleton<IHostedService>(sp => new FocusKeeperShutdownHostedService(
            sp.GetRequiredService<IFocusKeeperService>(),
            sp.GetRequiredService<IHostApplicationLifetime>(),
            sp.GetService<ILogger<FocusKeeperShutdownHostedService>>()));
    }
}
