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
    private const string SettingsFileName = "Settings.json";
    private readonly string _path;

    /// <summary>初始化设置存储。</summary>
    /// <param name="plugin">Web Renderer 插件入口。</param>
    /// <exception cref="ArgumentNullException"><paramref name="plugin"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">插件配置目录为空或设置文件路径逃逸出该目录。</exception>
    public WebRendererSettingsStore(WebRendererPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (string.IsNullOrWhiteSpace(plugin.PluginConfigFolder))
        {
            throw new InvalidOperationException("Web Renderer plugin config folder is not initialized.");
        }

        var configRoot = Path.GetFullPath(plugin.PluginConfigFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(configRoot, SettingsFileName));
        if (!path.StartsWith(configRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Web Renderer settings path escapes the plugin config folder.");
        }

        _path = path;
        Settings = ConfigureFileHelper.LoadConfig<WebRendererPluginSettings>(_path)
                   ?? new WebRendererPluginSettings();
    }

    /// <summary>获取当前可编辑设置。</summary>
    /// <remarks>修改后必须调用 <see cref="Save"/> 才会持久化；该可变引用只供插件内部管理页使用。</remarks>
    public WebRendererPluginSettings Settings { get; }

    /// <summary>保存当前设置。</summary>
    public void Save() => ConfigureFileHelper.SaveConfig(_path, Settings);
}
