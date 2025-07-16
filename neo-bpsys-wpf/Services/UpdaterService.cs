using Downloader;
using neo_bpsys_wpf.Abstractions.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
// 添加必要的日志命名空间
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 更新服务
    /// </summary>
    public class UpdaterService : IUpdaterService
    {
        // 添加日志记录器字段
        private readonly ILogger<UpdaterService> _logger;

        public string NewVersion { get; set; } = string.Empty;
        public ReleaseInfo NewVersionInfo { get; set; } = new();
        public bool IsFindPreRelease { get; set; } = false;
        public DownloadService Downloader { get; }

        private const string Owner = "plfjy";
        private const string Repo = "neo-bpsys-wpf";
        private const string GitHubApiBaseUrl = "https://api.github.com";
        private const string InstallerFileName = "neo-bpsys-wpf_Installer.exe";
        private readonly HttpClient _httpClient;
        private readonly IMessageBoxService _messageBoxService;
        private readonly IInfoBarService _infoBarService;

        // 在构造函数中添加日志参数
        public UpdaterService(IMessageBoxService messageBoxService,
                             IInfoBarService infoBarService,
                             ILogger<UpdaterService> logger)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(GitHubApiBaseUrl)
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "neo-bpsys-wpf");
            _messageBoxService = messageBoxService;
            _infoBarService = infoBarService;
            // 注入日志记录器
            _logger = logger;

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
            if (!File.Exists(fileName))
            {
                _logger.LogInformation("No previous installer file found");
                return;
            }
            try
            {
                File.Delete(fileName);
                _logger.LogDebug("Previous installer file deleted: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete previous installer: {FileName}", fileName);
                _messageBoxService.ShowErrorAsync(ex.Message, "清理更新残留异常");
            }
        }

        /// <summary>
        /// 下载更新
        /// </summary>
        public async Task DownloadUpdate(string mirror = "")
        {
            _logger.LogInformation("DownloadUpdate started. Mirror: {Mirror}", mirror);
            var fileName = Path.Combine(Path.GetTempPath(), InstallerFileName);
            var downloadUrl = NewVersionInfo.Assets.First(a => a.Name == InstallerFileName).BrowserDownloadUrl;
            _logger.LogDebug("Target download URL: {DownloadUrl}", mirror + downloadUrl);

            try
            {
                await Downloader.DownloadFileTaskAsync(mirror + downloadUrl, fileName);
                _logger.LogInformation("Download initiated successfully to: {FileName}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Download failed for URL: {Url}", mirror + downloadUrl);
                await _messageBoxService.ShowErrorAsync($"下载失败: {ex.Message}");
            }
        }

        private async void OnDownloadFileCompletedAsync(object? sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                _logger.LogError(e.Error, "Download completed with error");
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await _messageBoxService.ShowErrorAsync($"下载失败：{e.Error.Message}");
                });
                return;
            }

            _logger.LogInformation("Download completed successfully");
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                _logger.LogDebug("Prompting user to install update");
                if (await _messageBoxService.ShowConfirmAsync("下载提示", "下载完成", "安装"))
                {
                    _logger.LogInformation("User chose to install update");
                    InstallUpdate();
                }
                else
                {
                    _logger.LogInformation("User declined installation");
                }
            });
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        /// <returns>如果有新版本则返回true，反之为false</returns>
        public async Task<bool> UpdateCheck(bool isInitial = false, string mirror = "")
        {
            _logger.LogInformation("UpdateCheck started. IsInitial: {IsInitial}", isInitial);
            await GetNewVersionInfoAsync();

            if (string.IsNullOrEmpty(NewVersionInfo.TagName))
            {
                _logger.LogWarning("Update information request failed. Empty tag name");
                await _messageBoxService.ShowErrorAsync("获取更新错误");
                return false;
            }

            if (NewVersionInfo.TagName != "v" + Application.ResourceAssembly.GetName().Version!.ToString())
            {
                _logger.LogInformation("New version detected: {NewVersion}", NewVersionInfo.TagName);
                if (!isInitial)
                {
                    var result = await _messageBoxService.ShowConfirmAsync("更新检查", $"检测到新版本{NewVersionInfo.TagName}，是否更新？", "更新");
                    if (result)
                    {
                        _logger.LogInformation("User accepted update for version: {Version}", NewVersionInfo.TagName);
                        await DownloadUpdate(mirror);
                    }
                    else
                    {
                        _logger.LogInformation("User declined update for version: {Version}", NewVersionInfo.TagName);
                    }
                }
                else
                {
                    _logger.LogInformation("Notifying user about new version in background: {Version}", NewVersionInfo.TagName);
                    _infoBarService.ShowSuccessInfoBar($"检测到新版本{NewVersionInfo.TagName}，前往设置页进行更新");
                }
                return true;
            }

            if (!isInitial)
            {
                _logger.LogInformation("Application is already up-to-date");
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
            _logger.LogInformation("Fetching new version information. PreReleaseEnabled: {IsFindPreRelease}", IsFindPreRelease);
            NewVersionInfo = new ReleaseInfo();

            try
            {
                var url = $"repos/{Owner}/{Repo}/releases{(IsFindPreRelease ? string.Empty : "/latest")}";
                _logger.LogDebug("API request URL: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(content))
                {
                    _logger.LogWarning("Received empty response from GitHub API");
                    return;
                }

                if (!IsFindPreRelease)
                {
                    var releaseInfo = JsonSerializer.Deserialize<ReleaseInfo>(content);
                    if (releaseInfo != null)
                    {
                        NewVersionInfo = releaseInfo;
                        _logger.LogInformation("Latest stable release found: {Version}", releaseInfo.TagName);
                    }
                }
                else
                {
                    var releaseInfoArray = JsonSerializer.Deserialize<ReleaseInfo[]>(content);
                    if (releaseInfoArray != null && releaseInfoArray.Length > 0)
                    {
                        NewVersionInfo = releaseInfoArray[0];
                        _logger.LogInformation("Newest pre-release found: {Version}", releaseInfoArray[0].TagName);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed to GitHub API");
                Debug.WriteLine($"HTTP请求错误: {ex.Message}");
                await _messageBoxService.ShowErrorAsync($"HTTP请求错误: {ex.Message}");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error for GitHub response");
                Console.WriteLine($"JSON解析错误: {ex.Message}");
                await _messageBoxService.ShowErrorAsync($"JSON解析错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unexpected error during version check");
                Console.WriteLine($"发生未知错误: {ex.Message}");
                await _messageBoxService.ShowErrorAsync($"发生未知错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 安装更新
        /// </summary>
        public void InstallUpdate()
        {
            _logger.LogInformation("Starting update installation process");
            var fileName = Path.Combine(
                Path.GetTempPath(),
                NewVersionInfo.Assets.First(a => a.Name == InstallerFileName).Name
                );

            _logger.LogDebug("Launching installer: {FilePath}", fileName);
            Process p = new();
            p.StartInfo.FileName = fileName;
            p.StartInfo.Arguments = "/silent";
            p.Start();
            _logger.LogInformation("Installer started, application shutting down");
            Application.Current.Shutdown();
        }

        public class ReleaseInfo
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; init; } = string.Empty;
            [JsonPropertyName("body")]
            public string Body { get; init; } = string.Empty;
            [JsonPropertyName("assets")]
            public AssetsInfo[] Assets { get; init; } = [];
        }

        public class AssetsInfo
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = string.Empty;
        }
    }
}