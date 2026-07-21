using neo_bpsys_wpf.Core.Helpers;
using System.IO;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>保存在 Web Renderer 插件目录内的用户设置。</summary>
public sealed class WebRendererPluginSettings
{
    /// <summary>获取或设置监听地址。</summary>
    public string Host { get; set; } = WebRendererLaunchOptions.DefaultAddress;
    /// <summary>获取或设置监听端口。</summary>
    public int Port { get; set; } = WebRendererLaunchOptions.DefaultPort;
    /// <summary>获取或设置是否随应用启动。</summary>
    /// <remarks>
    /// 默认值为 <see langword="false"/>：sidecar 进程不随应用启动自动运行，
    /// 避免未使用 Web Renderer 时产生常驻子进程内存占用。
    /// 用户可在管理页手动启动，或将此属性设为 <see langword="true"/> 恢复自动启动。
    /// 已有 <c>Settings.json</c> 的旧用户升级后保留原值，行为不变。
    /// </remarks>
    public bool StartWithApplication { get; set; }
    /// <summary>获取或设置 Exit fail-open 超时（毫秒）。</summary>
    public int ExitTimeoutMs { get; set; } = 2000;
    /// <summary>获取或设置 Enter fail-open 超时（毫秒）。</summary>
    public int EnterTimeoutMs { get; set; } = 2000;
    /// <summary>获取或设置是否记录协议摘要。</summary>
    public bool LogProtocol { get; set; }
}

/// <summary>提供插件私有设置的加载和保存。</summary>
public sealed class WebRendererSettingsStore
{
    private readonly string _path;

    /// <summary>初始化设置存储。</summary>
    /// <param name="plugin">Web Renderer 插件入口。</param>
    public WebRendererSettingsStore(WebRendererPlugin plugin)
    {
        _path = Path.Combine(plugin.PluginConfigFolder, "Settings.json");
        Settings = ConfigureFileHelper.LoadConfig<WebRendererPluginSettings>(_path);
    }

    /// <summary>获取当前可编辑设置。</summary>
    public WebRendererPluginSettings Settings { get; }

    /// <summary>保存当前设置。</summary>
    public void Save() => ConfigureFileHelper.SaveConfig(_path, Settings);
}
