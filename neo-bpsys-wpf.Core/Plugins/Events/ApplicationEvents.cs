namespace neo_bpsys_wpf.Core.Plugins.Events;

/// <summary>
/// 应用程序事件基类
/// </summary>
public abstract class ApplicationEvent : PluginEventBase;

/// <summary>
/// 应用程序启动完成事件
/// </summary>
public sealed class ApplicationStartedEvent : ApplicationEvent;

/// <summary>
/// 应用程序关闭中事件
/// </summary>
public sealed class ApplicationShuttingDownEvent : ApplicationEvent;

/// <summary>
/// 主题变更事件
/// </summary>
public sealed class ThemeChangedEvent : ApplicationEvent
{
    /// <summary>
    /// 新主题名称
    /// </summary>
    public required string NewTheme { get; init; }

    /// <summary>
    /// 旧主题名称
    /// </summary>
    public string? OldTheme { get; init; }
}

/// <summary>
/// 语言变更事件
/// </summary>
public sealed class LanguageChangedEvent : ApplicationEvent
{
    /// <summary>
    /// 新语言代码
    /// </summary>
    public required string NewLanguage { get; init; }

    /// <summary>
    /// 旧语言代码
    /// </summary>
    public string? OldLanguage { get; init; }
}

/// <summary>
/// 导航事件
/// </summary>
public sealed class NavigationEvent : ApplicationEvent
{
    /// <summary>
    /// 导航目标页面类型
    /// </summary>
    public required Type TargetPageType { get; init; }

    /// <summary>
    /// 导航参数
    /// </summary>
    public object? Parameter { get; init; }
}
