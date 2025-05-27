using Downloader;
using static neo_bpsys_wpf.Services.UpdaterService;

namespace neo_bpsys_wpf.Services
{
    public interface IUpdaterService
    {
        string NewVersion { get; set; }
        ReleaseInfo NewVersionInfo { get; set; }
        DownloadService Downloader { get; }
        bool IsFindPreRelease { get; set; }
        Task DownloadUpdate(string mirror = "");
        void InstallUpdate();
        Task<bool> UpdateCheck(bool isinitial = false, string mirror = "");
    }
}
