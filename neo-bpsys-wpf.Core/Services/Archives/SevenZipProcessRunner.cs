using System.Diagnostics;
using System.IO;
using System.Text;

namespace neo_bpsys_wpf.Core.Services.Archives;

/// <summary>
/// 封装官方 x64 7z.exe 进程的启动、stdout/stderr 异步读取、退出码处理和取消。
/// </summary>
internal sealed class SevenZipProcessRunner
{
    private const int ReadBufferSize = 4096;

    private readonly SevenZipToolLocator _toolLocator;
    private string? _cachedVersion;

    /// <summary>
    /// 创建 <see cref="SevenZipProcessRunner"/> 实例。
    /// </summary>
    /// <param name="toolLocator">7z.exe 定位器。</param>
    public SevenZipProcessRunner(SevenZipToolLocator toolLocator)
    {
        _toolLocator = toolLocator;
    }

    /// <summary>
    /// 7-Zip 进程运行结果。
    /// </summary>
    /// <param name="ExitCode">进程退出码。</param>
    /// <param name="StandardError">stderr 累积内容。</param>
    /// <param name="Version">7-Zip 版本字符串(若已缓存)。</param>
    public sealed record SevenZipProcessResult(int ExitCode, string StandardError, string? Version);

    /// <summary>
    /// 启动 7z.exe 并等待其退出。按字符块异步读取 stdout 解析进度,独立 Task 读取 stderr 防止管道死锁。
    /// 取消时 Kill 整个进程树。
    /// </summary>
    /// <param name="arguments">逐项添加的命令行参数(禁止拼接字符串)。</param>
    /// <param name="progress">进度上报器。null 时不报。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进程退出码、stderr 和版本信息。</returns>
    /// <exception cref="OperationCanceledException">取消时抛出。</exception>
    public async Task<SevenZipProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        IProgress<(int Percentage, string? CurrentFile)>? progress,
        CancellationToken cancellationToken)
    {
        var exePath = _toolLocator.GetExecutablePath();
        var workingDirectory = _toolLocator.GetToolDirectory();

        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        var stderrBuilder = new StringBuilder();
        var wasCanceled = false;
        var exitCode = 0;

        CancellationTokenRegistration cancellationRegistration = default;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // 进程可能已退出,忽略
                }
            });
        }

        try
        {
            process.Start();

            // stdout 按字符块异步读取,与 SevenZipProgressParser 配合跨块匹配
            var stdoutTask = ReadStdoutAsync(process.StandardOutput, progress, cancellationToken);

            // stderr 独立 Task 读取,避免管道死锁
            var stderrTask = ReadStderrAsync(process.StandardError, stderrBuilder, cancellationToken);

            // 等待进程退出
            await process.WaitForExitAsync(cancellationToken);

            // 进程已退出,确保 stdout/stderr 读取完成
            try
            {
                await stdoutTask;
            }
            catch (OperationCanceledException)
            {
                wasCanceled = true;
            }

            try
            {
                await stderrTask;
            }
            catch (OperationCanceledException)
            {
                wasCanceled = true;
            }

            exitCode = process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            wasCanceled = true;
            // 确保进程被 Kill
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
            catch
            {
                // 忽略
            }
        }
        finally
        {
            cancellationRegistration.Dispose();
            process.Dispose();
        }

        if (wasCanceled || cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        var version = await GetVersionAsync(cancellationToken);
        return new SevenZipProcessResult(exitCode, stderrBuilder.ToString(), version);
    }

    /// <summary>
    /// 获取 7-Zip 版本字符串(运行 7z.exe 无参数,解析首行)。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>版本字符串,失败时返回 null。</returns>
    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedVersion is not null)
        {
            return _cachedVersion;
        }

        var exePath = _toolLocator.GetExecutablePath();
        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            // 7z.exe 无参数时输出帮助信息,首行通常是 "7-Zip [64] 23.01 : Copyright (c) ..."
            var firstLine = await process.StandardOutput.ReadLineAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            _cachedVersion = firstLine;
            return firstLine;
        }
        catch
        {
            return null;
        }
    }

    private static async Task ReadStdoutAsync(
        StreamReader reader,
        IProgress<(int Percentage, string? CurrentFile)>? progress,
        CancellationToken cancellationToken)
    {
        var lastReported = 0;
        var remainingBuffer = string.Empty;
        var buffer = new char[ReadBufferSize];

        while (true)
        {
            int read;
            try
            {
                read = await reader.ReadAsync(buffer, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            if (read <= 0)
            {
                break;
            }

            var chunk = remainingBuffer + new string(buffer, 0, read);
            var (newPercentages, newRemaining) = SevenZipProgressParser.Parse(chunk, ref lastReported);

            foreach (var percentage in newPercentages)
            {
                progress?.Report((percentage, null));
            }

            remainingBuffer = newRemaining;
        }

        // 进度结束时若未报 100,补报 100
        if (progress is not null && lastReported < 100)
        {
            progress.Report((100, null));
        }
    }

    private static async Task ReadStderrAsync(
        StreamReader reader,
        StringBuilder builder,
        CancellationToken cancellationToken)
    {
        var buffer = new char[ReadBufferSize];
        while (true)
        {
            int read;
            try
            {
                read = await reader.ReadAsync(buffer, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            if (read <= 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);
        }
    }
}
