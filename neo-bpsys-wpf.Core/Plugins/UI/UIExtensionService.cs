using System.Collections.Concurrent;
using System.Windows;

namespace neo_bpsys_wpf.Core.Plugins.UI;

/// <summary>
/// UI扩展服务实现
/// </summary>
public sealed class UIExtensionService : IUIExtensionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, UIComponentRegistration> _components = new();
    private readonly ConcurrentDictionary<string, NavigationPageRegistration> _navigationPages = new();
    private readonly ConcurrentDictionary<string, WindowRegistration> _windows = new();
    private readonly ConcurrentDictionary<string, Window> _singletonWindows = new();

    /// <inheritdoc/>
    public event EventHandler<UIComponentRegistration>? ComponentRegistered;

    /// <inheritdoc/>
    public event EventHandler<string>? ComponentUnregistered;

    /// <inheritdoc/>
    public event EventHandler<NavigationPageRegistration>? NavigationPageRegistered;

    /// <inheritdoc/>
    public event EventHandler<WindowRegistration>? WindowRegistered;

    /// <summary>
    /// 创建UI扩展服务
    /// </summary>
    public UIExtensionService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public void RegisterComponent(UIComponentRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (string.IsNullOrWhiteSpace(registration.Id))
        {
            throw new ArgumentException("Component ID cannot be null or empty.", nameof(registration));
        }

        // 如果组件已注册，先卸载旧的（幂等性处理）
        if (_components.ContainsKey(registration.Id))
        {
            UnregisterComponent(registration.Id);
        }

        _components[registration.Id] = registration;
        ComponentRegistered?.Invoke(this, registration);
    }

    /// <inheritdoc/>
    public void UnregisterComponent(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
        {
            throw new ArgumentException("Component ID cannot be null or empty.", nameof(componentId));
        }

        if (_components.TryRemove(componentId, out _))
        {
            ComponentUnregistered?.Invoke(this, componentId);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<UIComponentRegistration> GetComponents(UIExtensionPoint extensionPoint)
    {
        return _components.Values
            .Where(c => c.ExtensionPoint == extensionPoint)
            .OrderBy(c => c.Priority)
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<UIComponentRegistration> GetComponents(string customExtensionPointName)
    {
        if (string.IsNullOrWhiteSpace(customExtensionPointName))
        {
            return Array.Empty<UIComponentRegistration>();
        }

        return _components.Values
            .Where(c => c.ExtensionPoint == UIExtensionPoint.Custom &&
                       c.CustomExtensionPointName == customExtensionPointName)
            .OrderBy(c => c.Priority)
            .ToList();
    }

    /// <inheritdoc/>
    public FrameworkElement? CreateComponent(string componentId)
    {
        if (!_components.TryGetValue(componentId, out var registration))
        {
            return null;
        }

        try
        {
            return registration.ComponentFactory(_serviceProvider);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void RegisterNavigationPage(NavigationPageRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (string.IsNullOrWhiteSpace(registration.Id))
        {
            throw new ArgumentException("Page ID cannot be null or empty.", nameof(registration));
        }

        // 如果页面已注册，先卸载旧的（幂等性处理）
        if (_navigationPages.ContainsKey(registration.Id))
        {
            UnregisterNavigationPage(registration.Id);
        }

        _navigationPages[registration.Id] = registration;
        NavigationPageRegistered?.Invoke(this, registration);
    }

    /// <inheritdoc/>
    public void UnregisterNavigationPage(string pageId)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            throw new ArgumentException("Page ID cannot be null or empty.", nameof(pageId));
        }

        _navigationPages.TryRemove(pageId, out _);
    }

    /// <inheritdoc/>
    public IReadOnlyList<NavigationPageRegistration> GetNavigationPages()
    {
        return _navigationPages.Values
            .Where(p => p.ShowInNavigation)
            .OrderBy(p => p.Priority)
            .ToList();
    }

    /// <inheritdoc/>
    public void RegisterWindow(WindowRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (string.IsNullOrWhiteSpace(registration.Id))
        {
            throw new ArgumentException("Window ID cannot be null or empty.", nameof(registration));
        }

        // 如果窗口已注册，先卸载旧的（幂等性处理）
        if (_windows.ContainsKey(registration.Id))
        {
            UnregisterWindow(registration.Id);
        }

        _windows[registration.Id] = registration;
        WindowRegistered?.Invoke(this, registration);
    }

    /// <inheritdoc/>
    public void UnregisterWindow(string windowId)
    {
        if (string.IsNullOrWhiteSpace(windowId))
        {
            throw new ArgumentException("Window ID cannot be null or empty.", nameof(windowId));
        }

        _windows.TryRemove(windowId, out _);
        _singletonWindows.TryRemove(windowId, out _);
    }

    /// <inheritdoc/>
    public IReadOnlyList<WindowRegistration> GetWindows()
    {
        return _windows.Values.ToList();
    }

    /// <inheritdoc/>
    public Window? ShowWindow(string windowId)
    {
        if (!_windows.TryGetValue(windowId, out var registration))
        {
            return null;
        }

        Window? window = null;

        if (registration.IsSingleton)
        {
            if (_singletonWindows.TryGetValue(windowId, out var existingWindow))
            {
                if (existingWindow.IsLoaded)
                {
                    existingWindow.Activate();
                    return existingWindow;
                }
                else
                {
                    _singletonWindows.TryRemove(windowId, out _);
                }
            }
        }

        try
        {
            if (registration.WindowFactory != null)
            {
                window = registration.WindowFactory(_serviceProvider);
            }
            else
            {
                window = (Window?)Activator.CreateInstance(registration.WindowType);

                if (window != null && registration.ViewModelType != null)
                {
                    var viewModel = Activator.CreateInstance(registration.ViewModelType);
                    window.DataContext = viewModel;
                }
            }

            if (window == null)
            {
                return null;
            }

            if (registration.IsSingleton)
            {
                _singletonWindows[windowId] = window;
                window.Closed += (_, _) => _singletonWindows.TryRemove(windowId, out _);
            }

            window.Show();
            return window;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 按插件ID取消注册所有组件
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    public void UnregisterAllByPluginId(string pluginId)
    {
        // 取消注册组件
        var componentsToRemove = _components.Values
            .Where(c => c.PluginId == pluginId)
            .Select(c => c.Id)
            .ToList();

        foreach (var id in componentsToRemove)
        {
            UnregisterComponent(id);
        }

        // 取消注册导航页面
        var pagesToRemove = _navigationPages.Values
            .Where(p => p.PluginId == pluginId)
            .Select(p => p.Id)
            .ToList();

        foreach (var id in pagesToRemove)
        {
            UnregisterNavigationPage(id);
        }

        // 取消注册窗口
        var windowsToRemove = _windows.Values
            .Where(w => w.PluginId == pluginId)
            .Select(w => w.Id)
            .ToList();

        foreach (var id in windowsToRemove)
        {
            UnregisterWindow(id);
        }
    }
}
