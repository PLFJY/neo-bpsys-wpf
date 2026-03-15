using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Models;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Models.Plugins;

public partial class PluginMarketItem : ObservableObjectBase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("readme")]
    public string Readme { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("downloadURL")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonIgnore]
    public PluginInfo? LocalPlugin { get; set; }

    [ObservableProperty]
    private string _resolvedIconUrl = string.Empty;

    [ObservableProperty]
    private string _resolvedReadmeUrl = string.Empty;

    [ObservableProperty]
    private string _resolvedDownloadUrl = string.Empty;

    [ObservableProperty]
    private string _readmeMarkdown = string.Empty;

    [ObservableProperty]
    private bool _isReadmeLoading;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _hasUpdateAvailable;

    [ObservableProperty]
    private bool _isApiCompatible = true;

    [ObservableProperty]
    private bool _isHostVersionTooLow;

    [ObservableProperty]
    private bool _isApiTooLow;

    [ObservableProperty]
    private string _marketStatusKey = string.Empty;

    [ObservableProperty]
    private bool _isStatusVisible;

    [ObservableProperty]
    private string _primaryActionKey = "Install";

    [ObservableProperty]
    private bool _canExecutePrimaryAction = true;

    [ObservableProperty]
    private bool _canUninstall;

    [ObservableProperty]
    private string _compatibilityMessage = string.Empty;

    [ObservableProperty]
    private string _uninstallActionKey = "Uninstall";
}
