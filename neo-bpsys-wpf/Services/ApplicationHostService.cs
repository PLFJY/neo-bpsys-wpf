using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using System.Windows;
using Wpf.Ui;

namespace neo_bpsys_wpf.Services
{
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
                _navigationWindow = (serviceProvider.GetService(typeof(INavigationWindow)) as INavigationWindow)!;
                _navigationWindow!.ShowWindow();

                _ = _navigationWindow.Navigate(typeof(HomePage));
            }
            await Task.CompletedTask;
        }
    }
}
