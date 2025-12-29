namespace neo_bpsys_wpf.Core.Plugins.Services;

/// <summary>
/// 插件日志服务接口
/// </summary>
public interface IPluginLogger
{
    /// <summary>
    /// 记录调试日志
    /// </summary>
    void Debug(string message);

    /// <summary>
    /// 记录调试日志（带参数）
    /// </summary>
    void Debug(string message, params object[] args);

    /// <summary>
    /// 记录信息日志
    /// </summary>
    void Info(string message);

    /// <summary>
    /// 记录信息日志（带参数）
    /// </summary>
    void Info(string message, params object[] args);

    /// <summary>
    /// 记录警告日志
    /// </summary>
    void Warning(string message);

    /// <summary>
    /// 记录警告日志（带参数）
    /// </summary>
    void Warning(string message, params object[] args);

    /// <summary>
    /// 记录错误日志
    /// </summary>
    void Error(string message);

    /// <summary>
    /// 记录错误日志（带异常）
    /// </summary>
    void Error(string message, Exception exception);

    /// <summary>
    /// 记录错误日志（带参数）
    /// </summary>
    void Error(string message, params object[] args);

    /// <summary>
    /// 记录致命错误日志
    /// </summary>
    void Fatal(string message);

    /// <summary>
    /// 记录致命错误日志（带异常）
    /// </summary>
    void Fatal(string message, Exception exception);
}

/// <summary>
/// 插件日志工厂接口
/// </summary>
public interface IPluginLoggerFactory
{
    /// <summary>
    /// 创建插件日志记录器
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>日志记录器</returns>
    IPluginLogger CreateLogger(string pluginId);

    /// <summary>
    /// 创建插件日志记录器
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="pluginId">插件ID</param>
    /// <returns>日志记录器</returns>
    IPluginLogger CreateLogger<T>(string pluginId);
}
