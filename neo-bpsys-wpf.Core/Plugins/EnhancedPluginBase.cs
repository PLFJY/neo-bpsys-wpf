using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Plugins.Commands;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.Settings;
using neo_bpsys_wpf.Core.Plugins.UI;

namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 增强型插件基类，提供对宿主服务的便捷访问
/// </summary>
public abstract class EnhancedPluginBase : PluginBase
{
    private IPluginHost? _host;

    /// <summary>
    /// 插件宿主
    /// </summary>
    protected IPluginHost Host => _host ?? throw new InvalidOperationException("Plugin has not been initialized.");

    /// <summary>
    /// UI扩展服务
    /// </summary>
    protected IUIExtensionService UIExtension => Host.UIExtensionService;

    /// <summary>
    /// 命令扩展服务
    /// </summary>
    protected ICommandExtensionService Commands => Host.CommandExtensionService;

    /// <summary>
    /// 事件总线
    /// </summary>
    protected IPluginEventBus EventBus => Host.EventBus;

    /// <summary>
    /// 设置服务
    /// </summary>
    protected IPluginSettingsService Settings => Host.SettingsService;

    /// <summary>
    /// 订阅令牌集合，用于自动清理
    /// </summary>
    private readonly List<IDisposable> _subscriptions = new();

    /// <inheritdoc/>
    public override Task InitializeAsync(IServiceProvider serviceProvider)
    {
        _host = serviceProvider.GetRequiredService<IPluginHost>();
        return base.InitializeAsync(serviceProvider);
    }

    /// <inheritdoc/>
    public override async Task UnloadAsync()
    {
        // 自动清理事件订阅
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();

        // 清理插件注册的所有UI组件
        if (_host?.UIExtensionService is UIExtensionService uiService)
        {
            uiService.UnregisterAllByPluginId(Id);
        }

        // 清理插件注册的所有命令
        if (_host?.CommandExtensionService is CommandExtensionService cmdService)
        {
            cmdService.UnregisterAllByPluginId(Id);
        }

        // 清理事件订阅
        EventBus.UnsubscribeAll(Id);

        _host = null;

        await base.UnloadAsync();
    }

    /// <summary>
    /// 订阅事件（自动在插件卸载时取消订阅）
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理器</param>
    protected void Subscribe<TEvent>(PluginEventHandler<TEvent> handler) where TEvent : PluginEvent
    {
        var subscription = EventBus.Subscribe(Id, handler);
        _subscriptions.Add(subscription);
    }

    /// <summary>
    /// 注册UI组件
    /// </summary>
    /// <param name="registration">组件注册信息</param>
    protected void RegisterComponent(UIComponentRegistration registration)
    {
        registration.PluginId = Id;
        UIExtension.RegisterComponent(registration);
    }

    /// <summary>
    /// 注册导航页面
    /// </summary>
    /// <param name="registration">页面注册信息</param>
    protected void RegisterNavigationPage(NavigationPageRegistration registration)
    {
        registration.PluginId = Id;
        UIExtension.RegisterNavigationPage(registration);
    }

    /// <summary>
    /// 注册窗口
    /// </summary>
    /// <param name="registration">窗口注册信息</param>
    protected void RegisterWindow(WindowRegistration registration)
    {
        registration.PluginId = Id;
        UIExtension.RegisterWindow(registration);
    }

    /// <summary>
    /// 注册命令
    /// </summary>
    /// <param name="registration">命令注册信息</param>
    protected void RegisterCommand(CommandRegistration registration)
    {
        registration.PluginId = Id;
        Commands.RegisterCommand(registration);
    }

    /// <summary>
    /// 获取插件设置
    /// </summary>
    /// <typeparam name="T">设置类型</typeparam>
    /// <returns>设置实例</returns>
    protected T GetPluginSettings<T>() where T : PluginSettingsBase, new()
    {
        return Settings.GetSettings<T>(Id);
    }

    /// <summary>
    /// 保存插件设置
    /// </summary>
    /// <typeparam name="T">设置类型</typeparam>
    /// <param name="settings">设置实例</param>
    protected void SavePluginSettings<T>(T settings) where T : PluginSettingsBase
    {
        Settings.SaveSettings(Id, settings);
    }

    /// <summary>
    /// 在UI线程上执行操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    protected void InvokeOnUI(Action action)
    {
        Host.InvokeOnUIThread(action);
    }

    /// <summary>
    /// 在UI线程上异步执行操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <returns>异步任务</returns>
    protected Task InvokeOnUIAsync(Action action)
    {
        return Host.InvokeOnUIThreadAsync(action);
    }

    /// <summary>
    /// 显示通知
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息</param>
    /// <param name="severity">严重程度</param>
    protected void ShowNotification(string title, string message, NotificationSeverity severity = NotificationSeverity.Information)
    {
        Host.ShowNotification(title, message, severity);
    }

    /// <summary>
    /// 显示对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息</param>
    /// <param name="buttons">按钮</param>
    /// <returns>对话框结果</returns>
    protected Task<DialogResult> ShowDialogAsync(string title, string message, DialogButtons buttons = DialogButtons.Ok)
    {
        return Host.ShowDialogAsync(title, message, buttons);
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件实例</param>
    protected Task PublishEventAsync<TEvent>(TEvent @event) where TEvent : PluginEvent
    {
        @event.SourcePluginId = Id;
        return EventBus.PublishAsync(@event);
    }

    /// <summary>
    /// 同步发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件实例</param>
    protected void PublishEvent<TEvent>(TEvent @event) where TEvent : PluginEvent
    {
        @event.SourcePluginId = Id;
        EventBus.Publish(@event);
    }
}
