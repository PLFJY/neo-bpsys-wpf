using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Logging;

/// <summary>
/// 自定义 <see cref="ILoggerProvider"/>：以简单文件追加方式写入日志。
/// 当前运行日志始终写入 <c>latest.txt</c>，并在文件开头记录本次启动时间。
/// 应用正常退出时调用 <see cref="FinalizeRun"/> 将 <c>latest.txt</c> 按启动时间归档为
/// <c>log-{yyyyMMdd_HHmmss}.txt</c>；若上次运行因故障未正常退出，<c>latest.txt</c> 会保留，
/// 下次启动时读取其头部记录的启动时间并完成归档。仅保留最近
/// <see cref="RetainedRunCount"/> 次运行的归档日志。
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    /// <summary>
    /// 保留的最近运行日志数量。
    /// </summary>
    private const int RetainedRunCount = 10;

    /// <summary>
    /// 当前运行日志文件名。
    /// </summary>
    private const string LatestFileName = "latest.txt";

    /// <summary>
    /// 归档日志文件名前缀。
    /// </summary>
    private const string ArchivePrefix = "log-";

    /// <summary>
    /// 启动时间头部标记的正则表达式，用于解析崩溃残留的 <c>latest.txt</c>。
    /// </summary>
    private static readonly Regex RunHeaderRegex = new(
        @"==== Run started:\s*(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\s*====",
        RegexOptions.Compiled);

    private static readonly object SyncRoot = new();
    private static string? s_currentFilePath;
    private static DateTime s_currentRunStartTime;
    private static AppLogLevel s_currentLevel = AppLogLevel.Warning;
    private static bool s_initialized;
    private static bool s_finalized;

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

    /// <summary>
    /// 归档当前运行日志。在应用正常退出时调用：将 <c>latest.txt</c> 按本次启动时间
    /// 重命名为 <c>log-{yyyyMMdd_HHmmss}.txt</c>，并清理超出保留数量的旧日志。
    /// 若 <c>latest.txt</c> 不存在、已归档或未初始化，则不执行任何操作。
    /// </summary>
    public static void FinalizeRun()
    {
        lock (SyncRoot)
        {
            if (s_finalized || s_currentFilePath is null)
            {
                return;
            }

            s_finalized = true;
            try
            {
                if (!File.Exists(s_currentFilePath))
                {
                    return;
                }

                var directory = Path.GetDirectoryName(s_currentFilePath);
                if (string.IsNullOrEmpty(directory))
                {
                    return;
                }

                var archivePath = BuildArchivePath(directory, s_currentRunStartTime);
                if (!string.Equals(archivePath, s_currentFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(s_currentFilePath, archivePath);
                }

                CleanupOldLogs(directory);
            }
            catch
            {
                // 归档失败不应阻止应用退出
            }
        }
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
            s_currentRunStartTime = DateTime.Now;
            try
            {
                Directory.CreateDirectory(logDirectory);
                ArchivePreviousLatestLog(logDirectory);

                s_currentFilePath = Path.Combine(logDirectory, LatestFileName);
                WriteRunHeader(s_currentFilePath, s_currentRunStartTime);
            }
            catch
            {
                // 初始化失败不应阻止应用启动
                s_currentFilePath = Path.Combine(logDirectory, LatestFileName);
            }
        }
    }

    /// <summary>
    /// 若存在上一次运行残留的 <c>latest.txt</c>（通常因故障未正常归档），
    /// 读取其头部记录的启动时间并重命名为 <c>log-{yyyyMMdd_HHmmss}.txt</c>，
    /// 随后清理超出保留数量的旧日志。读取不到启动时间时回退到文件最后写入时间。
    /// </summary>
    /// <param name="logDirectory">日志目录路径。</param>
    private static void ArchivePreviousLatestLog(string logDirectory)
    {
        var latestPath = Path.Combine(logDirectory, LatestFileName);
        if (!File.Exists(latestPath))
        {
            return;
        }

        try
        {
            var runStart = TryReadRunHeader(latestPath) ?? File.GetLastWriteTime(latestPath);
            var archivePath = BuildArchivePath(logDirectory, runStart);
            if (!string.Equals(archivePath, latestPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(latestPath, archivePath);
            }

            CleanupOldLogs(logDirectory);
        }
        catch
        {
            // 旧日志归档失败时不应阻塞新运行；后续 WriteRunHeader 会重置 latest.txt
        }
    }

    /// <summary>
    /// 在 <c>latest.txt</c> 开头写入本次运行的启动时间标记，供故障后下次启动归档时读取。
    /// </summary>
    /// <param name="filePath">日志文件路径。</param>
    /// <param name="runStart">本次运行启动时间。</param>
    private static void WriteRunHeader(string filePath, DateTime runStart)
    {
        var header = $"==== Run started: {runStart:yyyy-MM-dd HH:mm:ss} ===={Environment.NewLine}";
        File.WriteAllText(filePath, header, Encoding.UTF8);
    }

    /// <summary>
    /// 尝试从日志文件头部读取运行启动时间标记。
    /// </summary>
    /// <param name="filePath">日志文件路径。</param>
    /// <returns>读取到的启动时间；读取失败返回 <c>null</c>。</returns>
    private static DateTime? TryReadRunHeader(string filePath)
    {
        try
        {
            string? headerLine = null;
            using (var reader = new StreamReader(filePath, Encoding.UTF8))
            {
                for (var i = 0; i < 5 && !reader.EndOfStream; i++)
                {
                    var line = reader.ReadLine();
                    if (line is null)
                    {
                        break;
                    }

                    if (line.StartsWith("==== Run started:", StringComparison.Ordinal))
                    {
                        headerLine = line;
                        break;
                    }
                }
            }

            if (headerLine is null)
            {
                return null;
            }

            var match = RunHeaderRegex.Match(headerLine);
            if (!match.Success)
            {
                return null;
            }

            return DateTime.TryParseExact(
                match.Groups[1].Value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? parsed
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 根据启动时间构造归档文件路径，若目标已存在则追加数字后缀以避免覆盖。
    /// </summary>
    /// <param name="logDirectory">日志目录路径。</param>
    /// <param name="runStart">运行启动时间。</param>
    /// <returns>归档文件完整路径。</returns>
    private static string BuildArchivePath(string logDirectory, DateTime runStart)
    {
        var baseName = $"{ArchivePrefix}{runStart:yyyyMMdd_HHmmss}.txt";
        var path = Path.Combine(logDirectory, baseName);
        var counter = 1;
        while (File.Exists(path))
        {
            baseName = $"{ArchivePrefix}{runStart:yyyyMMdd_HHmmss}_{counter}.txt";
            path = Path.Combine(logDirectory, baseName);
            counter++;
        }

        return path;
    }

    /// <summary>
    /// 清理旧的日志文件，只保留最近 <see cref="RetainedRunCount"/> 次运行的归档日志。
    /// </summary>
    /// <param name="logDirectory">日志目录路径。</param>
    private static void CleanupOldLogs(string logDirectory)
    {
        try
        {
            var staleLogs = Directory.GetFiles(logDirectory, ArchivePrefix + "*.txt")
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
        lock (SyncRoot)
        {
            current = s_currentLevel;
            if (s_finalized || s_currentFilePath is null)
            {
                return;
            }
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
                if (s_finalized || s_currentFilePath is null)
                {
                    return;
                }

                File.AppendAllText(s_currentFilePath, builder.ToString(), Encoding.UTF8);
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
                return !s_finalized && s_currentFilePath is not null && IsLevelEnabled(logLevel, s_currentLevel);
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
