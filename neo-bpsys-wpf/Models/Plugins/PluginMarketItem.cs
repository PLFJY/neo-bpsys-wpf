using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Models;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Models.Plugins;

/// <summary>
/// 表示插件市场中的一个插件条目，以及页面展示这个条目时需要的状态数据。
/// </summary>
public partial class PluginMarketItem : ObservableObjectBase
{
    /// <summary>
    /// 插件唯一标识。
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 插件显示名称。
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 插件简介。
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 插件版本号。
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 插件声明的插件 API 版本。
    /// </summary>
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = string.Empty;

    /// <summary>
    /// 插件作者名称。
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 插件图标原始地址。
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 插件 README 原始地址。
    /// </summary>
    [JsonPropertyName("readme")]
    public string Readme { get; set; } = string.Empty;

    /// <summary>
    /// 插件项目主页地址。
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 插件包下载地址。
    /// </summary>
    [JsonPropertyName("downloadURL")]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 插件压缩包的 SHA-256 校验值。
    /// 下载完成后会在解压前校验，用于阻止被篡改或损坏的插件包继续安装。
    /// </summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>
    /// 当前已安装的本地插件信息。
    /// </summary>
    [JsonIgnore]
    public PluginInfo? LocalPlugin { get; set; }

    /// <summary>
    /// 实际用于加载图标的地址。
    /// </summary>
    [ObservableProperty]
    private string _resolvedIconUrl = string.Empty;

    /// <summary>
    /// 实际用于加载 README 的地址。
    /// </summary>
    [ObservableProperty]
    private string _resolvedReadmeUrl = string.Empty;

    /// <summary>
    /// 实际用于下载插件包的地址。
    /// </summary>
    [ObservableProperty]
    private string _resolvedDownloadUrl = string.Empty;

    /// <summary>
    /// 已加载到页面中的 README Markdown 内容。
    /// </summary>
    [ObservableProperty]
    private string _readmeMarkdown = string.Empty;

    /// <summary>
    /// README 是否正在加载中。
    /// </summary>
    [ObservableProperty]
    private bool _isReadmeLoading;

    /// <summary>
    /// 当前插件是否已安装。
    /// </summary>
    [ObservableProperty]
    private bool _isInstalled;

    /// <summary>
    /// 当前插件是否已经安装到本地，但需要重启后才能应用更改。
    /// </summary>
    [ObservableProperty]
    private bool _isRestartRequired;

    /// <summary>
    /// 当前插件是否有可更新版本。
    /// </summary>
    [ObservableProperty]
    private bool _hasUpdateAvailable;

    /// <summary>
    /// 当前插件是否与宿主支持的插件 API 兼容。
    /// </summary>
    [ObservableProperty]
    private bool _isApiCompatible = true;

    /// <summary>
    /// 宿主版本是否低于当前插件要求。
    /// </summary>
    [ObservableProperty]
    private bool _isHostVersionTooLow;

    /// <summary>
    /// 插件 API 版本是否过低或格式无效。
    /// </summary>
    [ObservableProperty]
    private bool _isApiTooLow;

    /// <summary>
    /// 插件卡片状态文案对应的本地化 Key。
    /// </summary>
    [ObservableProperty]
    private string _marketStatusKey = string.Empty;

    /// <summary>
    /// 插件卡片状态文案是否显示。
    /// </summary>
    [ObservableProperty]
    private bool _isStatusVisible;

    /// <summary>
    /// 插件主操作按钮文案对应的本地化 Key。
    /// </summary>
    [ObservableProperty]
    private string _primaryActionKey = "Install";

    /// <summary>
    /// 当前是否允许执行主操作按钮。
    /// </summary>
    [ObservableProperty]
    private bool _canExecutePrimaryAction = true;

    /// <summary>
    /// 当前是否允许卸载该插件。
    /// </summary>
    [ObservableProperty]
    private bool _canUninstall;

    /// <summary>
    /// 插件兼容性说明文本。
    /// </summary>
    [ObservableProperty]
    private string _compatibilityMessage = string.Empty;

    /// <summary>
    /// 卸载按钮文案对应的本地化 Key。
    /// </summary>
    [ObservableProperty]
    private string _uninstallActionKey = "Uninstall";
}
