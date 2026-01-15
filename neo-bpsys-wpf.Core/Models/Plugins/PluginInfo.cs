using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Services;
using System.IO;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models;

public class PluginInfo : ObservableObjectBase
{
    private bool _isRestartRequired;
    private bool _isUpdateAvailable;
    private long _downloadCount;
    private bool _isNewVersionInstalled;
    private string? _newVersion;
    private Exception? _exception;


    [JsonRequired] public required PluginManifest Manifest { get; init; }

    [JsonIgnore] public bool IsLocal { get; internal set; }

    [JsonIgnore]
    public bool IsEnabled
    {
        get
        {
            if (!IsLocal) return false;

            return PluginStatusService.IsPluginEnabled(Manifest.Id, IsBuiltIn);
        }
        set
        {
            if (!IsLocal) throw new InvalidOperationException("无法为不存在本地的插件设置启用状态。");

            IsRestartRequired = true;

            PluginStatusService.SetPluginEnabled(Manifest.Id, value);

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 插件是否将要卸载
    /// </summary>
    [JsonIgnore]
    public bool IsUninstalling
    {
        get
        {
            if (!IsLocal)
                return false;
            return File.Exists(Path.Combine(PluginFolderPath, ".uninstall"));
        }
        set
        {
            if (!IsLocal)
                throw new InvalidOperationException("无法为不存在本地的插件设置将要卸载状态。");
            var path = Path.Combine(PluginFolderPath, ".uninstall");
            if (value)
            {
                File.WriteAllText(path, "");
            }
            else
            {
                File.Delete(path);
                IsRestartRequired = false;
            }

            IsRestartRequired = true;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 插件文件路径。
    /// </summary>
    [JsonIgnore]
    public string PluginFolderPath { get; init; } = "";

    /// <summary>
    /// 图标真实路径
    /// </summary>
    public string RealIconPath { get; set; } = "";

    /// <summary>
    /// 插件加载时错误
    /// </summary>
    [JsonIgnore]
    public Exception? Exception
    {
        get => _exception;
        set => SetPropertyWithAction(ref _exception, value, _ => { OnPropertyChanged(nameof(ExceptionText)); });
    }

    [JsonIgnore]
    public string ExceptionText => IsEnabled ?
        (Exception != null ? Exception.ToString()
            : "Loaded")
        : "Disabled";

    /// <summary>
    /// 需要重启
    /// </summary>
    [JsonIgnore]
    public bool IsRestartRequired
    {
        get => _isRestartRequired;
        set => SetProperty(ref _isRestartRequired, value);
    }

    /// <summary>
    /// 插件是否有更新可用。
    /// </summary>
    [JsonIgnore]
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value);
    }

    /// <summary>
    /// 插件下载量
    /// </summary>
    public long DownloadCount
    {
        get => _downloadCount;
        set => SetProperty(ref _downloadCount, value);
    }

    /// <summary>
    /// 插件加载状态
    /// </summary>
    public PluginLoadStatus LoadStatus { get; set; }

    /// <summary>
    /// 是否是内置插件
    /// </summary>
    public bool IsBuiltIn { get; internal set; }

    /// <summary>
    /// 新的版本是否已经被安装 (放在.new处等待重启)
    /// </summary>
    [JsonIgnore]
    public bool IsNewVersionInstalled
    {
        get => _isNewVersionInstalled;
        set => SetProperty(ref _isNewVersionInstalled, value);
    }


    /// <summary>
    /// 在 .new 处的新版本
    /// </summary>
    [JsonIgnore]
    public string? NewVersion
    {
        get => _newVersion;
        set => SetProperty(ref _newVersion, value);
    }
}