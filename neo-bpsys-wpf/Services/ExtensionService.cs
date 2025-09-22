using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 一个用于管理扩展的单例类，通过单例的方式确保全局只有一个实例。
/// 该类提供了注册和注销扩展的方法，并且可以设置共享数据服务
/// </summary>
public class ExtensionService : IExtensionService
{
    private static readonly Lock Lock = new();
    private readonly ILogger<IExtensionService> _logger;

    private const string ExtensionsSuffix = ".dll";

    public ISharedDataService SharedDataService { get; set; }

    /// <summary>
    /// 当 Extensions (即ReadOnlyExtensions) 更改时触发此事件。
    /// </summary>
    public event EventHandler? ExtensionsChanged;
    
    private Dictionary<IExtension, bool> Extensions { get; } = [];
    
    public ReadOnlyDictionary<IExtension, bool> ReadOnlyExtensions => new(Extensions);
    
    private Dictionary<IExtension, ObservableCollection<Border>> ExtensionUis { get; } = [];

    private ObservableCollection<Border> UiCollection { get; } = [];

    public ExtensionService(ILogger<ExtensionService> logger, ISharedDataService sharedDataService)
    {
        _logger = logger;
        SharedDataService = sharedDataService;
        // Extensions Startup
        _logger.LogInformation("Initializing ExtensionService...");
        
        var extensionsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "neo-bpsys-wpf", "Extensions");
        if (!Directory.Exists(extensionsDirectory))
        {
            Directory.CreateDirectory(extensionsDirectory);
        }
        _logger.LogInformation("ExtensionService initialized. Initializing Extensions (At: {ExtensionsPath})...", extensionsDirectory);
        _logger.LogInformation("{ExtensionCount} extensions has been initialized. ", ReadOnlyExtensions.Count);
        LoadExtensions(extensionsDirectory);
    }

    public ObservableCollection<Border> ExtensionUIs
    {
        get
        {
            // 对_extensionUis 进行筛选，返回一个 ObservableCollection<Border>，只包含已启用的扩展的 UI
            UiCollection.Clear();
            foreach (var ui in ExtensionUis)
            {
                if (Extensions.TryGetValue(ui.Key, out bool value) && value)
                {
                    foreach (var border in ui.Value)
                    {
                        if (!UiCollection.Contains(border))
                        {
                            UiCollection.Add(border);
                        }
                    }
                }
            }
            return UiCollection;
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
            if (Extensions.TryAdd(extension, false))
            {
                extension.ExtensionService = this;
                ExtensionsChanged?.Invoke(this, EventArgs.Empty);
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
            ExtensionsChanged?.Invoke(this, EventArgs.Empty);
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
                ExtensionsChanged?.Invoke(this, EventArgs.Empty);
                // 触发插件启用事件
                extension.OnEnable();
                FlushExtensionUIs();
                _logger.LogInformation("Extension {Name} has been enabled.", GetExtensionManifest(extension.GetType()).ExtensionName);
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
            if (Extensions.ContainsKey(extension))
            {
                if (!Extensions[extension]) return; // 插件未启用
                Extensions[extension] = false; // 标记插件为未启用
                ExtensionsChanged?.Invoke(this, EventArgs.Empty);
                // 触发插件禁用事件
                extension.OnDisable();
                FlushExtensionUIs();
                _logger.LogInformation("Extension {Name} has been disabled.", GetExtensionManifest(extension.GetType()).ExtensionName);
            }
        }
    }
    
    /// <summary>
    /// 当有插件成功注册或注销用户界面时会触发该事件。
    /// </summary>
    public event EventHandler? ExtensionUIsUpdatedEvent;

    /// <summary>
    /// 用于向用户界面更新一个新的UI。
    /// </summary>
    /// <param name="extension">要注册的插件</param>
    /// <param name="ui">要注册的UI</param>
    public void RegisterUi(IExtension extension, Border ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        
        lock (Lock)
        {
            if (Extensions.ContainsKey(extension))
            {
                if (!ExtensionUis.TryGetValue(extension, out var value))
                {
                    value = [];
                    ExtensionUis[extension] = value;
                }

                if (value.Contains(ui)) return;
                value.Add(ui);
                ExtensionUIsUpdatedEvent?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// 用于向用户界面注销一个UI。
    /// </summary>
    /// <param name="extension">要注销的插件</param>
    /// <param name="ui">要注销的UI</param>
    public void UnregisterUi(IExtension extension, Border ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        
        lock (Lock)
        {
            if (!Extensions.ContainsKey(extension)) return;
            if (ExtensionUis[extension].Remove(ui))
            {
                ExtensionUIsUpdatedEvent?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// 以反射方式获取某 Extension 的信息。
    /// 可以以此避免将未知风险的 Extension 实例化。
    /// </summary>
    /// <param name="extensionType"></param>
    /// <exception cref="ArgumentException"><paramref name="extensionType"/> 没有实现 IExtension 接口。</exception>
    /// <exception cref="InvalidOperationException"><paramref name="extensionType"/> 没有提供 [ExtensionManifest] 特性。</exception>
    public ExtensionManifest GetExtensionManifest(Type extensionType)
    {
        ArgumentNullException.ThrowIfNull(extensionType);
        if (!typeof(IExtension).IsAssignableFrom(extensionType))
        {
            throw new ArgumentException(
                $"Type {extensionType.Name} has not implemented interface IExtension. ");
        }
        
        var extensionManifest = extensionType.GetCustomAttribute<ExtensionManifest>();
        
        if (extensionManifest == null)
        {
            throw new InvalidOperationException(
                $"Class {extensionType.Name} is not decorated with [ExtensionManifest] attribute. ");
        }

        return extensionManifest;
    }
    
    public void LoadExtensions(string extensionsDirectory)
    {
        var extensions = new List<IExtension>();
        if (!Directory.Exists(extensionsDirectory))
        {
            _logger.LogWarning("Extensions directory does not exist: {Directory}", extensionsDirectory);
            return;
        }
        
        var extensionFiles = Directory.GetFiles(extensionsDirectory, "*" + ExtensionsSuffix);

        foreach (var extensionFile in extensionFiles)
        {
            try
            {
                _logger.LogInformation("Loading extension from file: {File}", extensionFile);
                var assembly = Assembly.LoadFrom(extensionFile);
                var types = assembly.GetTypes()
                    .Where(t => typeof(IExtension).IsAssignableFrom(t) && !t.IsAbstract)
                    .ToList(); // 修复“可能多次枚举”
                #if DEBUG
                _logger.LogInformation("Found {Count} types implementing IExtension in {File}", types.Count, extensionFile);
                #endif
                foreach (var type in types)
                {
                    try
                    {
                        var extensionManifest = GetExtensionManifest(type);
                        _logger.LogInformation(
                            "Registering extension {Name} [{Version}({VersionCode})] By {Author} from file: {File}",
                            extensionManifest.ExtensionName, extensionManifest.ExtensionVersion,
                            extensionManifest.ExtensionVersionCode, extensionManifest.ExtensionAuthor, extensionFile);
                        if (Activator.CreateInstance(type)
                            is IExtension
                            extension) // 已经通过筛选 types 与使用 GetExtensionManifest 方法两次确定 type 为 IExtension 的实现，但顺便在创建实例的时候再次确认
                        {
                            RegisterExtension(extension);
                            _logger.LogInformation("Extension {Name} registered successfully from file {File}",
                                GetExtensionManifest(extension.GetType()).ExtensionName, extensionFile);
                        }
                    }
                    catch (ArgumentException e)
                    {
#if DEBUG
                        _logger.LogWarning(e, "{Message}", e.Message);
#endif
                    }
                    catch (InvalidOperationException e)
                    {
                        _logger.LogError(e, "An error occurred when loading an extension: {Message}", e.Message);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "An error occurred when loading an extension: {Message}", e.Message);
                    }
                }
            }
            catch (ReflectionTypeLoadException e)
            {
                _logger.LogError(e, "Failed to load an extension from {File}: {Message}", extensionFile, e.Message);
            }
            catch (Exception e)
            {
                _logger.LogError(e,"An error occured when loading extensions: {Message}", e.Message);
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
            ExtensionsChanged?.Invoke(this, EventArgs.Empty);
            ExtensionUis.Clear();
            ExtensionUIsUpdatedEvent?.Invoke(this, EventArgs.Empty);
        }
    }

    private void FlushExtensionUIs()
    {
        ExtensionUIsUpdatedEvent?.Invoke(this, EventArgs.Empty);
    }
}