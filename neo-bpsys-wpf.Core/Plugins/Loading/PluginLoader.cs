using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;

namespace neo_bpsys_wpf.Core.Plugins.Loading;

/// <summary>
/// 默认插件加载器实现
/// </summary>
public sealed class PluginLoader : IPluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly Dictionary<string, PluginLoadContext> _loadContexts = new();
    private readonly object _lock = new();

    /// <summary>
    /// 创建插件加载器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public PluginAssemblyLoadResult LoadAssembly(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return PluginAssemblyLoadResult.Failed("Assembly path cannot be null or empty.");
        }

        if (!File.Exists(assemblyPath))
        {
            return PluginAssemblyLoadResult.Failed($"Assembly file not found: {assemblyPath}");
        }

        try
        {
            _logger.LogInformation("Loading plugin assembly: {AssemblyPath}", assemblyPath);

            // 生成临时插件ID用于加载上下文
            var tempPluginId = Path.GetFileNameWithoutExtension(assemblyPath);

            lock (_lock)
            {
                // 检查是否已加载
                if (_loadContexts.ContainsKey(tempPluginId))
                {
                    _logger.LogWarning("Plugin assembly already loaded: {PluginId}", tempPluginId);
                    return PluginAssemblyLoadResult.Failed($"Plugin assembly already loaded: {tempPluginId}");
                }
            }

            // 创建加载上下文
            var loadContext = new PluginLoadContext(assemblyPath, tempPluginId);
            Assembly assembly;

            try
            {
                assembly = loadContext.LoadPluginAssembly();
            }
            catch (Exception ex)
            {
                loadContext.Unload();
                _logger.LogError(ex, "Failed to load assembly: {AssemblyPath}", assemblyPath);
                return PluginAssemblyLoadResult.Failed($"Failed to load assembly: {ex.Message}", ex);
            }

            // 查找实现IPlugin接口的类型
            var pluginTypes = FindPluginTypes(assembly);

            if (pluginTypes.Count == 0)
            {
                loadContext.Unload();
                _logger.LogWarning("No plugin types found in assembly: {AssemblyPath}", assemblyPath);
                return PluginAssemblyLoadResult.Failed("No plugin types found in assembly.");
            }

            lock (_lock)
            {
                _loadContexts[tempPluginId] = loadContext;
            }

            _logger.LogInformation("Successfully loaded plugin assembly: {AssemblyPath}, found {Count} plugin(s)",
                assemblyPath, pluginTypes.Count);

            return PluginAssemblyLoadResult.Succeeded(assembly, loadContext, pluginTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading plugin assembly: {AssemblyPath}", assemblyPath);
            return PluginAssemblyLoadResult.Failed($"Unexpected error: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public bool UnloadAssembly(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        lock (_lock)
        {
            if (!_loadContexts.TryGetValue(pluginId, out var loadContext))
            {
                _logger.LogWarning("Plugin not found for unloading: {PluginId}", pluginId);
                return false;
            }

            try
            {
                _loadContexts.Remove(pluginId);
                loadContext.Unload();

                // 触发GC以确保程序集被卸载
                GC.Collect();
                GC.WaitForPendingFinalizers();

                _logger.LogInformation("Successfully unloaded plugin: {PluginId}", pluginId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unload plugin: {PluginId}", pluginId);
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public PluginLoadContext? GetLoadContext(string pluginId)
    {
        lock (_lock)
        {
            return _loadContexts.TryGetValue(pluginId, out var context) ? context : null;
        }
    }

    /// <inheritdoc/>
    public bool IsValidPluginAssembly(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            return false;
        }

        try
        {
            // 使用 PEReader 进行轻量级检查
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new System.Reflection.PortableExecutable.PEReader(stream);

            if (!peReader.HasMetadata)
            {
                return false;
            }

            // 获取元数据读取器
            var metadataReader = peReader.GetMetadataReader();

            // 检查是否包含类型定义
            foreach (var typeHandle in metadataReader.TypeDefinitions)
            {
                var typeDef = metadataReader.GetTypeDefinition(typeHandle);
                var typeName = metadataReader.GetString(typeDef.Name);

                // 简单检查：类型名称是否包含"Plugin"
                if (typeName.Contains("Plugin", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 查找程序集中实现IPlugin接口的类型
    /// </summary>
    private List<Type> FindPluginTypes(Assembly assembly)
    {
        var pluginTypes = new List<Type>();
        var pluginInterface = typeof(IPlugin);

        try
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.IsClass &&
                    !type.IsAbstract &&
                    pluginInterface.IsAssignableFrom(type))
                {
                    pluginTypes.Add(type);
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            _logger.LogWarning(ex, "Some types could not be loaded from assembly");

            // 处理部分加载的类型
            foreach (var type in ex.Types.Where(t => t != null))
            {
                if (type!.IsClass &&
                    !type.IsAbstract &&
                    pluginInterface.IsAssignableFrom(type))
                {
                    pluginTypes.Add(type);
                }
            }
        }

        return pluginTypes;
    }
}
