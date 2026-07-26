using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Logging;

/// <summary>
/// 自定义 <see cref="ILoggerProvider"/>：参考 ContextMenuMgr 的 FrontendDebugLog 风格，
/// 以简单文件追加方式写入日志。每次应用启动创建带时间戳的新日志文件，并保留最近
/// <see cref="RetainedRunCount"/> 次运行的日志。
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    /// <summary>
    /// 保留的最近运行日志数量。
    /// </summary>
    private const int RetainedRunCount = 10;

    private static readonly object SyncRoot = new();
    private static string? s_currentFilePath;
    private static AppLogLevel s_currentLevel = AppLogLevel.Warning;
    private static bool s_initialized;

    /// <summary>
    /// 初始化 <see cref="FileLoggerProvider"/> 的新实例。
    /// </summary>
    /// <param name="logDirectory">日志文件输出目录。</param>
    /// <param name="initialLevel">初始日志级别。</param>
    public FileLoggerProvider(string logDirectory, AppLogLevel initialLevel)
    {
        s_currentLevel = initialLevel;
        EnsureInitialized(logDirectory);
    }

    /// <summary>
    /// 动态切换日志级别。
    /// </summary>
    /// <param name="level">要应用的应用日志级别。</param>
    public static void SetLevel(AppLogLevel level)
    {
        lock (SyncRoot)
        {
            s_currentLevel = level;
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private static void EnsureInitialized(string logDirectory)
    {
        lock (SyncRoot)
        {
            if (s_initialized)
            {
                return;
            }

            s_initialized = true;
            try
            {
                Directory.CreateDirectory(logDirectory);
                CleanupOldLogs(logDirectory);
                s_currentFilePath = Path.Combine(logDirectory, $"log-{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            }
            catch
            {
                // 初始化失败不应阻止应用启动
                s_currentFilePath = Path.Combine(logDirectory, "log.txt");
            }
        }
    }

    /// <summary>
    /// 清理旧的日志文件，只保留最近 <see cref="RetainedRunCount"/> 次运行的日志。
    /// </summary>
    /// <param name="logDirectory">日志目录路径。</param>
    private static void CleanupOldLogs(string logDirectory)
    {
        try
        {
            var staleLogs = Directory.GetFiles(logDirectory, "log-*.txt")
                .OrderByDescending(Path.GetFileName)
                .Skip(RetainedRunCount)
                .ToArray();
            foreach (var file in staleLogs)
            {
                try { File.Delete(file); }
                catch { /* 忽略单个文件删除失败 */ }
            }
        }
        catch
        {
            // 忽略清理失败
        }
    }

    /// <summary>
    /// 将一条日志写入当前日志文件。
    /// </summary>
    /// <param name="categoryName">日志类别名称（通常为类型全名）。</param>
    /// <param name="logLevel">Microsoft 日志级别。</param>
    /// <param name="message">格式化后的日志消息。</param>
    /// <param name="exception">关联异常（可为 null）。</param>
    internal static void Write(string categoryName, LogLevel logLevel, string message, Exception? exception)
    {
        AppLogLevel current;
        string? filePath;
        lock (SyncRoot)
        {
            current = s_currentLevel;
            filePath = s_currentFilePath;
        }

        if (!IsLevelEnabled(logLevel, current))
        {
            return;
        }

        var levelText = logLevel switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "FATAL",
            _ => logLevel.ToString().ToUpperInvariant()
        };

        var builder = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(" [").Append(levelText).Append("] ")
            .Append('[').Append(categoryName).Append("] ")
            .Append(message);
        if (exception is not null)
        {
            builder.AppendLine().Append(exception);
        }
        builder.AppendLine();

        try
        {
            lock (SyncRoot)
            {
                File.AppendAllText(filePath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // 写入失败不应影响业务流程
        }
    }

    private static bool IsLevelEnabled(LogLevel logLevel, AppLogLevel current)
        => ToAppLogLevel(logLevel) >= current;

    private static AppLogLevel ToAppLogLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => AppLogLevel.Verbose,
        LogLevel.Debug => AppLogLevel.Debug,
        LogLevel.Information => AppLogLevel.Information,
        LogLevel.Warning => AppLogLevel.Warning,
        LogLevel.Error => AppLogLevel.Error,
        LogLevel.Critical => AppLogLevel.Fatal,
        _ => AppLogLevel.Information
    };

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;

        public FileLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            lock (SyncRoot)
            {
                return IsLevelEnabled(logLevel, s_currentLevel);
            }
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter is null)
            {
                return;
            }

            var message = formatter(state, exception);
            Write(_categoryName, logLevel, message, exception);
        }
    }
}
