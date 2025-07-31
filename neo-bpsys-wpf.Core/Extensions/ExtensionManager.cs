using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
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
    public ILogger<ExtensionManager> Logger { get; set; }

    public const string ExtensionsSuffix = ".dll";

    public ISharedDataService SharedDataService;

    private Dictionary<IExtension, bool> Extensions { get; } = new();
    
    public ReadOnlyDictionary<IExtension, bool> ReadOnlyExtensions => new(Extensions);
    
    public ObservableCollection<Border> ExtensionUIs { get; internal set; } = new();

    private ExtensionManager()
    {
    }
    
    /// <summary>
    /// 获取 ExtensionManager 的单例实例。
    /// 该方法应在 ExtensionManager 已经被初始化后调用。
    /// </summary>
    /// <returns></returns>
    public static ExtensionManager Instance()
    {
        lock (Lock)
        {
            return _instance ??= new ExtensionManager();
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
    
    public void SetLogger(ILogger<ExtensionManager> logger)
    {
        lock (Lock)
        {
            Logger = logger;
        }
    }

    public void RegisterExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        
        lock (Lock)
        {
            if (!Extensions.Keys.Contains(extension))
            {
                EnableExtension(extension);
                Extensions.Add(extension, false);
                extension.Initialize(); // 初始化插件
                EnableExtension(extension);
            }
        }
    }
    public void UnregisterExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        
        lock (Lock)
        {
            DisableExtension(extension); // 以防插件没有被禁用，禁用它
            Extensions.Remove(extension);
        }
    }
    
    public void EnableExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        
        lock (Lock)
        {
            if (Extensions.Keys.Contains(extension))
            {
                if (Extensions[extension])
                {
                    return; // 插件已经启用
                }
                Extensions[extension] = true; // 标记插件为已启用
                // 触发插件启用事件
                extension.OnEnable();
            }
        }
    }

    public void DisableExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        lock (Lock)
        {
            if (Extensions.Keys.Contains(extension))
            {
                extension.OnDisable();
            }
        }
    }
    
    /// <summary>
    /// 当有插件成功注册或注销用户界面时会触发该事件。
    /// </summary>
    public event EventHandler ExtensionUIsUpdatedEvent;
    
    /// <summary>
    /// 用于向用户界面更新一个新的UI。
    /// </summary>
    /// <param name="ui"></param>
    public void RegisterUI(Border ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        
        lock (Lock)
        {
            if (!ExtensionUIs.Contains(ui))
            {
                ExtensionUIs.Add(ui);
                ExtensionUIsUpdatedEvent?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    /// <summary>
    /// 用于向用户界面注销一个UI。
    /// </summary>
    /// <param name="ui"></param>
    public void UnregisterUI(Border ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        
        lock (Lock)
        {
            if (ExtensionUIs.Remove(ui))
            {
                ExtensionUIsUpdatedEvent?.Invoke(this, EventArgs.Empty);
            }
            
        }
    }

    public void LoadExtensions(string extensionsDirectory)
    {
        var extensions = new List<IExtension>();
        if (!Directory.Exists(extensionsDirectory))
        {
            Logger.LogWarning("Extensions directory does not exist: {Directory}", extensionsDirectory);
            return;
        }
        
        var extensionFiles = Directory.GetFiles(extensionsDirectory, "*" + ExtensionsSuffix);

        foreach (var extensionFile in extensionFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(extensionFile);
                var types = assembly.GetTypes()
                    .Where(t => typeof(IExtension).IsAssignableFrom(t) && !t.IsAbstract);
                foreach (var type in types)
                {
                    if (Activator.CreateInstance(type) is IExtension extension)
                    {
                        this.RegisterExtension(extension);
                    }
                }
            }
            catch (ReflectionTypeLoadException e)
            {
                Logger.LogError(e, "从文件 {File} 中加载扩展失败：{Message}", extensionFile, e.Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e,"加载扩展时发生了错误：{Message}", e.Message);
            }
        }
    }
    
    public void UnloadExtensions()
    {
        lock (Lock)
        {
            foreach (var extension in Extensions.Keys.ToList())
            {
                this.UnregisterExtension(extension);
            }
            Extensions.Clear();
            ExtensionUIs.Clear();
        }
    }
}