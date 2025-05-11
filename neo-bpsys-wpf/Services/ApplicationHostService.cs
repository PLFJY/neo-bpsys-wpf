using System.Windows;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using Wpf.Ui;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 应用程序宿主服务，负责管理主窗口的启动和导航初始化
    /// </summary>
    /// <param name="serviceProvider">用于获取导航窗口和其他服务的服务提供者</param>
    public class ApplicationHostService(IServiceProvider serviceProvider) : IHostedService
    {
        private INavigationWindow? _navigationWindow;

        /// <summary>
        /// 启动异步服务，触发应用程序激活处理
        /// </summary>
        /// <param name="cancellationToken">取消操作的通知令牌</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await HandleAvtivationAsync();
        }

        /// <summary>
        /// 停止异步服务，执行清理操作
        /// </summary>
        /// <param name="cancellationToken">取消操作的通知令牌</param>
        /// <returns>表示异步操作的任务</returns>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// 处理应用程序激活逻辑，初始化主窗口和导航
        /// </summary>
        /// <returns>表示异步操作的任务</returns>
        private async Task HandleAvtivationAsync()
        {
            await Task.CompletedTask;

            // 检查是否存在已存在的主窗口实例
            if (!Application.Current.Windows.OfType<MainWindow>().Any())
            {
                // 从服务提供者获取导航窗口实例
                _navigationWindow = (
                    serviceProvider.GetService(typeof(INavigationWindow)) as INavigationWindow
                )!;
                _navigationWindow!.ShowWindow();

                /// 预加载包含CharaSelector组件的页面以优化性能
                /// 通过延迟导航顺序来平衡资源加载压力
                _ = _navigationWindow.Navigate(typeof(PickPage));
                await Task.Delay(750);

                // 加载BanSurPage页面并预留资源缓冲时间
                _ = _navigationWindow.Navigate(typeof(BanSurPage));
                await Task.Delay(550);

                // 加载BanHunPage页面并预留资源缓冲时间
                _ = _navigationWindow.Navigate(typeof(BanHunPage));
                await Task.Delay(250);

                // 最终导航至首页
                _ = _navigationWindow.Navigate(typeof(HomePage));
            }
            await Task.CompletedTask;
        }
    }
}