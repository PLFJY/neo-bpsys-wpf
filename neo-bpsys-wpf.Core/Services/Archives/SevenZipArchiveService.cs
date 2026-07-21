using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Exceptions;
using neo_bpsys_wpf.Core.Models.Archives;

namespace neo_bpsys_wpf.Core.Services.Archives;

/// <summary>
/// 基于官方 x64 7z.exe 的运行时归档服务,支持 ZIP 和 7z 包。
/// 所有进程调用通过 <see cref="SevenZipProcessRunner"/> 启动,支持 CancellationToken 和原生进度汇报。
/// </summary>
public sealed class SevenZipArchiveService : IArchiveService
{
    private readonly SevenZipProcessRunner _runner;
    private readonly SevenZipToolLocator _toolLocator;

    /// <summary>
    /// 创建 <see cref="SevenZipArchiveService"/> 实例,使用默认的 <see cref="SevenZipToolLocator"/>。
    /// </summary>
    public SevenZipArchiveService()
        : this(new SevenZipToolLocator())
    {
    }

    /// <summary>
    /// 创建 <see cref="SevenZipArchiveService"/> 实例。
    /// </summary>
    /// <param name="toolLocator">7z.exe 定位器。</param>
    public SevenZipArchiveService(SevenZipToolLocator toolLocator)
    {
        _toolLocator = toolLocator;
        _runner = new SevenZipProcessRunner(toolLocator);
    }

    /// <inheritdoc />
    public async Task<ArchiveFormat> DetectFormatAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Archive file was not found.", archivePath);
        }

        var (stdout, exitCode, stderr, version) = await RunListAsync(archivePath, cancellationToken);
        if (exitCode != 0 && exitCode != 1)
        {
            ThrowListFailure(stdout, exitCode, stderr, version, archivePath);
        }

        return ParseArchiveFormat(stdout, archivePath);
    }

    /// <inheritdoc />
    public Task<ArchiveFormat> ExtractToDirectoryAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        return ExtractToDirectoryAsync(archivePath, destinationDirectory, progress: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ArchiveFormat> ExtractToDirectoryAsync(
        string archivePath,
        string destinationDirectory,
        IProgress<ArchiveProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Archive file was not found.", archivePath);
        }

        // 先通过列表操作探测格式并验证归档可读
        var (listOutput, listExitCode, listStderr, version) = await RunListAsync(archivePath, cancellationToken);
        if (listExitCode != 0 && listExitCode != 1)
        {
            ThrowListFailure(listOutput, listExitCode, listStderr, version, archivePath);
        }

        var format = ParseArchiveFormat(listOutput, archivePath);

        // 解压前安全列表检查
        var normalizedDestination = NormalizeDirectoryPath(destinationDirectory);
        ValidateArchiveEntries(listOutput, normalizedDestination);

        Directory.CreateDirectory(destinationDirectory);

        // 解压:x 解压到目录, -y 全部 yes, -aoa 覆盖, -bso0 静默 stdout, -bse2 stderr 到 stderr,
        // -bsp1 进度到 stdout, -bb0 无详细输出, -sccUTF-8 控制台编码
        var arguments = new List<string>
        {
            "x",
            archivePath,
            $"-o{destinationDirectory}",
            "-y",
            "-aoa",
            "-bso0",
            "-bse2",
            "-bsp1",
            "-bb0",
            "-sccUTF-8",
        };

        var progressAdapter = progress is null
            ? null
            : new Progress<(int Percentage, string? CurrentFile)>(p =>
                progress.Report(new ArchiveProgress(p.Percentage, p.CurrentFile)));

        SevenZipProcessRunner.SevenZipProcessResult result;
        try
        {
            result = await _runner.RunAsync(arguments, progressAdapter, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 取消时清理本次创建的解压目录(仅当目录原本不存在或为空时清理)
            TryCleanupOnFailure(destinationDirectory);
            throw;
        }

        // 退出码处理
        if (result.ExitCode != 0 && result.ExitCode != 1)
        {
            TryCleanupOnFailure(destinationDirectory);
            var message = result.ExitCode switch
            {
                2 => $"7-Zip fatal error during extraction (exit code {result.ExitCode}).",
                7 => $"7-Zip command line error (exit code {result.ExitCode}).",
                8 => $"7-Zip ran out of memory (exit code {result.ExitCode}).",
                255 => $"7-Zip was stopped (exit code {result.ExitCode}).",
                _ => $"7-Zip extraction failed with unknown exit code {result.ExitCode}.",
            };
            throw new SevenZipException(
                message,
                operation: "extract",
                exitCode: result.ExitCode,
                archivePath: archivePath,
                destinationPath: destinationDirectory,
                standardError: result.StandardError,
                sevenZipVersion: result.Version,
                toolPath: _toolLocator.GetExecutablePath());
        }

        // 解压完成后扫描生成树,拒绝任何 reparse point(符号链接/junction)
        ValidateNoReparsePoints(destinationDirectory);

        return format;
    }

    /// <summary>
    /// 运行 7z l -slt -sccUTF-8 列出归档内容,返回 (stdout, exitCode, stderr, version)。
    /// 不使用 -ba:7-Zip 26.02 在 -ba 模式下会抑制归档级头部(包含 Type = 字段),导致无法探测格式。
    /// </summary>
    private async Task<(string Stdout, int ExitCode, string Stderr, string? Version)> RunListAsync(
        string archivePath, CancellationToken cancellationToken)
    {
        var exePath = _toolLocator.GetExecutablePath();
        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _toolLocator.GetToolDirectory(),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        startInfo.ArgumentList.Add("l");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add("-slt");
        startInfo.ArgumentList.Add("-sccUTF-8");

        var process = new Process { StartInfo = startInfo };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        CancellationTokenRegistration registration = default;
        if (cancellationToken.CanBeCanceled)
        {
            registration = cancellationToken.Register(() =>
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
                }
            });
        }

        int exitCode;
        try
        {
            process.Start();
            var stdoutTask = ReadToEndAsync(process.StandardOutput, stdoutBuilder, cancellationToken);
            var stderrTask = ReadToEndAsync(process.StandardError, stderrBuilder, cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await stdoutTask;
            await stderrTask;
            exitCode = process.ExitCode;
        }
        finally
        {
            registration.Dispose();
            process.Dispose();
        }

        var version = await _runner.GetVersionAsync(cancellationToken);
        return (stdoutBuilder.ToString(), exitCode, stderrBuilder.ToString(), version);
    }

    private static async Task ReadToEndAsync(StreamReader reader, StringBuilder builder, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken);
            if (read <= 0) break;
            builder.Append(buffer, 0, read);
        }
    }

    /// <summary>
    /// 当 7z l 列表操作返回失败退出码时抛出相应的异常。
    /// 若 stdout 中不包含 <c>Type =</c> 字段(即 7z 无法识别归档格式,通常因为文件不是有效归档),
    /// 抛出 <see cref="InvalidDataException"/>;否则抛出 <see cref="SevenZipException"/> 表示操作级失败。
    /// </summary>
    /// <param name="stdout">7z l 的标准输出。</param>
    /// <param name="exitCode">7z 进程退出码。</param>
    /// <param name="stderr">7z 的标准错误输出。</param>
    /// <param name="version">7z 版本字符串(用于诊断)。</param>
    /// <param name="archivePath">归档文件路径(用于诊断)。</param>
    /// <exception cref="InvalidDataException">当 stdout 中不包含 <c>Type =</c> 字段时抛出。</exception>
    /// <exception cref="SevenZipException">当 stdout 中包含 <c>Type =</c> 字段但退出码仍为失败时抛出。</exception>
    private void ThrowListFailure(string stdout, int exitCode, string stderr, string? version, string archivePath)
    {
        // 若 stdout 中没有 Type = 字段,说明 7z 完全无法识别该文件的归档格式
        // (例如文件是普通文本、二进制垃圾数据等),这种情况属于"无效数据",抛 InvalidDataException
        if (!Regex.IsMatch(stdout, @"^Type\s*=\s*.+$", RegexOptions.Multiline))
        {
            throw new InvalidDataException(
                $"Unable to determine archive format for {archivePath}. " +
                $"7z list exited with code {exitCode}. Stderr: {stderr}");
        }

        throw new SevenZipException(
            $"7-Zip list operation failed (exit code {exitCode}).",
            operation: "list",
            exitCode: exitCode,
            archivePath: archivePath,
            standardError: stderr,
            sevenZipVersion: version,
            toolPath: _toolLocator.GetExecutablePath());
    }

    /// <summary>
    /// 从 7z l -slt 输出中解析归档格式。
    /// </summary>
    private static ArchiveFormat ParseArchiveFormat(string listOutput, string archivePath)
    {
        // 7z l -slt 输出中,顶层归档级信息块包含 "Type = " 行(如 Type = 7z 或 Type = zip)
        var match = Regex.Match(listOutput, @"^Type\s*=\s*(.+)$", RegexOptions.Multiline);
        if (!match.Success)
        {
            throw new InvalidDataException(
                $"Unable to determine archive format. Archive: {archivePath}. 7z output did not contain a Type field.");
        }

        var type = match.Groups[1].Value.Trim();
        return type switch
        {
            "7z" => ArchiveFormat.SevenZip,
            "zip" => ArchiveFormat.Zip,
            _ => throw new InvalidDataException(
                $"Unsupported archive format for {archivePath}. Detected Type = '{type}'.")
        };
    }

    /// <summary>
    /// 验证归档条目路径安全性。拒绝绝对路径、驱动器前缀、.. 穿越和符号链接条目。
    /// </summary>
    private static void ValidateArchiveEntries(string listOutput, string normalizedDestination)
    {
        // 7z l -slt 输出按空行分块。归档级信息块(含 Type = 字段)和条目块混合在一起,
        // 需要跳过归档级块(其 Path 字段是归档本身的绝对路径,不是条目路径)。
        var blocks = listOutput.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            // 归档级信息块包含 Type = 字段(如 "Type = 7z"),跳过它
            var archiveType = ExtractFieldValue(block, "Type");
            if (archiveType is not null)
            {
                continue;
            }

            var entryPath = ExtractFieldValue(block, "Path");
            if (entryPath is null)
            {
                continue; // 不是条目块(可能是分隔符或其他信息)
            }

            // 验证路径安全性(与原实现的 GetSafeDestinationPath 等价)
            ValidateEntryPath(entryPath, normalizedDestination);

            // 检查 Attributes 字段是否含 SymLink 或 reparse point 标记
            var attributes = ExtractFieldValue(block, "Attributes");
            if (attributes is not null)
            {
                if (attributes.Contains("SymLink", StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        $"Archive entry is a symbolic link and was rejected: {entryPath}");
                }

                if (attributes.Contains("ReparsePoint", StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        $"Archive entry is a reparse point and was rejected: {entryPath}");
                }
            }
        }
    }

    /// <summary>
    /// 从 7z l -slt 输出块中提取指定字段的值。
    /// </summary>
    private static string? ExtractFieldValue(string block, string fieldName)
    {
        var pattern = $@"^{Regex.Escape(fieldName)}\s*=\s*(.*)$";
        var match = Regex.Match(block, pattern, RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// 验证单个归档条目路径安全性。与原实现的 GetSafeDestinationPath 等价。
    /// </summary>
    private static void ValidateEntryPath(string entryKey, string normalizedDestination)
    {
        if (string.IsNullOrWhiteSpace(entryKey))
        {
            throw new IOException("Archive entry path is empty.");
        }

        var normalizedEntryKey = entryKey.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedEntryKey) || HasWindowsDrivePrefix(normalizedEntryKey))
        {
            throw new IOException($"Archive entry path is absolute: {entryKey}");
        }

        var segments = normalizedEntryKey.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            throw new IOException($"Archive entry path contains traversal: {entryKey}");
        }

        var destinationPath = Path.GetFullPath(Path.Combine(normalizedDestination, normalizedEntryKey));
        if (!IsSameOrChildPath(destinationPath, normalizedDestination))
        {
            throw new IOException($"Archive entry path escapes destination: {entryKey}");
        }
    }

    private static bool HasWindowsDrivePrefix(string path)
    {
        return path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]);
    }

    private static string NormalizeDirectoryPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsSameOrChildPath(string child, string parent)
    {
        var normalizedChild = NormalizeDirectoryPath(child) + Path.DirectorySeparatorChar;
        var normalizedParent = NormalizeDirectoryPath(parent) + Path.DirectorySeparatorChar;
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解压完成后扫描生成树,拒绝任何 reparse point(符号链接/junction)。
    /// </summary>
    private static void ValidateNoReparsePoints(string destinationDirectory)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(
            destinationDirectory, "*", SearchOption.AllDirectories))
        {
            var attrs = File.GetAttributes(entry);
            if ((attrs & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                throw new IOException(
                    $"Extracted file system entry is a reparse point and was rejected: {entry}");
            }
        }
    }

    /// <summary>
    /// 失败或取消时尝试清理本次解压创建的目录。仅当目录原本不存在或为空时清理。
    /// </summary>
    private static void TryCleanupOnFailure(string destinationDirectory)
    {
        try
        {
            if (Directory.Exists(destinationDirectory) && !Directory.EnumerateFileSystemEntries(destinationDirectory).Any())
            {
                Directory.Delete(destinationDirectory, recursive: false);
            }
        }
        catch
        {
            // 清理失败不掩盖原始错误
        }
    }
}
