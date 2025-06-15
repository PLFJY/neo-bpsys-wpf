using Downloader;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Xml.Serialization;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 更新服务
    /// </summary>
    public class UpdaterService : IUpdaterService
    {
        public string NewVersion { get; set; } = string.Empty;
        public ReleaseInfo NewVersionInfo { get; set; } = new();
        public bool IsFindPreRelease { get; set; } = false;
        public DownloadService Downloader { get; }

        private const string owner = "plfjy";
        private const string repo = "neo-bpsys-wpf";
        private const string GitHubApiBaseUrl = "https://api.github.com";
        private readonly HttpClient _httpClient;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IInfoBarService _infoBarService;

        public UpdaterService(IMessageBoxService messageBoxService, IInfoBarService infoBarService)
        {
            _httpClient = new()
            {
                BaseAddress = new Uri(GitHubApiBaseUrl)
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "neo-bpsys-wpf");
            _messageBoxService = messageBoxService;
            _infoBarService = infoBarService;
            var downloadOpt = new DownloadConfiguration()
            {
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 5,
                ParallelCount = 6,
            };

            Downloader = new DownloadService(downloadOpt);
            Downloader.DownloadFileCompleted += OnDownloadFileCompletedAsync;

            var fileName = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf_Installer.exe");
            if (File.Exists(fileName))
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (Exception ex)
                {
                    _messageBoxService.ShowErrorAsync(ex.Message, "清理更新残留异常");
                }
            }
        }

        /// <summary>
        /// 下载更新
        /// </summary>
        public async Task DownloadUpdate(string mirror = "")
        {
            var fileName = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf_Installer.exe");
            var downloadUrl = NewVersionInfo.assets.Where(a => a.name == "neo-bpsys-wpf_Installer.exe").ToArray()[0].browser_download_url;
            try
            {
                await Downloader.DownloadFileTaskAsync(mirror + downloadUrl, fileName);
            }
            catch (Exception ex)
            {
                await _messageBoxService.ShowErrorAsync($"下载失败: {ex.Message}");
            }
        }

        private async void OnDownloadFileCompletedAsync(object? sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await _messageBoxService.ShowErrorAsync($"下载失败：{e.Error.Message}");
                });
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (await _messageBoxService.ShowConfirmAsync("下载提示", "下载完成", "安装"))
                {
                    InstallUpdate();
                }
            });
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        /// <returns>如果有新版本则返回true，反之为false</returns>
        public async Task<bool> UpdateCheck(bool isinitial = false, string mirror = "")
        {
            await GetNewVersionInfoAsync();
            if (string.IsNullOrEmpty(NewVersionInfo.tag_name))
            {
                await _messageBoxService.ShowErrorAsync("获取更新错误");
                return false;
            }
            if (NewVersionInfo.tag_name != "v" + App.ResourceAssembly.GetName().Version!.ToString())
            {
                if (!isinitial)
                {
                    var result = await _messageBoxService.ShowConfirmAsync("更新检查", $"检测到新版本{NewVersionInfo.tag_name}，是否更新？", "更新");
                    if (result)
                        await DownloadUpdate(mirror);
                }
                else
                {
                    _infoBarService.ShowSuccessInfoBar($"检测到新版本{NewVersionInfo.tag_name}，前往设置页进行更新");
                }
                return true;
            }
            if (!isinitial)
            {
                await _messageBoxService.ShowInfoAsync("当前已是最新版本", "更新检查"); 
            }
            return false;
        }

        /// <summary>
        /// 获取新版本信息
        /// </summary>
        /// <returns></returns>
        private async Task GetNewVersionInfoAsync()
        {
            NewVersionInfo = new();
            try
            {
                var response = await _httpClient.GetAsync($"repos/{owner}/{repo}/releases{(IsFindPreRelease ? string.Empty : "/latest")}");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(content)) return;
                if (!IsFindPreRelease)
                {
                    var releaseInfo = JsonSerializer.Deserialize<ReleaseInfo>(content);
                    if (releaseInfo != null)
                    {
                        NewVersionInfo = releaseInfo;
                    }
                }
                else
                {
                    var releaseInfoArray = JsonSerializer.Deserialize<ReleaseInfo[]>(content);
                    if (releaseInfoArray != null && releaseInfoArray.Length > 0)
                    {
                        NewVersionInfo = releaseInfoArray[0];
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"HTTP请求错误: {ex.Message}");
                await _messageBoxService.ShowErrorAsync($"HTTP请求错误: {ex.Message}");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON解析错误: {ex.Message}");
                await _messageBoxService.ShowErrorAsync($"JSON解析错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生未知错误: {ex.Message}");
                await _messageBoxService.ShowErrorAsync($"发生未知错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 安装更新
        /// </summary>
        public void InstallUpdate()
        {
            var fileName = Path.Combine(
                Path.GetTempPath(),
                NewVersionInfo.assets.Where(a => a.name == "neo-bpsys-wpf_Installer.exe").ToArray()[0].name
                );
            Process p = new();
            p.StartInfo.FileName = fileName;
            p.StartInfo.Arguments = "/silent";
            p.Start();
            App.Current.Shutdown();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:命名样式", Justification = "<挂起>")]
        public class ReleaseInfo
        {
            public string tag_name { get; set; } = string.Empty;
            public string body { get; set; } = string.Empty;
            public AssetsInfo[] assets { get; set; } = [];
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:命名样式", Justification = "<挂起>")]
        public class AssetsInfo
        {
            public string name { get; set; } = string.Empty;
            public string browser_download_url { get; set; } = string.Empty;
        }
    }
}
