using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        // 在 UI 线程上执行窗口操作
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (!Application.Current.Windows.OfType<MainWindow>().Any())
            {
                _navigationWindow = serviceProvider.GetRequiredService<INavigationWindow>();
                _navigationWindow.ShowWindow();

                //提前加载调用了CharaSelector的页面，避免使用过程中卡顿
                Task.Delay(250).Wait();
                _ = _navigationWindow.Navigate(typeof(PickPage));
                Task.Delay(750).Wait();
                _ = _navigationWindow.Navigate(typeof(BanSurPage));
                Task.Delay(550).Wait();
                _ = _navigationWindow.Navigate(typeof(BanHunPage));
                Task.Delay(250).Wait();

                _ = _navigationWindow.Navigate(typeof(HomePage));
            }
        });

        await Task.CompletedTask;
    }
}