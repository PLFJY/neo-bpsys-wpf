using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using System.Windows;
using Wpf.Ui;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 应用程序启动服务，包含导航页面预加载
    /// </summary>
    /// <param name="serviceProvider"></param>
    public class ApplicationHostService(IServiceProvider serviceProvider) : IHostedService
    {
        private INavigationWindow? _navigationWindow;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await HandleAvtivationAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        private async Task HandleAvtivationAsync()
        {
            await Task.CompletedTask;

            if (!Application.Current.Windows.OfType<MainWindow>().Any())
            {
                _navigationWindow = (
                    serviceProvider.GetService(typeof(INavigationWindow)) as INavigationWindow
                )!;
                _navigationWindow?.ShowWindow();

                //提前加载调用了CharaSelector的页面，避免使用过程中卡顿
                _ = _navigationWindow?.Navigate(typeof(PickPage));
                await Task.Delay(750);
                _ = _navigationWindow?.Navigate(typeof(BanSurPage));
                await Task.Delay(550);
                _ = _navigationWindow?.Navigate(typeof(BanHunPage));
                await Task.Delay(250);

                _ = _navigationWindow?.Navigate(typeof(HomePage));
            }
            await Task.CompletedTask;
        }
    }
}
