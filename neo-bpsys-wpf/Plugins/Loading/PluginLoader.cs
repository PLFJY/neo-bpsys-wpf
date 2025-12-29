using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Plugins.Abstractions;
using neo_bpsys_wpf.Core.Plugins.Services;

namespace neo_bpsys_wpf.Plugins.Loading;

/// <summary>
/// 插件加载上下文，用于隔离插件程序集
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 首先尝试从插件目录加载
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // 如果插件目录没有，尝试从默认上下文加载（共享程序集）
        try
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch
        {
            return null;
        }
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }
        return IntPtr.Zero;
    }
}

/// <summary>
/// 插件加载器
/// </summary>
public class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly Dictionary<string, (PluginLoadContext Context, IPlugin Plugin)> _loadedPlugins = new();

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 加载插件
    /// </summary>
    /// <param name="pluginPath">插件DLL路径</param>
    /// <returns>加载结果</returns>
    public PluginLoadResult LoadPlugin(string pluginPath)
    {
        try
        {
            if (!File.Exists(pluginPath))
            {
                return PluginLoadResult.Failed($"Plugin file not found: {pluginPath}");
            }

            var loadContext = new PluginLoadContext(pluginPath);
            var assembly = loadContext.LoadFromAssemblyPath(pluginPath);

            // 查找实现IPlugin接口的类型
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) 
                                     && !t.IsAbstract 
                                     && !t.IsInterface);

            if (pluginType == null)
            {
                loadContext.Unload();
                return PluginLoadResult.Failed($"No IPlugin implementation found in assembly: {pluginPath}");
            }

            // 创建插件实例
            var plugin = (IPlugin?)Activator.CreateInstance(pluginType);
            if (plugin == null)
            {
                loadContext.Unload();
                return PluginLoadResult.Failed($"Failed to create plugin instance: {pluginType.FullName}");
            }

            // 检查是否已加载相同ID的插件
            if (_loadedPlugins.ContainsKey(plugin.Metadata.Id))
            {
                loadContext.Unload();
                return PluginLoadResult.Failed($"Plugin with ID '{plugin.Metadata.Id}' is already loaded");
            }

            _loadedPlugins[plugin.Metadata.Id] = (loadContext, plugin);
            _logger.LogInformation("Plugin loaded: {PluginId} v{Version}", 
                plugin.Metadata.Id, plugin.Metadata.Version);

            return PluginLoadResult.Succeeded(plugin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin from {PluginPath}", pluginPath);
            return PluginLoadResult.Failed($"Failed to load plugin: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 卸载插件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    public async Task UnloadPluginAsync(string pluginId)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out var entry))
        {
            _logger.LogWarning("Plugin not found for unload: {PluginId}", pluginId);
            return;
        }

        try
        {
            // 停止插件
            if (entry.Plugin.State == PluginState.Running)
            {
                await entry.Plugin.StopAsync();
            }

            // 释放插件资源
            entry.Plugin.Dispose();

            // 从字典移除
            _loadedPlugins.Remove(pluginId);

            // 卸载程序集上下文
            entry.Context.Unload();

            // 强制GC以确保程序集被卸载
            for (int i = 0; i < 10 && entry.Context.IsCollectible; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            _logger.LogInformation("Plugin unloaded: {PluginId}", pluginId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading plugin: {PluginId}", pluginId);
            throw;
        }
    }

    /// <summary>
    /// 获取已加载的插件
    /// </summary>
    public IReadOnlyList<IPlugin> GetLoadedPlugins()
    {
        return _loadedPlugins.Values.Select(e => e.Plugin).ToList();
    }

    /// <summary>
    /// 获取指定ID的插件
    /// </summary>
    public IPlugin? GetPlugin(string pluginId)
    {
        return _loadedPlugins.TryGetValue(pluginId, out var entry) ? entry.Plugin : null;
    }

    /// <summary>
    /// 检查插件是否已加载
    /// </summary>
    public bool IsPluginLoaded(string pluginId)
    {
        return _loadedPlugins.ContainsKey(pluginId);
    }
}
