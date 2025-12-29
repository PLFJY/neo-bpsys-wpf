using neo_bpsys_wpf.Core.Plugins.Commands;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.Settings;
using neo_bpsys_wpf.Core.Plugins.UI;

namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 插件宿主接口，为插件提供对宿主应用程序的访问
/// </summary>
public interface IPluginHost
{
    /// <summary>
    /// 插件管理器
    /// </summary>
    IPluginManager PluginManager { get; }

    /// <summary>
    /// UI扩展服务
    /// </summary>
    IUIExtensionService UIExtensionService { get; }

    /// <summary>
    /// 命令扩展服务
    /// </summary>
    ICommandExtensionService CommandExtensionService { get; }

    /// <summary>
    /// 事件总线
    /// </summary>
    IPluginEventBus EventBus { get; }

    /// <summary>
    /// 设置服务
    /// </summary>
    IPluginSettingsService SettingsService { get; }

    /// <summary>
    /// 服务提供者
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 获取指定类型的服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>服务实例</returns>
    T? GetService<T>() where T : class;

    /// <summary>
    /// 获取指定类型的服务（必须存在）
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>服务实例</returns>
    T GetRequiredService<T>() where T : notnull;

    /// <summary>
    /// 在UI线程上执行操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    void InvokeOnUIThread(Action action);

    /// <summary>
    /// 在UI线程上异步执行操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <returns>异步任务</returns>
    Task InvokeOnUIThreadAsync(Action action);

    /// <summary>
    /// 显示通知消息
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    /// <param name="severity">严重程度</param>
    void ShowNotification(string title, string message, NotificationSeverity severity = NotificationSeverity.Information);

    /// <summary>
    /// 显示对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    /// <param name="buttons">按钮类型</param>
    /// <returns>用户选择的结果</returns>
    Task<DialogResult> ShowDialogAsync(string title, string message, DialogButtons buttons = DialogButtons.Ok);

    /// <summary>
    /// 获取应用程序版本
    /// </summary>
    Version ApplicationVersion { get; }

    /// <summary>
    /// 获取应用程序名称
    /// </summary>
    string ApplicationName { get; }

    /// <summary>
    /// 获取用户数据目录
    /// </summary>
    string UserDataDirectory { get; }

    /// <summary>
    /// 获取插件目录
    /// </summary>
    string PluginDirectory { get; }
}

/// <summary>
/// 通知严重程度
/// </summary>
public enum NotificationSeverity
{
    /// <summary>
    /// 信息
    /// </summary>
    Information,

    /// <summary>
    /// 成功
    /// </summary>
    Success,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 错误
    /// </summary>
    Error
}

/// <summary>
/// 对话框按钮类型
/// </summary>
public enum DialogButtons
{
    /// <summary>
    /// 仅确定按钮
    /// </summary>
    Ok,

    /// <summary>
    /// 确定和取消按钮
    /// </summary>
    OkCancel,

    /// <summary>
    /// 是和否按钮
    /// </summary>
    YesNo,

    /// <summary>
    /// 是、否和取消按钮
    /// </summary>
    YesNoCancel
}

/// <summary>
/// 对话框结果
/// </summary>
public enum DialogResult
{
    /// <summary>
    /// 无结果
    /// </summary>
    None,

    /// <summary>
    /// 确定
    /// </summary>
    Ok,

    /// <summary>
    /// 取消
    /// </summary>
    Cancel,

    /// <summary>
    /// 是
    /// </summary>
    Yes,

    /// <summary>
    /// 否
    /// </summary>
    No
}
