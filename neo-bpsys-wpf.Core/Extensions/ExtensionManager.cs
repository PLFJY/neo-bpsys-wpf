using System.Collections.ObjectModel;
using System.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Extensions;

/// <summary>
/// 一个用于管理扩展的单例类，通过单例的方式确保全局只有一个实例。
/// 该类提供了注册和注销扩展的方法，并且可以设置共享数据服务
/// </summary>
public class ExtensionManager
{
    private static ExtensionManager _instance;
    private static readonly object Lock = new();
    
    public ISharedDataService SharedDataService;

    private ObservableCollection<IExtension> Extensions { get; } = new();
    
    private ExtensionManager(ISharedDataService sharedDataService)
    {
        SharedDataService = sharedDataService;
    }
    
    /// <summary>
    /// 获取一个 ExtensionManager 的单例实例，若不存在则创建一个新的实例。
    /// </summary>
    /// <param name="sharedDataService">当前Context中的 sharedDataService</param>
    /// <returns></returns>
    public static ExtensionManager Instance(ISharedDataService sharedDataService)
    {
        lock (Lock) { 
            return _instance ??= new ExtensionManager(sharedDataService);
        }
    }
    /// <summary>
    /// 获取 ExtensionManager 的单例实例，若不存在则抛出异常。
    /// 该方法应在 ExtensionManager 已经被初始化后调用。
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static ExtensionManager Instance()
    {
        lock (Lock)
        {
            if (_instance == null)
            {
                throw new InvalidOperationException("ExtensionManager has not been initialized. Please call Instance(ISharedDataService) first.");
            }
            return _instance;
        }
    }
    
    /// <summary>
    /// 检查 ExtensionManager 是否已经被初始化。
    /// </summary>
    /// <returns></returns>
    public static bool IsInitialized()
    {
        lock (Lock)
        {
            return _instance != null;
        }
    }
    
    /// <summary>
    /// 若 sharedDataService 发生变化，可以通过此方法更新 ExtensionManager 中的 SharedDataService。
    /// </summary>
    /// <param name="sharedDataService"></param>
    public void SetSharedDataService(ISharedDataService sharedDataService)
    {
        lock (Lock)
        {
            SharedDataService = sharedDataService;
        }
    }
    
    public void RegisterExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        
        lock (Lock)
        {
            if (!Extensions.Contains(extension))
            {
                Extensions.Add(extension);
                extension.Initialize(); // 初始化插件
            }
        }
    }
    public void UnregisterExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        
        lock (Lock)
        {
            Extensions.Remove(extension);
        }
    }
    
    public void EnableExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        
        lock (Lock)
        {
            if (Extensions.Contains(extension))
            {
                extension.OnEnable();
            }
        }
    }

    public void DisableExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        lock (Lock)
        {
            if (Extensions.Contains(extension))
            {
                extension.OnDisable();
            }
        }
    }
}