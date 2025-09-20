using System.Collections.ObjectModel;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public interface IExtensionService
{
    ILogger<IExtensionService> Logger { get; set; }
    ISharedDataService SharedDataService { get; set; }
    ReadOnlyDictionary<IExtension, bool> ReadOnlyExtensions { get; }
    ObservableCollection<Border> ExtensionUIs { get; }

    /// <summary>
    /// 当 Extensions (即ReadOnlyExtensions) 更改时触发此事件。
    /// </summary>
    event EventHandler ExtensionsChanged;

    /// <summary>
    /// 若 sharedDataService 发生变化，可以通过此方法更新 ExtensionService 中的 SharedDataService。
    /// </summary>
    /// <param name="sharedDataService"></param>
    void SetSharedDataService(ISharedDataService sharedDataService);

    /// <summary>
    /// 若主服务的 Logger 发生变化，设置
    /// </summary>
    /// <param name="logger"></param>
    void SetLogger(ILogger<IExtensionService> logger);

    /// <summary>
    /// 注册一个扩展。
    /// </summary>
    /// <param name="extension">要注册的扩展主类</param>
    void RegisterExtension(IExtension extension);

    /// <summary>
    /// 注销一个扩展。
    /// </summary>
    /// <param name="extension"></param>
    void UnregisterExtension(IExtension extension);

    /// <summary>
    /// 启用一个扩展。
    /// </summary>
    /// <param name="extension"></param>
    void EnableExtension(IExtension extension);

    /// <summary>
    /// 禁用一个扩展。
    /// </summary>
    /// <param name="extension"></param>
    void DisableExtension(IExtension extension);

    /// <summary>
    /// 当有插件成功注册或注销用户界面时会触发该事件。
    /// </summary>
    event EventHandler ExtensionUIsUpdatedEvent;

    /// <summary>
    /// 用于向用户界面更新一个新的UI。
    /// </summary>
    /// <param name="ui"></param>
    void RegisterUI(IExtension extension, Border ui);

    /// <summary>
    /// 用于向用户界面注销一个UI。
    /// </summary>
    /// <param name="ui"></param>
    void UnregisterUI(IExtension extension, Border ui);

    /// <summary>
    /// 以反射方式获取某 Extension 的信息。
    /// 可以以此避免将未知风险的 Extension 实例化。
    /// </summary>
    /// <param name="extensionType"></param>
    /// <exception cref="ArgumentException"><paramref name="extensionType"/> 没有实现 IExtension 接口。</exception>
    /// <exception cref="InvalidOperationException"><paramref name="extensionType"/> 没有提供 [ExtensionManifest] 特性。</exception>
    ExtensionManifest GetExtensionManifest(Type extensionType);

    void LoadExtensions(string extensionsDirectory);
    void UnloadExtensions();
}