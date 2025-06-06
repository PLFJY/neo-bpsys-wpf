using Downloader;
using static neo_bpsys_wpf.Services.UpdaterService;

namespace neo_bpsys_wpf.Services
{
    public interface IUpdaterService
    {
        /// <summary>
        /// 最新版本号
        /// </summary>
        string NewVersion { get; set; }
        /// <summary>
        /// 最新版本信息
        /// </summary>
        ReleaseInfo NewVersionInfo { get; set; }
        /// <summary>
        /// 下载器
        /// </summary>
        DownloadService Downloader { get; }
        /// <summary>
        /// 是否找到预览版
        /// </summary>
        bool IsFindPreRelease { get; set; }
        /// <summary>
        /// 检查更新
        /// </summary>
        /// <param name="mirror">ghproxy镜像链接</param>
        /// <returns></returns>
        Task DownloadUpdate(string mirror = "");
        /// <summary>
        /// 安装更新
        /// </summary> 
        void InstallUpdate();
        /// <summary>
        /// 检查更新
        /// </summary>
        /// <returns>如果有新版本则返回true，反之为false</returns>
        Task<bool> UpdateCheck(bool isinitial = false, string mirror = "");
    }
}
