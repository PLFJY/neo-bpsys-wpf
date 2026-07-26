namespace neo_bpsys_wpf.Core.Exceptions;

/// <summary>
/// 7-Zip 进程操作失败时抛出。
/// </summary>
public sealed class SevenZipException : Exception
{
    /// <summary>操作类型。</summary>
    public string Operation { get; }

    /// <summary>归档路径。</summary>
    public string? ArchivePath { get; }

    /// <summary>目标路径。</summary>
    public string? DestinationPath { get; }

    /// <summary>7-Zip 退出码。</summary>
    public int ExitCode { get; }

    /// <summary>stderr 内容。</summary>
    public string? StandardError { get; }

    /// <summary>7-Zip 版本。</summary>
    public string? SevenZipVersion { get; }

    /// <summary>7z.exe 路径。</summary>
    public string? ToolPath { get; }

    /// <summary>
    /// 创建 <see cref="SevenZipException"/> 实例。
    /// </summary>
    /// <param name="message">异常消息。</param>
    /// <param name="operation">操作类型(extract/list/test 等)。</param>
    /// <param name="exitCode">7-Zip 退出码。</param>
    /// <param name="archivePath">归档路径。</param>
    /// <param name="destinationPath">目标路径。</param>
    /// <param name="standardError">stderr 内容。</param>
    /// <param name="sevenZipVersion">7-Zip 版本。</param>
    /// <param name="toolPath">7z.exe 路径。</param>
    /// <param name="innerException">内部异常。</param>
    public SevenZipException(
        string message,
        string operation,
        int exitCode,
        string? archivePath = null,
        string? destinationPath = null,
        string? standardError = null,
        string? sevenZipVersion = null,
        string? toolPath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
        ExitCode = exitCode;
        ArchivePath = archivePath;
        DestinationPath = destinationPath;
        StandardError = standardError;
        SevenZipVersion = sevenZipVersion;
        ToolPath = toolPath;
    }
}
