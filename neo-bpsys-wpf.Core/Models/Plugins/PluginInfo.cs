using System.IO;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.Plugin;

public class PluginInfo : ObservableObject
{
    private bool _restartRequired = false;
    private bool _isUpdateAvailable = false;
    private long _downloadCount;

    [JsonRequired]
    public required PluginManifest Manifest { get; set; }

    [JsonIgnore] public bool IsLocal { get; set; } = false;

    [JsonIgnore]
    public bool IsEnabled
    {
        get
        {
            if (!IsLocal) return false;
            return !File.Exists(Path.Combine(PluginFolderPath, ".disabled"));;
        }
        set
        {
            if (!IsLocal) throw new InvalidOperationException("无法为不存在本地的插件设置启用状态。");
            
            var path = Path.Combine(PluginFolderPath, ".disabled");
            RestartRequired = true;
            if (value)
            {
                File.Delete(path);
            }
            else
            {
                File.WriteAllText(path, "");
            }
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
            }   
            OnPropertyChanged();
        }
    }
    
    /// <summary>
    /// 插件文件路径。
    /// </summary>
    [JsonIgnore]
    public string PluginFolderPath { get; set; } = "";

    /// <summary>
    /// 图标真实路径
    /// </summary>
    public string RealIconPath { get; set; } = "";

    /// <summary>
    /// 插件加载时错误
    /// </summary>
    [JsonIgnore]
    public Exception? Exception { get; set; }

    /// <summary>
    /// 需要重启
    /// </summary>
    [JsonIgnore]
    public bool RestartRequired
    {
        get => _restartRequired;
        set => SetProperty(ref _restartRequired, value);
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
    
    public bool IsBuiltIn { get; internal set; }
}