using System.Reflection;
using System.Runtime.Loader;

namespace neo_bpsys_wpf.Plugins;

/// <summary>
/// 插件程序集加载上下文 - 提供插件隔离
/// Plugin assembly load context - Provides plugin isolation
/// </summary>
internal class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginPath;

    public PluginAssemblyLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _pluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 尝试从插件目录加载程序集
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // 对于共享程序集（如 Core、Framework等），从默认上下文加载
        // 这样可以确保插件和主应用程序共享相同的类型
        var sharedAssemblies = new[]
        {
            "neo-bpsys-wpf.Core",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions",
            "WPF-UI"
        };

        if (sharedAssemblies.Any(name => assemblyName.Name?.StartsWith(name) == true))
        {
            return null; // 返回null让默认上下文处理
        }

        return null;
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
