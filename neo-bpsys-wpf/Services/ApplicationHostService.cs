using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Abstractions.Services;
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
    public class ApplicationHostService : IHostedService
    {
        private INavigationWindow? _navigationWindow;
        private readonly ILogger<ApplicationHostService> _logger;
        private readonly IServiceProvider _serviceProvider;

        // 修复构造函数：添加base()初始化并正确注入logger
        public ApplicationHostService(IServiceProvider serviceProvider,
                                     ILogger<ApplicationHostService> logger) : base()
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _logger.LogInformation("ApplicationHostService initialized");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ApplicationHostService starting...");
            try
            {
                await HandleActivationAsync();
                _logger.LogInformation("ApplicationHostService started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting ApplicationHostService");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ApplicationHostService stopping...");
            await Task.CompletedTask;
            _logger.LogInformation("ApplicationHostService stopped");
        }

        private async Task HandleActivationAsync()
        {
            _logger.LogDebug("Handling application activation");
            await Task.CompletedTask;

            if (!Application.Current.Windows.OfType<MainWindow>().Any())
            {
                _logger.LogInformation("Creating main navigation window");
                _navigationWindow = _serviceProvider.GetRequiredService<INavigationWindow>();
                _navigationWindow.ShowWindow();

                // 记录页面预加载过程
                _logger.LogInformation("Preloading pages to avoid runtime lag");
                try
                {
                    await Task.Delay(250);
                    _logger.LogDebug("Navigating to PickPage");
                    _ = _navigationWindow.Navigate(typeof(PickPage));

                    await Task.Delay(750);
                    _logger.LogDebug("Navigating to BanSurPage");
                    _ = _navigationWindow.Navigate(typeof(BanSurPage));

                    await Task.Delay(550);
                    _logger.LogDebug("Navigating to BanHunPage");
                    _ = _navigationWindow.Navigate(typeof(BanHunPage));

                    await Task.Delay(250);
                    _logger.LogInformation("Returning to HomePage");
                    _ = _navigationWindow.Navigate(typeof(HomePage));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during page preloading");
                }
            }
            else
            {
                _logger.LogInformation("Main window already exists");
            }

            await Task.CompletedTask;
            _logger.LogDebug("Activation handling completed");
        }
    }
}