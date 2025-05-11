using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Printing;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class SettingPageViewModel : ObservableObject
    {
        public IUpdaterService UpdaterService { get; }

        public SettingPageViewModel(IUpdaterService updaterService)
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
            AppVersion = "版本 v" + App.ResourceAssembly.GetName().Version!.ToString();
            UpdaterService = updaterService;
            UpdaterService.Downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
            UpdaterService.Downloader.DownloadFileCompleted += Downloader_DownloadFileCompleted;
            UpdaterService.Downloader.DownloadStarted += Downloader_DownloadStarted;
        }

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UpdateCheckCommand))]
        private bool _isDownloading = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
        private bool _isDownloadFinished = false;

        [ObservableProperty]
        private string _downloadProgressText = string.Empty;

        [ObservableProperty]
        private double _downloadProgress;

        [ObservableProperty]
        private string _mbPerSecondSpeed = string.Empty;

        public string Mirror { get; set; } = "https://ghproxy.net/";

        private void Downloader_DownloadStarted(object? sender, Downloader.DownloadStartedEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                IsDownloading = true;
            });
        }

        private void Downloader_DownloadFileCompleted(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (e.Error == null && !e.Cancelled)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    IsDownloadFinished = true;
                    IsDownloading = false;
                });
                return;
            }
            App.Current.Dispatcher.Invoke(() =>
            {
                IsDownloading = false;
            });
        }

        private void Downloader_DownloadProgressChanged(object? sender, Downloader.DownloadProgressChangedEventArgs e)
        {
            DownloadProgress = e.ProgressPercentage;
            DownloadProgressText = e.ProgressPercentage.ToString("0.00") + "%";
            MbPerSecondSpeed = (e.BytesPerSecondSpeed / 1024 / 1024).ToString("0.00") + " MB/s";
        }

        [RelayCommand(CanExecute = nameof(CanUpdateCheckExcute))]
        private async Task UpdateCheck()
        {
            await UpdaterService.UpdateCheck(false, Mirror);
        }

        private bool CanUpdateCheckExcute() => !IsDownloading;

        [RelayCommand(CanExecute = nameof(CanInstallExcute))]
        private void InstallUpdate()
        {
            UpdaterService.InstallUpdate();
        }

        private bool CanInstallExcute() => IsDownloadFinished;

        [RelayCommand]
        private void CancelDownload()
        {
            UpdaterService.Downloader.CancelAsync();
        }

        public ObservableCollection<string> MirrorList { get; } = [
            "https://ghproxy.net/",
            "https://gh.plfjy.top/",
            "https://ghfast.top/",
            ""
        ];
    }
}
