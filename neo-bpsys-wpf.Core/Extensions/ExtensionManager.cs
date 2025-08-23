using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;

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
    
    private Dictionary<IExtension, ObservableCollection<Border>> _extensionUis { get; } = new();

    private ObservableCollection<Border> _uiCollection { get; } = new();

    public ObservableCollection<Border> ExtensionUIs
    {
        get
        {
            // 对_extensionUis 进行筛选，返回一个 ObservableCollection<Border>，只包含已启用的扩展的 UI
            _uiCollection.Clear();
            foreach (var ui in _extensionUis)
            {
                if (Extensions.ContainsKey(ui.Key) && Extensions[ui.Key])
                {
                    foreach (var border in ui.Value)
                    {
                        if (!_uiCollection.Contains(border))
                        {
                            _uiCollection.Add(border);
                        }
                    }
                }
            }
            return _uiCollection;
        }
    }

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
    
    /// <summary>
    /// 若主服务的 Logger 发生变化，设置
    /// </summary>
    /// <param name="logger"></param>
    public void SetLogger(ILogger<ExtensionManager> logger)
    {
        lock (Lock)
        {
            Logger = logger;
        }
    }

    /// <summary>
    /// 注册一个扩展。
    /// </summary>
    /// <param name="extension">要注册的扩展主类</param>
    public void RegisterExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        
        lock (Lock)
        {
            if (!Extensions.Keys.Contains(extension))
            {
                Extensions.Add(extension, false);
                extension.Initialize(); // 初始化插件
                EnableExtension(extension); // 启用该插件
            }
        }
    }
    /// <summary>
    /// 注销一个扩展。
    /// </summary>
    /// <param name="extension"></param>
    public void UnregisterExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        
        lock (Lock)
        {
            DisableExtension(extension); // 以防插件没有被禁用，禁用它
            extension.Uninitialize(); // 卸载插件
            Extensions.Remove(extension);
        }
    }
    /// <summary>
    /// 启用一个扩展。
    /// </summary>
    /// <param name="extension"></param>
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
                FlushExtensionUIs();
            }
        }
    }

    /// <summary>
    /// 禁用一个扩展。
    /// </summary>
    /// <param name="extension"></param>
    public void DisableExtension(IExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        lock (Lock)
        {
            if (Extensions.Keys.Contains(extension))
            {
                if (!Extensions[extension]) return; // 插件未启用
                Extensions[extension] = false; // 标记插件为未启用
                // 触发插件禁用事件
                extension.OnDisable();
                FlushExtensionUIs();
                Logger.LogInformation("扩展 {Name} 已经禁用", extension.ExtensionManifest.ExtensionName);
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
    public void RegisterUI(IExtension extension, Border ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        
        lock (Lock)
        {
            if (Extensions.ContainsKey(extension))
            {
                if(!_extensionUis.ContainsKey(extension))
                {
                    _extensionUis[extension] = new ObservableCollection<Border>();
                }
                if (!_extensionUis[extension].Contains(ui))
                {
                    _extensionUis[extension].Add(ui);
                    ExtensionUIsUpdatedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
    
    /// <summary>
    /// 用于向用户界面注销一个UI。
    /// </summary>
    /// <param name="ui"></param>
    public void UnregisterUI(IExtension extension, Border ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        
        lock (Lock)
        {
            if (!Extensions.ContainsKey(extension)) return;
            if (_extensionUis[extension].Remove(ui))
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
                Logger.LogInformation("Loading extension from file: {File}", extensionFile);
                var assembly = Assembly.LoadFrom(extensionFile);
                var types = assembly.GetTypes()
                    .Where(t => typeof(IExtension).IsAssignableFrom(t) && !t.IsAbstract);
                #if DEBUG
                Logger.LogInformation("Found {Count} types implementing IExtension in {File}", types.Count(), extensionFile);
                #endif
                foreach (var type in types)
                {
                    Logger.LogInformation("Registering extension from file: {File}", extensionFile);
                    if (Activator.CreateInstance(type) is IExtension extension)
                    {
                        this.RegisterExtension(extension);
                        Logger.LogInformation("Extension {Name} registered successfully from file {File}", extension.ExtensionManifest.ExtensionName, extensionFile);
                    }
                }
            }
            catch (ReflectionTypeLoadException e)
            {
                Logger.LogError(e, "Failed to load an extension from {File}: {Message}", extensionFile, e.Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e,"An error occured when loading an extension: {Message}", e.Message);
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
            _extensionUis.Clear();
        }
    }

    public void FlushExtensionUIs()
    {
        var _ = ExtensionUIs;
        ExtensionUIsUpdatedEvent?.Invoke(this, EventArgs.Empty);
    }
}