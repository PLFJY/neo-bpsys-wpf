using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using System.Windows;
using Wpf.Ui;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 应用程序启动服务，包含导航页面预加载
/// </summary>
/// <param name="serviceProvider"></param>
public class ApplicationHostService(IServiceProvider serviceProvider) : IHostedService
{
    private INavigationWindow? _navigationWindow;
    private ClassicBackendWindow? _classicBackendWindow;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await HandleActivationAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    private async Task HandleActivationAsync()
    {
        await Task.CompletedTask;

        var settingsHostService = serviceProvider.GetRequiredService<ISettingsHostService>();
        _ = serviceProvider.GetRequiredService<ProductTourRegistrationMarker>();
        if (settingsHostService.Settings.IsClassicMode)
        {
            if (!Application.Current.Windows.OfType<ClassicBackendWindow>().Any()
                && !Application.Current.Windows.OfType<MainWindow>().Any())
            {
                _classicBackendWindow = serviceProvider.GetRequiredService<ClassicBackendWindow>();
                _classicBackendWindow.Show();
            }

            return;
        }

        if (!Application.Current.Windows.OfType<MainWindow>().Any()
            && !Application.Current.Windows.OfType<ClassicBackendWindow>().Any())
        {
            _navigationWindow = serviceProvider.GetRequiredService<INavigationWindow>();
            _navigationWindow.ShowWindow();

            //提前加载调用了CharaSelector的页面，避免使用过程中卡顿
            //await Task.Delay(250);
            //_ = _navigationWindow.Navigate(typeof(PickPage));
            //await Task.Delay(750);
            //_ = _navigationWindow.Navigate(typeof(BanSurPage));
            //await Task.Delay(550);
            //_ = _navigationWindow.Navigate(typeof(BanHunPage));
            //await Task.Delay(250);

            _ = _navigationWindow.Navigate(typeof(HomePage));

            // CharacterSelector 页面在首次导航时会同步创建大量控件；启动遮罩消失后在后台空闲阶段预热，
            // 避免用户首次切换到 Pick/Ban 页面时出现明显卡顿。预加载不切换当前页面，不触发导航动画。
            _ = PreloadCharacterSelectorPagesAsync();
        }
        await Task.CompletedTask;
    }

    private async Task PreloadCharacterSelectorPagesAsync()
    {
        try
        {
            await Task.Delay(1500);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.HasShutdownStarted)
            {
                return;
            }

            foreach (var pageType in new[] { typeof(PickPage), typeof(BanSurPage), typeof(BanHunPage) })
            {
                if (dispatcher.HasShutdownStarted)
                {
                    return;
                }

                await dispatcher.InvokeAsync(
                    () =>
                    {
                        try
                        {
                            _ = serviceProvider.GetService(pageType);
                        }
                        catch (Exception ex)
                        {
                            serviceProvider
                                .GetService<Microsoft.Extensions.Logging.ILogger<ApplicationHostService>>()
                                ?.LogWarning(ex, "Failed to preload backend page {PageType}.", pageType.Name);
                        }
                    },
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
        catch (Exception)
        {
            // 预加载是纯优化，不得影响主窗口启动与导航。
        }
    }
}
