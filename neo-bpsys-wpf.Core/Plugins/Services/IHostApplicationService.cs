namespace neo_bpsys_wpf.Core.Plugins.Services;

/// <summary>
/// 宿主应用程序服务接口，提供插件访问宿主功能的入口
/// </summary>
public interface IHostApplicationService
{
    /// <summary>
    /// 获取宿主应用程序版本
    /// </summary>
    Version HostVersion { get; }

    /// <summary>
    /// 获取宿主应用程序名称
    /// </summary>
    string HostName { get; }

    /// <summary>
    /// 获取应用程序数据目录
    /// </summary>
    string AppDataDirectory { get; }

    /// <summary>
    /// 获取插件根目录
    /// </summary>
    string PluginsDirectory { get; }

    /// <summary>
    /// 获取当前主题
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// 获取当前语言
    /// </summary>
    string CurrentLanguage { get; }

    /// <summary>
    /// 显示消息框
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="title">标题</param>
    /// <param name="buttons">按钮类型</param>
    /// <returns>用户选择的结果</returns>
    Task<MessageBoxResult> ShowMessageBoxAsync(string message, string title, MessageBoxButtons buttons = MessageBoxButtons.OK);

    /// <summary>
    /// 显示通知
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    /// <param name="type">通知类型</param>
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Information);

    /// <summary>
    /// 导航到指定页面
    /// </summary>
    /// <param name="pageType">页面类型</param>
    /// <param name="parameter">导航参数</param>
    bool Navigate(Type pageType, object? parameter = null);

    /// <summary>
    /// 在UI线程执行操作
    /// </summary>
    /// <param name="action">操作</param>
    void InvokeOnUIThread(Action action);

    /// <summary>
    /// 在UI线程异步执行操作
    /// </summary>
    /// <param name="action">操作</param>
    Task InvokeOnUIThreadAsync(Action action);
}

/// <summary>
/// 消息框按钮类型
/// </summary>
public enum MessageBoxButtons
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel
}

/// <summary>
/// 消息框结果
/// </summary>
public enum MessageBoxResult
{
    None,
    OK,
    Cancel,
    Yes,
    No
}

/// <summary>
/// 通知类型
/// </summary>
public enum NotificationType
{
    Information,
    Success,
    Warning,
    Error
}
