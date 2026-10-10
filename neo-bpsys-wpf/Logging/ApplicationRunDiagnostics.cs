using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;

namespace neo_bpsys_wpf.Logging;

/// <summary>本地运行状态和全局异常诊断，不依赖 Host 的生命周期。</summary>
internal sealed class ApplicationRunDiagnostics
{
    private readonly object _gate = new();
    private readonly string _statePath;
    private RunState? _run;
    private bool _fatalExceptionSeen;

    /// <summary>创建指定目录的运行诊断。</summary>
    /// <param name="logDirectory">现有文件日志目录。</param>
    internal ApplicationRunDiagnostics(string logDirectory) => _statePath = Path.Combine(logDirectory, "run-state.json");

    /// <summary>读取上次运行并记录本次启动；仅在取得单实例和日志所有权后调用。</summary>
    internal void Start()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_statePath))
                {
                    var previous = JsonSerializer.Deserialize<RunState>(File.ReadAllText(_statePath));
                    if (previous is not null && previous.State != "ShutdownCompleted")
                    {
                        Write(LogLevel.Warning, $"Unclean shutdown detected. Previous application run did not complete a clean shutdown. RunId={previous.RunId} PID={previous.Pid} StartedAt={previous.StartedAt:O} Version={previous.Version} LastKnownState={previous.State}");
                    }
                }
            }
            catch (Exception ex)
            {
                Write(LogLevel.Warning, "Previous run state could not be read; shutdown outcome is unknown.", ex);
            }

            _run = new RunState(Guid.NewGuid(), DateTimeOffset.Now, Environment.ProcessId, AppConstants.AppVersion, "Running", DateTimeOffset.Now);
            SaveState();
            Write(LogLevel.Information, $"Application run started. RunId={_run.RunId} StartedAt={_run.StartedAt:O} Version={_run.Version}");
        }
    }

    /// <summary>记录关闭开始，不将其视为关闭完成。</summary>
    internal void ShutdownRequested() => SetState("ShutdownRequested");

    /// <summary>仅在全部关闭工作成功且未见致命异常时标记完成。</summary>
    /// <returns>本次运行是否可以标记为正常完成。</returns>
    internal bool ShutdownCompleted()
    {
        lock (_gate)
        {
            if (_fatalExceptionSeen || _run is null) return false;
            SetState("ShutdownCompleted");
            return true;
        }
    }

    /// <summary>同步记录 Dispatcher 未处理异常，不改变 Handled。</summary>
    /// <param name="exception">完整异常。</param>
    internal void DispatcherException(Exception exception) => Fatal(exception, "Unhandled WPF Dispatcher exception");

    /// <summary>同步记录进程级托管异常。</summary>
    /// <param name="args">异常和终止信息。</param>
    internal void DomainException(UnhandledExceptionEventArgs args)
    {
        Fatal(args.ExceptionObject as Exception,
            $"Unhandled AppDomain exception. IsTerminating={args.IsTerminating} ExceptionType={args.ExceptionObject?.GetType().FullName}");
    }

    /// <summary>记录未观察 Task 异常，不调用 SetObserved 或改变异常策略。</summary>
    /// <param name="args">含全部内部异常的事件。</param>
    internal void UnobservedTaskException(UnobservedTaskExceptionEventArgs args)
        => Write(LogLevel.Warning, $"Unobserved Task exception (not a fatal process exception). Observed={args.Observed}", args.Exception);

    private void Fatal(Exception? exception, string message)
    {
        lock (_gate) _fatalExceptionSeen = true;
        Write(LogLevel.Critical, message, exception);
    }

    private void SetState(string state)
    {
        lock (_gate)
        {
            if (_run is null) return;
            _run = _run with { State = state, UpdatedAt = DateTimeOffset.Now };
            SaveState();
            Write(LogLevel.Information, $"Application shutdown state changed. State={state}");
        }
    }

    private void SaveState()
    {
        try
        {
            // 同目录临时文件 + 原子替换，断电/强杀不应留下半份 JSON。
            var temporaryPath = _statePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_run));
            File.Move(temporaryPath, _statePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Write(LogLevel.Warning, "Application run state could not be persisted; shutdown detection may be unavailable.", ex);
        }
    }

    private void Write(LogLevel level, string message, Exception? exception = null)
        => FileLoggerProvider.WriteDiagnostic("ApplicationRunDiagnostics", level, $"RunId={_run?.RunId.ToString() ?? "not-started"} {message}", exception);

    /// <summary>只保存诊断元数据，不保存用户配置或布局。</summary>
    /// <param name="RunId">运行身份。</param>
    /// <param name="StartedAt">含时区的启动时间。</param>
    /// <param name="Pid">进程 ID。</param>
    /// <param name="Version">应用版本。</param>
    /// <param name="State">最后完成的状态转换。</param>
    /// <param name="UpdatedAt">含时区的状态写入时间。</param>
    internal sealed record RunState(Guid RunId, DateTimeOffset StartedAt, int Pid, string Version, string State, DateTimeOffset UpdatedAt);
}
