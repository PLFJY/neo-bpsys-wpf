using System.Windows;

namespace neo_bpsys_wpf.Core.Plugins.UI;

/// <summary>
/// UI扩展服务接口，用于管理插件的UI组件
/// </summary>
public interface IUIExtensionService
{
    /// <summary>
    /// 注册UI组件
    /// </summary>
    /// <param name="registration">组件注册信息</param>
    void RegisterComponent(UIComponentRegistration registration);

    /// <summary>
    /// 取消注册UI组件
    /// </summary>
    /// <param name="componentId">组件ID</param>
    void UnregisterComponent(string componentId);

    /// <summary>
    /// 获取指定扩展点的所有组件
    /// </summary>
    /// <param name="extensionPoint">扩展点</param>
    /// <returns>组件注册信息列表</returns>
    IReadOnlyList<UIComponentRegistration> GetComponents(UIExtensionPoint extensionPoint);

    /// <summary>
    /// 获取自定义扩展点的所有组件
    /// </summary>
    /// <param name="customExtensionPointName">自定义扩展点名称</param>
    /// <returns>组件注册信息列表</returns>
    IReadOnlyList<UIComponentRegistration> GetComponents(string customExtensionPointName);

    /// <summary>
    /// 创建组件实例
    /// </summary>
    /// <param name="componentId">组件ID</param>
    /// <returns>组件实例</returns>
    FrameworkElement? CreateComponent(string componentId);

    /// <summary>
    /// 注册导航页面
    /// </summary>
    /// <param name="registration">页面注册信息</param>
    void RegisterNavigationPage(NavigationPageRegistration registration);

    /// <summary>
    /// 取消注册导航页面
    /// </summary>
    /// <param name="pageId">页面ID</param>
    void UnregisterNavigationPage(string pageId);

    /// <summary>
    /// 获取所有注册的导航页面
    /// </summary>
    /// <returns>导航页面注册信息列表</returns>
    IReadOnlyList<NavigationPageRegistration> GetNavigationPages();

    /// <summary>
    /// 注册窗口
    /// </summary>
    /// <param name="registration">窗口注册信息</param>
    void RegisterWindow(WindowRegistration registration);

    /// <summary>
    /// 取消注册窗口
    /// </summary>
    /// <param name="windowId">窗口ID</param>
    void UnregisterWindow(string windowId);

    /// <summary>
    /// 获取所有注册的窗口
    /// </summary>
    /// <returns>窗口注册信息列表</returns>
    IReadOnlyList<WindowRegistration> GetWindows();

    /// <summary>
    /// 创建并显示窗口
    /// </summary>
    /// <param name="windowId">窗口ID</param>
    /// <returns>窗口实例</returns>
    Window? ShowWindow(string windowId);

    /// <summary>
    /// 当组件注册时触发的事件
    /// </summary>
    event EventHandler<UIComponentRegistration>? ComponentRegistered;

    /// <summary>
    /// 当组件取消注册时触发的事件
    /// </summary>
    event EventHandler<string>? ComponentUnregistered;

    /// <summary>
    /// 当导航页面注册时触发的事件
    /// </summary>
    event EventHandler<NavigationPageRegistration>? NavigationPageRegistered;

    /// <summary>
    /// 当窗口注册时触发的事件
    /// </summary>
    event EventHandler<WindowRegistration>? WindowRegistered;
}
