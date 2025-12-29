using System.Reflection;
using System.Runtime.Loader;

namespace neo_bpsys_wpf.Core.Plugins.Loading;

/// <summary>
/// 插件加载上下文，用于隔离插件程序集
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginPath;

    /// <summary>
    /// 插件ID
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// 创建插件加载上下文
    /// </summary>
    /// <param name="pluginPath">插件程序集路径</param>
    /// <param name="pluginId">插件ID</param>
    public PluginLoadContext(string pluginPath, string pluginId)
        : base(name: pluginId, isCollectible: true)
    {
        _pluginPath = pluginPath;
        PluginId = pluginId;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <inheritdoc/>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 首先检查是否是共享程序集（主应用程序已加载的程序集）
        var sharedAssembly = Default.Assemblies.FirstOrDefault(a =>
            a.GetName().Name == assemblyName.Name);

        if (sharedAssembly != null)
        {
            // 使用共享程序集，避免类型不兼容问题
            return sharedAssembly;
        }

        // 尝试使用依赖解析器
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    /// <inheritdoc/>
    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return nint.Zero;
    }

    /// <summary>
    /// 加载插件主程序集
    /// </summary>
    /// <returns>插件程序集</returns>
    public Assembly LoadPluginAssembly()
    {
        return LoadFromAssemblyPath(_pluginPath);
    }
}

/// <summary>
/// 插件加载器接口
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// 加载插件程序集
    /// </summary>
    /// <param name="assemblyPath">程序集路径</param>
    /// <returns>加载结果</returns>
    PluginAssemblyLoadResult LoadAssembly(string assemblyPath);

    /// <summary>
    /// 卸载插件程序集
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>是否成功卸载</returns>
    bool UnloadAssembly(string pluginId);

    /// <summary>
    /// 获取插件的加载上下文
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>加载上下文</returns>
    PluginLoadContext? GetLoadContext(string pluginId);

    /// <summary>
    /// 检查程序集是否为有效的插件程序集
    /// </summary>
    /// <param name="assemblyPath">程序集路径</param>
    /// <returns>是否有效</returns>
    bool IsValidPluginAssembly(string assemblyPath);
}

/// <summary>
/// 插件程序集加载结果
/// </summary>
public sealed class PluginAssemblyLoadResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 加载的程序集
    /// </summary>
    public Assembly? Assembly { get; init; }

    /// <summary>
    /// 加载上下文
    /// </summary>
    public PluginLoadContext? LoadContext { get; init; }

    /// <summary>
    /// 发现的插件类型
    /// </summary>
    public IReadOnlyList<Type> PluginTypes { get; init; } = Array.Empty<Type>();

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 异常
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static PluginAssemblyLoadResult Succeeded(
        Assembly assembly,
        PluginLoadContext loadContext,
        IReadOnlyList<Type> pluginTypes) => new()
        {
            Success = true,
            Assembly = assembly,
            LoadContext = loadContext,
            PluginTypes = pluginTypes
        };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static PluginAssemblyLoadResult Failed(string errorMessage, Exception? exception = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        Exception = exception
    };
}
